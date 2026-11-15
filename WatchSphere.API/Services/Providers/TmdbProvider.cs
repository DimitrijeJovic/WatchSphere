using System.Text.Json;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Services.Providers
{
    /// <summary>
    /// Films and television from The Movie Database. Needs a free API key,
    /// which the provider reports as missing rather than failing per request.
    /// </summary>
    public class TmdbProvider : ICatalogProvider
    {
        public const string ProviderName = "tmdb";

        private const string PosterBase = "https://image.tmdb.org/t/p/w500";

        private const string BackdropBase = "https://image.tmdb.org/t/p/w1280";

        private const string StillBase = "https://image.tmdb.org/t/p/w300";

        private const string ProfileBase = "https://image.tmdb.org/t/p/w185";

        private const string LogoBase = "https://image.tmdb.org/t/p/w154";

        private readonly HttpClient _http;

        private readonly ILogger<TmdbProvider> _logger;

        private readonly string? _apiKey;

        private readonly bool _useBearerToken;

        public TmdbProvider(HttpClient http, IConfiguration configuration, ILogger<TmdbProvider> logger)
        {
            _http = http;
            _logger = logger;

            _apiKey = (Environment.GetEnvironmentVariable("WATCHSPHERE_TMDB_KEY")
                       ?? configuration["Tmdb:ApiKey"])?.Trim();

            // TMDB issues two credentials for the same account: a short v3 API
            // key that goes in the query string, and a v4 read access token
            // that goes in an Authorization header. Either is accepted here, so
            // whichever one is copied out of the dashboard works.
            _useBearerToken = _apiKey is { Length: > 0 } && _apiKey.Count(c => c == '.') == 2;

            if (_useBearerToken)
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public string Name => ProviderName;

        public IReadOnlyCollection<string> SupportedTypes { get; } = new[] { "Movie", "Series" };

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<IReadOnlyList<ExternalTitleDto>> SearchAsync(
            string query,
            string? type,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return Array.Empty<ExternalTitleDto>();
            }

            var wanted = type is "Movie" or "Series"
                ? new[] { type }
                : new[] { "Movie", "Series" };

            var results = new List<ExternalTitleDto>();

            foreach (var kind in wanted)
            {
                var path = kind == "Movie" ? "search/movie" : "search/tv";

                var document = await GetJsonAsync(
                    $"{path}?query={Uri.EscapeDataString(query)}&include_adult=false",
                    cancellationToken);

                if (document == null || !document.RootElement.TryGetProperty("results", out var items))
                {
                    continue;
                }

                results.AddRange(
                    items.EnumerateArray()
                        .Take(10)
                        .Select(item => MapSummary(item, kind!)));
            }

            // A search across both endpoints comes back in two separately
            // ranked blocks, so it is reordered by the site's own popularity.
            return results
                .OrderByDescending(item => item.ExternalVotes ?? 0)
                .ToList();
        }

        public async Task<ExternalTitleDto?> FetchAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            var isMovie = type == "Movie";
            var path = isMovie ? "movie" : "tv";

            var document = await GetJsonAsync(
                $"{path}/{Uri.EscapeDataString(externalId)}?append_to_response=external_ids,videos",
                cancellationToken);

            if (document == null)
            {
                return null;
            }

            return MapDetail(document.RootElement, isMovie ? "Movie" : "Series");
        }

        public async Task<IReadOnlyList<ExternalTitleDto>> FetchPopularAsync(
            string type,
            int count,
            string ranking,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured || type is not ("Movie" or "Series"))
            {
                return Array.Empty<ExternalTitleDto>();
            }

            var isMovie = type == "Movie";

            var path = ranking == "top"
                ? (isMovie ? "movie/top_rated" : "tv/top_rated")
                : (isMovie ? "trending/movie/week" : "trending/tv/week");

            var results = new List<ExternalTitleDto>();

            // Trending returns twenty per page, so more than that means asking
            // for the next one.
            for (var page = 1; page <= 5 && results.Count < count; page++)
            {
                var document = await GetJsonAsync($"{path}?page={page}", cancellationToken);

                if (document == null || !document.RootElement.TryGetProperty("results", out var items))
                {
                    break;
                }

                var before = results.Count;

                results.AddRange(items.EnumerateArray().Select(item => MapSummary(item, type)));

                if (results.Count == before)
                {
                    break;
                }
            }

            return results.Take(count).ToList();
        }

        public async Task<ExternalCreditsDto> FetchCreditsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return ExternalCreditsDto.Empty;
            }

            var path = type == "Movie" ? "movie" : "tv";
            var id = Uri.EscapeDataString(externalId);

            var document = await GetJsonAsync(
                $"{path}/{id}?append_to_response=credits", cancellationToken);

            if (document == null)
            {
                return ExternalCreditsDto.Empty;
            }

            var root = document.RootElement;
            var credits = new List<ExternalCreditDto>();

            if (root.TryGetProperty("credits", out var block))
            {
                if (block.TryGetProperty("cast", out var cast) && cast.ValueKind == JsonValueKind.Array)
                {
                    // Billing order is meaningful; past the top of it a film
                    // lists every extra with a line.
                    credits.AddRange(
                        cast.EnumerateArray()
                            .Take(20)
                            .Select((item, index) => new ExternalCreditDto
                            {
                                Role = "Actor",
                                Provider = ProviderName,
                                PersonExternalId = Number(item, "id")?.ToString(),
                                PersonName = Text(item, "name") ?? string.Empty,
                                PersonPhotoUrl = Image(Text(item, "profile_path"), ProfileBase),
                                CharacterName = Text(item, "character") ?? string.Empty,
                                SortOrder = Number(item, "order") ?? index
                            })
                            .Where(c => c.PersonName.Length > 0));
                }

                if (block.TryGetProperty("crew", out var crew) && crew.ValueKind == JsonValueKind.Array)
                {
                    // Only the jobs anyone looks up. The full crew of a film
                    // runs to hundreds of names.
                    var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Director"] = "Director",
                        ["Writer"] = "Writer",
                        ["Screenplay"] = "Writer",
                        ["Novel"] = "Writer",
                        ["Producer"] = "Producer",
                        ["Executive Producer"] = "Producer",
                        ["Creator"] = "Creator"
                    };

                    credits.AddRange(
                        crew.EnumerateArray()
                            .Where(item => Text(item, "job") is { } job && wanted.ContainsKey(job))
                            .Take(20)
                            .Select((item, index) => new ExternalCreditDto
                            {
                                Role = wanted[Text(item, "job")!],
                                Provider = ProviderName,
                                PersonExternalId = Number(item, "id")?.ToString(),
                                PersonName = Text(item, "name") ?? string.Empty,
                                PersonPhotoUrl = Image(Text(item, "profile_path"), ProfileBase),
                                SortOrder = index
                            })
                            .Where(c => c.PersonName.Length > 0));
                }
            }

            var studios = new List<ExternalStudioDto>();

            if (root.TryGetProperty("production_companies", out var companies)
                && companies.ValueKind == JsonValueKind.Array)
            {
                studios.AddRange(
                    companies.EnumerateArray()
                        .Select(item => new ExternalStudioDto
                        {
                            Name = Text(item, "name") ?? string.Empty,
                            LogoUrl = Image(Text(item, "logo_path"), LogoBase),
                            TmdbId = Number(item, "id")
                        })
                        .Where(s => s.Name.Length > 0));
            }

            return new ExternalCreditsDto(credits, studios);
        }

        /// <summary>
        /// TMDB groups a film series into a "collection", which is exactly a
        /// franchise. Television has no equivalent there, so a series returns
        /// nothing rather than a guess built from recommendations.
        /// </summary>
        public async Task<IReadOnlyList<ExternalRelationDto>> FetchRelationsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured || type != "Movie")
            {
                return Array.Empty<ExternalRelationDto>();
            }

            var film = await GetJsonAsync(
                $"movie/{Uri.EscapeDataString(externalId)}", cancellationToken);

            if (film == null
                || !film.RootElement.TryGetProperty("belongs_to_collection", out var belongs)
                || belongs.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<ExternalRelationDto>();
            }

            var collectionId = Number(belongs, "id");

            if (collectionId == null)
            {
                return Array.Empty<ExternalRelationDto>();
            }

            var collection = await GetJsonAsync($"collection/{collectionId}", cancellationToken);

            if (collection == null || !collection.RootElement.TryGetProperty("parts", out var parts))
            {
                return Array.Empty<ExternalRelationDto>();
            }

            var relations = new List<ExternalRelationDto>();
            var order = 0;

            foreach (var part in parts.EnumerateArray()
                         .OrderBy(p => Text(p, "release_date") ?? "9999"))
            {
                var id = Number(part, "id");

                // The film itself is in its own collection.
                if (id == null || id.Value.ToString() == externalId)
                {
                    continue;
                }

                var date = Text(part, "release_date");

                relations.Add(new ExternalRelationDto
                {
                    RelationType = "Collection",
                    Provider = ProviderName,
                    ExternalId = id.Value.ToString(),
                    Type = "Movie",
                    Title = Text(part, "title") ?? "Untitled",
                    ReleaseYear = Year(date),
                    PosterUrl = Image(Text(part, "poster_path"), PosterBase),
                    SortOrder = ++order
                });
            }

            return relations;
        }

        public async Task<IReadOnlyList<ExternalEpisodeDto>> FetchEpisodesAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            // A film has nothing to break down, and TMDB serves episodes only
            // under the television endpoints.
            if (!IsConfigured || type == "Movie")
            {
                return Array.Empty<ExternalEpisodeDto>();
            }

            var id = Uri.EscapeDataString(externalId);

            var show = await GetJsonAsync($"tv/{id}", cancellationToken);

            if (show == null || !show.RootElement.TryGetProperty("seasons", out var seasons))
            {
                return Array.Empty<ExternalEpisodeDto>();
            }

            var seasonNumbers = seasons.EnumerateArray()
                .Select(season => Number(season, "season_number"))
                .Where(number => number.HasValue)
                .Select(number => number!.Value)
                .OrderBy(number => number)
                .ToList();

            var episodes = new List<ExternalEpisodeDto>();
            var absolute = 0;

            // One request per season is what TMDB requires; there is no
            // endpoint that returns a whole run at once.
            foreach (var seasonNumber in seasonNumbers)
            {
                var season = await GetJsonAsync($"tv/{id}/season/{seasonNumber}", cancellationToken);

                if (season == null || !season.RootElement.TryGetProperty("episodes", out var list))
                {
                    continue;
                }

                foreach (var item in list.EnumerateArray())
                {
                    var number = Number(item, "episode_number");

                    if (number == null)
                    {
                        continue;
                    }

                    // Specials sit in season zero with their own numbering, so
                    // they stay out of the running count people track against.
                    if (seasonNumber > 0)
                    {
                        absolute++;
                    }

                    episodes.Add(new ExternalEpisodeDto
                    {
                        SeasonNumber = seasonNumber,
                        EpisodeNumber = number.Value,
                        AbsoluteNumber = seasonNumber > 0 ? absolute : 0,
                        Name = Text(item, "name") ?? $"Episode {number}",
                        Overview = Text(item, "overview") ?? string.Empty,
                        AirDate = DateOnly.TryParse(Text(item, "air_date"), out var aired) ? aired : null,
                        Runtime = Number(item, "runtime"),
                        StillUrl = Image(Text(item, "still_path"), StillBase),
                        ExternalRating = Decimal(item, "vote_average")
                    });
                }
            }

            return episodes;
        }

        private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            // With a bearer token the credential is already on the header, so
            // the URL is left exactly as the caller built it.
            var requestUrl = _useBearerToken
                ? url
                : $"{url}{(url.Contains('?') ? '&' : '?')}api_key={_apiKey}";

            try
            {
                using var response = await _http.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "TMDB answered {Status} for {Path}",
                        (int)response.StatusCode,
                        url.Split('?')[0]);

                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "TMDB request failed for {Path}", url.Split('?')[0]);

                return null;
            }
        }

        private static ExternalTitleDto MapSummary(JsonElement item, string type)
        {
            var isMovie = type == "Movie";
            var id = item.GetProperty("id").GetInt32();

            var date = Text(item, isMovie ? "release_date" : "first_air_date");

            return new ExternalTitleDto
            {
                Provider = ProviderName,
                ExternalId = id.ToString(),
                TmdbId = id,
                Type = type,

                Title = Text(item, isMovie ? "title" : "name") ?? "Untitled",
                OriginalTitle = Text(item, isMovie ? "original_title" : "original_name") ?? string.Empty,
                Description = Text(item, "overview") ?? string.Empty,

                ReleaseYear = Year(date),

                PosterUrl = Image(Text(item, "poster_path"), PosterBase),
                BackdropUrl = Image(Text(item, "backdrop_path"), BackdropBase),

                ExternalRating = Decimal(item, "vote_average"),
                ExternalVotes = Number(item, "vote_count"),

                TotalEpisodes = isMovie ? 1 : null
            };
        }

        private static ExternalTitleDto MapDetail(JsonElement root, string type)
        {
            var dto = MapSummary(root, type);

            dto.Genres = root.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array
                ? genres.EnumerateArray()
                    .Select(g => Text(g, "name"))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Select(name => name!)
                    .ToList()
                : new List<string>();

            dto.Studio = root.TryGetProperty("production_companies", out var companies)
                         && companies.ValueKind == JsonValueKind.Array
                ? companies.EnumerateArray()
                    .Select(c => Text(c, "name"))
                    .FirstOrDefault(name => !string.IsNullOrEmpty(name)) ?? string.Empty
                : string.Empty;

            if (type == "Movie")
            {
                dto.EpisodeDuration = Number(root, "runtime");
                dto.TotalEpisodes = 1;
                dto.ImdbId = Text(root, "imdb_id");

                dto.AiringStatus = IsFuture(Text(root, "release_date")) ? "Upcoming" : "Finished";
            }
            else
            {
                dto.TotalEpisodes = Number(root, "number_of_episodes");

                // TMDB reports a list because runtimes vary across a run; the
                // first entry is the usual length.
                dto.EpisodeDuration = root.TryGetProperty("episode_run_time", out var runtimes)
                                      && runtimes.ValueKind == JsonValueKind.Array
                                      && runtimes.GetArrayLength() > 0
                    ? runtimes[0].GetInt32()
                    : null;

                dto.AiringStatus = Text(root, "status") switch
                {
                    "Returning Series" or "In Production" => "Airing",
                    "Planned" => "Upcoming",
                    _ => "Finished"
                };

                if (root.TryGetProperty("external_ids", out var external))
                {
                    dto.ImdbId = Text(external, "imdb_id");
                }
            }

            dto.TrailerUrl = FindTrailer(root) ?? string.Empty;

            return dto;
        }

        private static string? FindTrailer(JsonElement root)
        {
            if (!root.TryGetProperty("videos", out var videos)
                || !videos.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var video in results.EnumerateArray())
            {
                if (Text(video, "site") == "YouTube" && Text(video, "type") == "Trailer")
                {
                    return $"https://www.youtube.com/watch?v={Text(video, "key")}";
                }
            }

            return null;
        }

        private static bool IsFuture(string? date)
        {
            return DateTime.TryParse(date, out var parsed) && parsed > DateTime.UtcNow;
        }

        private static string Image(string? path, string prefix)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : prefix + path;
        }

        private static int Year(string? date)
        {
            return date is { Length: >= 4 } && int.TryParse(date[..4], out var year) ? year : 0;
        }

        private static string? Text(JsonElement element, string key)
        {
            return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int? Number(JsonElement element, string key)
        {
            return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : null;
        }

        private static double? Decimal(JsonElement element, string key)
        {
            return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
                ? Math.Round(value.GetDouble(), 2)
                : null;
        }
    }
}
