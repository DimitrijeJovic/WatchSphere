using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Models;

namespace WatchSphere.API.Services
{
    public class CatalogImportService : ICatalogImportService
    {
        private readonly AppDbContext _context;

        private readonly IEnumerable<ICatalogProvider> _providers;

        private readonly ILogger<CatalogImportService> _logger;

        public CatalogImportService(
            AppDbContext context,
            IEnumerable<ICatalogProvider> providers,
            ILogger<CatalogImportService> logger)
        {
            _context = context;
            _providers = providers;
            _logger = logger;
        }

        public IReadOnlyList<(string Name, bool Configured, IReadOnlyCollection<string> Types)> Providers =>
            _providers
                .Select(p => (p.Name, p.IsConfigured, p.SupportedTypes))
                .ToList();

        public async Task<IReadOnlyList<ExternalTitleDto>> SearchAsync(
            string query,
            string? provider,
            string? type,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<ExternalTitleDto>();
            }

            var selected = _providers.Where(p => p.IsConfigured);

            if (!string.IsNullOrEmpty(provider))
            {
                selected = selected.Where(p => p.Name == provider);
            }

            if (!string.IsNullOrEmpty(type))
            {
                selected = selected.Where(p => p.SupportedTypes.Contains(type));
            }

            // Providers are independent services, so one being slow should not
            // hold up the others.
            var batches = await Task.WhenAll(
                selected.Select(p => p.SearchAsync(query.Trim(), type, cancellationToken)));

            // Each provider ranks only against its own catalogue, so simply
            // concatenating them buries an exact match from one source under
            // another's loose matches. Re-rank across the merged set.
            var term = query.Trim();

            var results = batches
                .SelectMany(batch => batch)
                .OrderBy(item => Relevance(item.Title, term))
                .ThenByDescending(item => item.ExternalVotes ?? 0)
                .ThenByDescending(item => item.ExternalRating ?? 0)
                .ToList();

            await MarkExistingAsync(results, cancellationToken);

            return results;
        }

        public async Task<Media?> ImportAsync(
            string provider,
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var source = _providers.FirstOrDefault(p => p.Name == provider && p.IsConfigured);

            if (source == null)
            {
                throw new InvalidOperationException($"No usable provider named '{provider}'.");
            }

            var dto = await source.FetchAsync(externalId, type, cancellationToken)
                ?? throw new KeyNotFoundException("The provider does not have that title.");

            var media = await SaveAsync(dto, cancellationToken);

            await SyncEpisodesAsync(media, source, externalId, cancellationToken);
            await SyncCreditsAsync(media, source, externalId, cancellationToken);

            return media;
        }

        public async Task<Media?> RefreshAsync(int mediaId, CancellationToken cancellationToken = default)
        {
            var media = await _context.Media.FindAsync(new object[] { mediaId }, cancellationToken);

            if (media?.Source == null)
            {
                return null;
            }

            var source = _providers.FirstOrDefault(p => p.Name == media.Source && p.IsConfigured);

            if (source == null)
            {
                return null;
            }

            var externalId = media.Source switch
            {
                TmdbProviderName => media.TmdbId?.ToString(),
                KitsuProviderName => media.KitsuId?.ToString(),
                TvMazeProviderName => media.TvMazeId?.ToString(),
                _ => null
            };

            if (externalId == null)
            {
                return null;
            }

            var dto = await source.FetchAsync(externalId, media.Type, cancellationToken);

            if (dto == null)
            {
                return null;
            }

            var saved = await SaveAsync(dto, cancellationToken);

            await SyncEpisodesAsync(saved, source, externalId, cancellationToken);
            await SyncCreditsAsync(saved, source, externalId, cancellationToken);

            return saved;
        }

        public async Task<int> RefreshCreditsAsync(
            int mediaId,
            CancellationToken cancellationToken = default)
        {
            var media = await _context.Media.FindAsync(new object[] { mediaId }, cancellationToken);

            if (media?.Source == null)
            {
                return 0;
            }

            var source = _providers.FirstOrDefault(p => p.Name == media.Source && p.IsConfigured);
            var externalId = ExternalIdFor(media);

            if (source == null || externalId == null)
            {
                return 0;
            }

            await SyncCreditsAsync(media, source, externalId, cancellationToken);

            return await _context.MediaCredits.CountAsync(c => c.MediaId == mediaId, cancellationToken);
        }

        public async Task<SeedResultDto> SeedPopularAsync(
            string type,
            int count,
            string ranking,
            CancellationToken cancellationToken = default)
        {
            var result = new SeedResultDto { Type = type };

            if (type is not ("Movie" or "Series" or "Anime"))
            {
                result.Message = "Type must be Movie, Series or Anime.";

                return result;
            }

            count = Math.Clamp(count, 1, 40);
            ranking = ranking == "top" ? "top" : "trending";

            var candidates = _providers
                .Where(p => p.IsConfigured && p.SupportedTypes.Contains(type))
                .ToList();

            if (candidates.Count == 0)
            {
                result.Message = $"No configured source can supply {type}.";

                return result;
            }

            // Several providers may serve a type while only some publish a
            // ranking, so this asks each in turn and takes the first real
            // answer rather than assuming the first one listed can help.
            IReadOnlyList<ExternalTitleDto> popular = Array.Empty<ExternalTitleDto>();
            ICatalogProvider? source = null;

            foreach (var candidate in candidates)
            {
                popular = await candidate.FetchPopularAsync(type, count, ranking, cancellationToken);

                if (popular.Count > 0)
                {
                    source = candidate;

                    break;
                }
            }

            if (source == null)
            {
                result.Message = $"None of the sources for {type} publish a ranking.";

                return result;
            }

            result.Provider = source.Name;

            await MarkExistingAsync(popular.ToList(), cancellationToken);

            foreach (var candidate in popular)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (candidate.ExistingMediaId != null)
                {
                    result.Skipped++;

                    continue;
                }

                try
                {
                    var media = await ImportAsync(
                        source.Name, candidate.ExternalId, type, cancellationToken);

                    if (media != null)
                    {
                        result.Imported++;
                        result.Titles.Add(media.Title);
                    }
                }
                catch (Exception ex)
                {
                    // One title that will not come down is not a reason to
                    // abandon the rest of the batch.
                    result.Failed++;

                    _logger.LogWarning(
                        ex, "Could not import {Title} from {Provider}",
                        candidate.Title, source.Name);
                }
            }

            return result;
        }

        public async Task<FranchiseResultDto> ImportFranchiseAsync(
            int mediaId,
            CancellationToken cancellationToken = default)
        {
            var result = new FranchiseResultDto { MediaId = mediaId };

            var media = await _context.Media.FindAsync(new object[] { mediaId }, cancellationToken);

            if (media?.Source == null)
            {
                result.Message = "That title was not imported, so it has no franchise to follow.";

                return result;
            }

            result.Title = media.Title;

            var source = _providers.FirstOrDefault(p => p.Name == media.Source && p.IsConfigured);
            var externalId = ExternalIdFor(media);

            if (source == null || externalId == null)
            {
                result.Message = $"The source {media.Source} is not available right now.";

                return result;
            }

            var relations = await source.FetchRelationsAsync(externalId, media.Type, cancellationToken);

            if (relations.Count == 0)
            {
                result.Message = $"{source.Name} lists no other titles in this franchise.";

                return result;
            }

            foreach (var relation in relations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var related = await ImportAsync(
                        relation.Provider, relation.ExternalId, relation.Type, cancellationToken);

                    if (related == null || related.Id == media.Id)
                    {
                        continue;
                    }

                    var isNew = await LinkAsync(media.Id, related.Id, relation, cancellationToken);

                    if (isNew)
                    {
                        result.Imported++;
                        result.Titles.Add($"{related.Title} ({relation.RelationType})");
                    }
                    else
                    {
                        result.Skipped++;
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;

                    _logger.LogWarning(
                        ex, "Could not follow {Relation} {Title}",
                        relation.RelationType, relation.Title);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return result;
        }

        /// <summary>
        /// Records the link in both directions, so opening either title shows
        /// the other. The inverse carries the opposite name where there is one.
        /// </summary>
        private async Task<bool> LinkAsync(
            int mediaId,
            int relatedId,
            ExternalRelationDto relation,
            CancellationToken cancellationToken)
        {
            var exists = await _context.MediaRelations.AnyAsync(
                r => r.MediaId == mediaId && r.RelatedMediaId == relatedId, cancellationToken);

            if (exists)
            {
                return false;
            }

            _context.MediaRelations.Add(new MediaRelation
            {
                MediaId = mediaId,
                RelatedMediaId = relatedId,
                RelationType = relation.RelationType,
                SortOrder = relation.SortOrder
            });

            var reverseExists = await _context.MediaRelations.AnyAsync(
                r => r.MediaId == relatedId && r.RelatedMediaId == mediaId, cancellationToken);

            if (!reverseExists)
            {
                _context.MediaRelations.Add(new MediaRelation
                {
                    MediaId = relatedId,
                    RelatedMediaId = mediaId,
                    RelationType = Invert(relation.RelationType),
                    SortOrder = relation.SortOrder
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private static string Invert(string relationType)
        {
            return relationType switch
            {
                "Sequel" => "Prequel",
                "Prequel" => "Sequel",
                "SideStory" => "ParentStory",
                "ParentStory" => "SideStory",
                "Summary" => "FullStory",
                "FullStory" => "Summary",
                // Collection, AlternativeVersion, Spinoff and Other read the
                // same from either end.
                _ => relationType
            };
        }

        private static string? ExternalIdFor(Media media)
        {
            return media.Source switch
            {
                TmdbProviderName => media.TmdbId?.ToString(),
                KitsuProviderName => media.KitsuId?.ToString(),
                TvMazeProviderName => media.TvMazeId?.ToString(),
                _ => null
            };
        }

        /// <summary>
        /// Stores who made a title, and the companies behind it.
        /// </summary>
        /// <remarks>
        /// People are matched on the provider's own id where there is one and
        /// on name otherwise, so an actor in twenty films is one row rather
        /// than twenty. Nothing is deleted: a provider returning a short cast
        /// on a bad day should not quietly empty a title's credits.
        /// </remarks>
        private async Task SyncCreditsAsync(
            Media media,
            ICatalogProvider source,
            string externalId,
            CancellationToken cancellationToken)
        {
            var incoming = await source.FetchCreditsAsync(externalId, media.Type, cancellationToken);

            if (incoming.Credits.Count == 0 && incoming.Studios.Count == 0)
            {
                return;
            }

            await SyncPeopleAsync(media, incoming.Credits, source.Name, cancellationToken);
            await SyncStudiosAsync(media, incoming.Studios, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task SyncPeopleAsync(
            Media media,
            IReadOnlyList<ExternalCreditDto> credits,
            string provider,
            CancellationToken cancellationToken)
        {
            if (credits.Count == 0)
            {
                return;
            }

            var existing = await _context.MediaCredits
                .Where(c => c.MediaId == media.Id)
                .ToListAsync(cancellationToken);

            foreach (var dto in credits)
            {
                var person = await FindOrCreatePersonAsync(dto, provider, cancellationToken);

                var role = dto.Role;
                var character = Fit(dto.CharacterName, 200);
                var language = Fit(dto.Language, 30);

                var already = existing.Any(
                    c => c.PersonId == person.Id
                         && c.Role == role
                         && c.CharacterName == character
                         && c.Language == language);

                if (already)
                {
                    continue;
                }

                var credit = new MediaCredit
                {
                    MediaId = media.Id,
                    PersonId = person.Id,
                    Role = role,
                    CharacterName = character,
                    CharacterImageUrl = Fit(dto.CharacterImageUrl, 500),
                    Language = language,
                    SortOrder = dto.SortOrder
                };

                _context.MediaCredits.Add(credit);
                existing.Add(credit);
            }
        }

        private async Task<Person> FindOrCreatePersonAsync(
            ExternalCreditDto dto,
            string provider,
            CancellationToken cancellationToken)
        {
            var name = Fit(dto.PersonName, 200);

            Person? person = null;

            if (int.TryParse(dto.PersonExternalId, out var externalId))
            {
                person = provider switch
                {
                    TmdbProviderName => await _context.People
                        .FirstOrDefaultAsync(p => p.TmdbId == externalId, cancellationToken),
                    TvMazeProviderName => await _context.People
                        .FirstOrDefaultAsync(p => p.TvMazeId == externalId, cancellationToken),
                    KitsuProviderName => await _context.People
                        .FirstOrDefaultAsync(p => p.KitsuId == externalId, cancellationToken),
                    _ => null
                };
            }

            // Falling back to the name risks folding two people who share one,
            // but the alternative is a duplicate row every time a provider
            // omits its id, which happens far more often.
            person ??= await _context.People
                .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

            if (person == null)
            {
                person = new Person { Name = name, PhotoUrl = Fit(dto.PersonPhotoUrl, 500) };

                _context.People.Add(person);
            }
            else if (string.IsNullOrEmpty(person.PhotoUrl) && !string.IsNullOrEmpty(dto.PersonPhotoUrl))
            {
                person.PhotoUrl = Fit(dto.PersonPhotoUrl, 500);
            }

            if (int.TryParse(dto.PersonExternalId, out externalId))
            {
                switch (provider)
                {
                    case TmdbProviderName when person.TmdbId == null:
                        person.TmdbId = externalId;
                        break;
                    case TvMazeProviderName when person.TvMazeId == null:
                        person.TvMazeId = externalId;
                        break;
                    case KitsuProviderName when person.KitsuId == null:
                        person.KitsuId = externalId;
                        break;
                }
            }

            // Saved now so the credit below has a person id to point at.
            await _context.SaveChangesAsync(cancellationToken);

            return person;
        }

        private async Task SyncStudiosAsync(
            Media media,
            IReadOnlyList<ExternalStudioDto> studios,
            CancellationToken cancellationToken)
        {
            if (studios.Count == 0)
            {
                return;
            }

            var linked = await _context.MediaStudios
                .Where(ms => ms.MediaId == media.Id)
                .Select(ms => ms.StudioId)
                .ToListAsync(cancellationToken);

            foreach (var dto in studios)
            {
                var name = Fit(dto.Name, 150);

                var studio = await _context.Studios
                    .FirstOrDefaultAsync(
                        s => (dto.TmdbId != null && s.TmdbId == dto.TmdbId) || s.Name == name,
                        cancellationToken);

                if (studio == null)
                {
                    studio = new Studio
                    {
                        Name = name,
                        LogoUrl = Fit(dto.LogoUrl, 500),
                        TmdbId = dto.TmdbId
                    };

                    _context.Studios.Add(studio);

                    await _context.SaveChangesAsync(cancellationToken);
                }
                else if (string.IsNullOrEmpty(studio.LogoUrl) && !string.IsNullOrEmpty(dto.LogoUrl))
                {
                    studio.LogoUrl = Fit(dto.LogoUrl, 500);
                }

                if (!linked.Contains(studio.Id))
                {
                    _context.MediaStudios.Add(
                        new MediaStudio { MediaId = media.Id, StudioId = studio.Id });

                    linked.Add(studio.Id);
                }
            }
        }

        /// <summary>
        /// Brings the season and episode rows in line with the source.
        /// </summary>
        /// <remarks>
        /// Existing rows are updated and new ones added, but nothing is
        /// deleted. A provider having a bad day and returning a short list
        /// would otherwise wipe out episodes that people have already ticked
        /// off, and a stale row is much cheaper to live with than that.
        /// </remarks>
        private async Task SyncEpisodesAsync(
            Media media,
            ICatalogProvider source,
            string externalId,
            CancellationToken cancellationToken)
        {
            var incoming = await source.FetchEpisodesAsync(externalId, media.Type, cancellationToken);

            if (incoming.Count == 0)
            {
                return;
            }

            var seasons = await _context.Seasons
                .Where(s => s.MediaId == media.Id)
                .ToDictionaryAsync(s => s.SeasonNumber, cancellationToken);

            var episodes = await _context.Episodes
                .Where(e => e.MediaId == media.Id)
                .ToListAsync(cancellationToken);

            foreach (var group in incoming.GroupBy(e => e.SeasonNumber).OrderBy(g => g.Key))
            {
                if (!seasons.TryGetValue(group.Key, out var season))
                {
                    season = new Season
                    {
                        MediaId = media.Id,
                        SeasonNumber = group.Key,
                        Name = Fit(group.Key == 0 ? "Specials" : $"Season {group.Key}", 200),
                        AirDate = group.Min(e => e.AirDate)
                    };

                    _context.Seasons.Add(season);
                    seasons[group.Key] = season;

                    // Saved here so the season has an id the episodes below can
                    // point at.
                    await _context.SaveChangesAsync(cancellationToken);
                }

                foreach (var dto in group.OrderBy(e => e.EpisodeNumber))
                {
                    var episode = episodes.FirstOrDefault(
                        e => e.SeasonId == season.Id && e.EpisodeNumber == dto.EpisodeNumber);

                    if (episode == null)
                    {
                        episode = new Episode
                        {
                            SeasonId = season.Id,
                            MediaId = media.Id,
                            EpisodeNumber = dto.EpisodeNumber
                        };

                        _context.Episodes.Add(episode);
                        episodes.Add(episode);
                    }

                    episode.AbsoluteNumber = dto.AbsoluteNumber;
                    episode.Name = Fit(dto.Name, 300);
                    episode.Overview = Fit(dto.Overview, 2000);
                    episode.AirDate = dto.AirDate;
                    episode.Runtime = dto.Runtime;
                    episode.StillUrl = Fit(dto.StillUrl, 500);
                    episode.ExternalRating = dto.ExternalRating;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Counted from the stored rows rather than from this batch. A title
            // held under two providers carries the union of both their lists,
            // and taking the count from whichever ran last would leave the
            // progress bar reading against a smaller total than the episode
            // list actually shows.
            var stored = await _context.Episodes
                .CountAsync(
                    e => e.MediaId == media.Id && e.Season.SeasonNumber > 0,
                    cancellationToken);

            if (stored > 0 && stored != media.TotalEpisodes)
            {
                media.TotalEpisodes = stored;

                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Synced {Episodes} episodes across {Seasons} seasons for {Title}",
                incoming.Count,
                seasons.Count,
                media.Title);
        }

        /// <summary>
        /// Folds a title to a form two catalogues can be compared on.
        /// </summary>
        /// <remarks>
        /// The same show is punctuated differently from source to source:
        /// Kitsu writes "Journey's End" with a typographic apostrophe, TMDB
        /// with an ASCII one, and comparing them literally lands the title in
        /// the catalogue twice. Quotes and dashes are folded, spacing is
        /// collapsed, and the comparison is case-insensitive.
        /// </remarks>
        private static string NormaliseTitle(string title)
        {
            var builder = new System.Text.StringBuilder(title.Length);
            var lastWasSpace = false;

            foreach (var raw in title.Trim())
            {
                var c = raw switch
                {
                    '\u2018' or '\u2019' or '\u02bc' => '\'',
                    '\u201c' or '\u201d' => '"',
                    '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' => '-',
                    '\u00a0' => ' ',
                    _ => raw
                };

                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace && builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    lastWasSpace = true;

                    continue;
                }

                lastWasSpace = false;
                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Cuts a value down to what its column will hold.
        /// </summary>
        /// <remarks>
        /// External sources have no idea what this schema allows, and EF does
        /// not shorten anything on the way out - an over-long synopsis simply
        /// makes MySQL reject the whole save. Everything read from a provider
        /// passes through here. The cut falls back to the last word boundary so
        /// a trimmed synopsis still ends on a whole word.
        /// </remarks>
        private static string Fit(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();

            if (trimmed.Length <= max)
            {
                return trimmed;
            }

            var cut = trimmed[..(max - 1)];
            var lastSpace = cut.LastIndexOf(' ');

            if (lastSpace > max / 2)
            {
                cut = cut[..lastSpace];
            }

            return cut.TrimEnd(' ', ',', ';', ':', '-') + "…";
        }

        /// <summary>As <see cref="Fit"/>, but keeps null rather than emptying it.</summary>
        private static string? FitOptional(string? value, int max)
        {
            return string.IsNullOrWhiteSpace(value) ? null : Fit(value, max);
        }

        /// <summary>
        /// Lower is better. Deliberately blunt - the providers have already
        /// done the hard matching, this only decides which of their answers
        /// looks most like what was typed.
        /// </summary>
        private static int Relevance(string title, string query)
        {
            if (title.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return title.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : 3;
        }

        private const string TmdbProviderName = "tmdb";

        private const string KitsuProviderName = "kitsu";

        private const string TvMazeProviderName = "tvmaze";

        private async Task<Media> SaveAsync(ExternalTitleDto dto, CancellationToken cancellationToken)
        {
            var media = await FindExistingAsync(dto, cancellationToken);
            var isNew = media == null;

            media ??= new Media();

            // Every string here comes from a third party, so each is cut to
            // what its column holds before it reaches the database.
            media.Title = Fit(dto.Title, 200);
            media.OriginalTitle = Fit(dto.OriginalTitle, 200);
            media.Description = Fit(dto.Description, 4000);
            media.ReleaseYear = dto.ReleaseYear;

            // Kitsu only holds anime, so a row it classified that way keeps the
            // label even when a general catalogue later calls the same title a
            // series or a film. The more specific answer is the useful one.
            media.Type = media.Type == "Anime" ? "Anime" : dto.Type;
            media.PosterUrl = Fit(dto.PosterUrl, 500);
            media.BackdropUrl = Fit(dto.BackdropUrl, 500);
            media.AiringStatus = dto.AiringStatus;
            media.TotalEpisodes = dto.TotalEpisodes;
            media.EpisodeDuration = dto.EpisodeDuration;
            media.Studio = Fit(dto.Studio, 120);
            media.TrailerUrl = Fit(dto.TrailerUrl, 500);

            media.TmdbId = dto.TmdbId ?? media.TmdbId;
            media.KitsuId = dto.KitsuId ?? media.KitsuId;
            media.MalId = dto.MalId ?? media.MalId;
            media.AniListId = dto.AniListId ?? media.AniListId;
            media.TvMazeId = dto.TvMazeId ?? media.TvMazeId;
            media.ImdbId = FitOptional(dto.ImdbId, 20) ?? media.ImdbId;

            media.Source = dto.Provider;
            media.ExternalRating = dto.ExternalRating;
            media.ExternalVotes = dto.ExternalVotes;
            media.LastSyncedAt = DateTime.UtcNow;

            if (isNew)
            {
                _context.Media.Add(media);
            }

            await SyncGenresAsync(media, dto.Genres, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "{Action} {Title} from {Provider}",
                isNew ? "Imported" : "Refreshed",
                media.Title,
                dto.Provider);

            return media;
        }

        /// <summary>
        /// Finds the row this title already occupies, if any.
        /// </summary>
        /// <remarks>
        /// Every id the provider supplied is tried, not just the first: a title
        /// already held under one source's id will not carry another's, so
        /// stopping at the first miss is what lets the same show arrive twice
        /// from two providers. IMDb comes first because it is the one id every
        /// catalogue agrees on, and so the only one that links across them.
        /// </remarks>
        private async Task<Media?> FindExistingAsync(
            ExternalTitleDto dto,
            CancellationToken cancellationToken)
        {
            var query = _context.Media.Include(m => m.MediaGenres).AsQueryable();

            if (!string.IsNullOrWhiteSpace(dto.ImdbId))
            {
                var byImdb = await query.FirstOrDefaultAsync(
                    m => m.ImdbId == dto.ImdbId, cancellationToken);

                if (byImdb != null)
                {
                    return byImdb;
                }
            }

            if (dto.TmdbId is { } tmdb)
            {
                var match = await query.FirstOrDefaultAsync(m => m.TmdbId == tmdb, cancellationToken);

                if (match != null)
                {
                    return match;
                }
            }

            if (dto.KitsuId is { } kitsu)
            {
                var match = await query.FirstOrDefaultAsync(m => m.KitsuId == kitsu, cancellationToken);

                if (match != null)
                {
                    return match;
                }
            }

            if (dto.TvMazeId is { } tvmaze)
            {
                var match = await query.FirstOrDefaultAsync(m => m.TvMazeId == tvmaze, cancellationToken);

                if (match != null)
                {
                    return match;
                }
            }

            if (dto.MalId is { } mal)
            {
                var match = await query.FirstOrDefaultAsync(m => m.MalId == mal, cancellationToken);

                if (match != null)
                {
                    return match;
                }
            }

            return await FindHandAddedMatchAsync(dto, query, cancellationToken)
                ?? await FindCrossSourceMatchAsync(dto, query, cancellationToken);
        }

        /// <summary>
        /// Finds the same title already held under a different catalogue's id.
        /// </summary>
        /// <remarks>
        /// Anime is the case this exists for: Kitsu and TMDB both carry it, in
        /// separate id namespaces, and only TMDB knows its IMDb id — so there
        /// is no shared key to match on and the show lands twice. The guard is
        /// that the existing row carries no id from the incoming provider: this
        /// only ever adds a namespace to a row, never reconciles two rows that
        /// already disagree within one.
        /// </remarks>
        private async Task<Media?> FindCrossSourceMatchAsync(
            ExternalTitleDto dto,
            IQueryable<Media> query,
            CancellationToken cancellationToken)
        {
            if (dto.ReleaseYear == 0 || string.IsNullOrWhiteSpace(dto.Title))
            {
                return null;
            }

            // Compared in memory against everything from that year, because
            // the titles need normalising first and no database collation
            // folds a typographic apostrophe onto an ASCII one.
            var candidates = await query
                .Where(m => m.ReleaseYear == dto.ReleaseYear)
                .ToListAsync(cancellationToken);

            var wanted = NormaliseTitle(dto.Title);

            var match = candidates.FirstOrDefault(m =>
                NormaliseTitle(m.Title) == wanted
                && dto.Provider switch
                {
                    TmdbProviderName => m.TmdbId == null,
                    KitsuProviderName => m.KitsuId == null,
                    TvMazeProviderName => m.TvMazeId == null,
                    _ => false
                });

            if (match != null)
            {
                _logger.LogInformation(
                    "Merged {Title} ({Year}) with the existing row {Id} rather than adding a second",
                    dto.Title,
                    dto.ReleaseYear,
                    match.Id);
            }

            return match;
        }

        /// <summary>
        /// Adopts a title that was typed in before importing existed, so the
        /// first import fills it in rather than creating a second row and
        /// stranding the reviews and library entries already attached to it.
        /// </summary>
        /// <remarks>
        /// The conditions are deliberately strict: the same title, the same
        /// year, and no external id of any kind on the existing row. A looser
        /// rule risks silently merging two genuinely different titles, which is
        /// far worse than leaving a duplicate for an admin to notice.
        /// </remarks>
        private async Task<Media?> FindHandAddedMatchAsync(
            ExternalTitleDto dto,
            IQueryable<Media> query,
            CancellationToken cancellationToken)
        {
            if (dto.ReleaseYear == 0)
            {
                return null;
            }

            var untouched = await query
                .Where(m => m.TmdbId == null
                            && m.KitsuId == null
                            && m.MalId == null
                            && m.AniListId == null
                            && m.TvMazeId == null)
                .Where(m => m.ReleaseYear == dto.ReleaseYear)
                .ToListAsync(cancellationToken);

            var wanted = NormaliseTitle(dto.Title);

            var candidate = untouched.FirstOrDefault(m => NormaliseTitle(m.Title) == wanted);

            if (candidate != null)
            {
                _logger.LogInformation(
                    "Linked the hand-added title {Title} ({Year}) to {Provider}",
                    candidate.Title,
                    candidate.ReleaseYear,
                    dto.Provider);
            }

            return candidate;
        }

        /// <summary>
        /// Replaces the title's genres with the source's, creating any genre
        /// name that is not already known.
        /// </summary>
        private async Task SyncGenresAsync(
            Media media,
            List<string> genreNames,
            CancellationToken cancellationToken)
        {
            if (genreNames.Count == 0)
            {
                return;
            }

            var names = genreNames
                .Select(name => Fit(name, 100))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var known = await _context.Genres
                .Where(g => names.Contains(g.Name))
                .ToListAsync(cancellationToken);

            foreach (var name in names)
            {
                if (known.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var created = new Genre { Name = name };

                _context.Genres.Add(created);
                known.Add(created);
            }

            media.MediaGenres.Clear();

            foreach (var genre in known)
            {
                media.MediaGenres.Add(new MediaGenre { Genre = genre });
            }
        }

        /// <summary>
        /// Flags search results that the catalogue already holds, so the admin
        /// screen can show "In catalogue" instead of offering a second import.
        /// </summary>
        private async Task MarkExistingAsync(
            List<ExternalTitleDto> results,
            CancellationToken cancellationToken)
        {
            if (results.Count == 0)
            {
                return;
            }

            var tmdbIds = results.Where(r => r.TmdbId.HasValue).Select(r => r.TmdbId!.Value).ToList();
            var kitsuIds = results.Where(r => r.KitsuId.HasValue).Select(r => r.KitsuId!.Value).ToList();
            var tvMazeIds = results.Where(r => r.TvMazeId.HasValue).Select(r => r.TvMazeId!.Value).ToList();

            var imdbIds = results
                .Where(r => !string.IsNullOrWhiteSpace(r.ImdbId))
                .Select(r => r.ImdbId!)
                .ToList();

            var existing = await _context.Media
                .AsNoTracking()
                .Where(m => (m.TmdbId != null && tmdbIds.Contains(m.TmdbId.Value))
                            || (m.KitsuId != null && kitsuIds.Contains(m.KitsuId.Value))
                            || (m.TvMazeId != null && tvMazeIds.Contains(m.TvMazeId.Value))
                            || (m.ImdbId != null && imdbIds.Contains(m.ImdbId)))
                .Select(m => new { m.Id, m.TmdbId, m.KitsuId, m.TvMazeId, m.ImdbId })
                .ToListAsync(cancellationToken);

            foreach (var result in results)
            {
                var match = existing.FirstOrDefault(
                    m => (result.TmdbId.HasValue && m.TmdbId == result.TmdbId)
                         || (result.KitsuId.HasValue && m.KitsuId == result.KitsuId)
                         || (result.TvMazeId.HasValue && m.TvMazeId == result.TvMazeId)
                         || (!string.IsNullOrWhiteSpace(result.ImdbId) && m.ImdbId == result.ImdbId));

                result.ExistingMediaId = match?.Id;
            }
        }
    }
}
