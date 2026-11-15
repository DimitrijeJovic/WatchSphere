using System.Text.Json;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Services.Providers
{
    /// <summary>
    /// Anime from Kitsu. Chosen over AniList and Jikan because it is the one of
    /// the three that needs no API key and was answering when this was written.
    /// </summary>
    public class KitsuProvider : ICatalogProvider
    {
        public const string ProviderName = "kitsu";

        private readonly HttpClient _http;

        private readonly ILogger<KitsuProvider> _logger;

        public KitsuProvider(HttpClient http, ILogger<KitsuProvider> logger)
        {
            _http = http;
            _logger = logger;
        }

        public string Name => ProviderName;

        public IReadOnlyCollection<string> SupportedTypes { get; } = new[] { "Anime" };

        public bool IsConfigured => true;

        public async Task<IReadOnlyList<ExternalTitleDto>> SearchAsync(
            string query,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var url = "anime"
                + $"?filter[text]={Uri.EscapeDataString(query)}"
                + "&page[limit]=12"
                + "&include=categories";

            var document = await GetJsonAsync(url, cancellationToken);

            if (document == null || !document.RootElement.TryGetProperty("data", out var data))
            {
                return Array.Empty<ExternalTitleDto>();
            }

            var categories = IndexIncluded(document.RootElement);

            return data.EnumerateArray()
                .Select(element => Map(element, categories))
                .Where(title => title != null)
                .Select(title => title!)
                .ToList();
        }

        public async Task<ExternalTitleDto?> FetchAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var document = await GetJsonAsync(
                $"anime/{Uri.EscapeDataString(externalId)}?include=categories",
                cancellationToken);

            if (document == null || !document.RootElement.TryGetProperty("data", out var data))
            {
                return null;
            }

            return Map(data, IndexIncluded(document.RootElement));
        }

        public async Task<IReadOnlyList<ExternalTitleDto>> FetchPopularAsync(
            string type,
            int count,
            string ranking,
            CancellationToken cancellationToken = default)
        {
            if (type != "Anime")
            {
                return Array.Empty<ExternalTitleDto>();
            }

            var results = new List<ExternalTitleDto>();

            const int pageSize = 20;

            // By list count rather than score: on a catalogue this size an
            // average is easily topped by something almost nobody has rated.
            var sort = ranking == "top" ? "-averageRating" : "-userCount";

            for (var offset = 0; offset < count; offset += pageSize)
            {
                var document = await GetJsonAsync(
                    $"anime?sort={sort}"
                        + $"&page[limit]={Math.Min(pageSize, count - offset)}"
                        + $"&page[offset]={offset}"
                        + "&include=categories",
                    cancellationToken);

                if (document == null || !document.RootElement.TryGetProperty("data", out var data))
                {
                    break;
                }

                var categories = IndexIncluded(document.RootElement);

                var before = results.Count;

                results.AddRange(
                    data.EnumerateArray()
                        .Select(element => Map(element, categories))
                        .Where(title => title != null)
                        .Select(title => title!));

                if (results.Count == before)
                {
                    break;
                }
            }

            return results.Take(count).ToList();
        }

        /// <summary>
        /// Kitsu is character-first: a casting ties a character to the actor
        /// who voices them, once per dubbing language.
        /// </summary>
        /// <remarks>
        /// Only the Japanese cast is taken. Unfiltered, a request comes back
        /// spread across eight or nine dubs, so twenty castings yield two or
        /// three characters voiced by whoever happens to sort first - and the
        /// original performance, the one an anime page is expected to list,
        /// might not appear at all.
        /// </remarks>
        public async Task<ExternalCreditsDto> FetchCreditsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var document = await GetJsonAsync(
                "castings"
                    + $"?filter[mediaId]={Uri.EscapeDataString(externalId)}"
                    + "&filter[mediaType]=Anime"
                    + "&filter[isCharacter]=true"
                    + "&filter[language]=Japanese"
                    + "&include=character,person"
                    // Featured first, which is Kitsu's word for the main cast.
                    + "&sort=-featured"
                    + "&page[limit]=20",
                cancellationToken);

            if (document == null || !document.RootElement.TryGetProperty("data", out var data))
            {
                return ExternalCreditsDto.Empty;
            }

            var included = IndexPeopleAndCharacters(document.RootElement);

            var credits = new List<ExternalCreditDto>();
            var order = 0;

            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("attributes", out var a)
                    || !item.TryGetProperty("relationships", out var links))
                {
                    continue;
                }

                var person = Related(links, "person", included);
                var character = Related(links, "character", included);

                if (person == null)
                {
                    continue;
                }

                credits.Add(new ExternalCreditDto
                {
                    Role = "VoiceActor",
                    Provider = ProviderName,
                    PersonExternalId = person.Value.Id,
                    PersonName = person.Value.Name,
                    PersonPhotoUrl = person.Value.ImageUrl,
                    CharacterName = character?.Name ?? string.Empty,
                    CharacterImageUrl = character?.ImageUrl ?? string.Empty,
                    Language = Text(a, "language") ?? string.Empty,
                    SortOrder = order++
                });
            }

            return new ExternalCreditsDto(credits, Array.Empty<ExternalStudioDto>());
        }

        private static (string Id, string Name, string ImageUrl)? Related(
            JsonElement links,
            string key,
            Dictionary<(string, string), (string Name, string ImageUrl)> included)
        {
            if (!links.TryGetProperty(key, out var link)
                || !link.TryGetProperty("data", out var target)
                || target.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var type = target.GetProperty("type").GetString();
            var id = target.GetProperty("id").GetString();

            if (type == null || id == null || !included.TryGetValue((type, id), out var node))
            {
                return null;
            }

            return (id, node.Name, node.ImageUrl);
        }

        private static Dictionary<(string, string), (string Name, string ImageUrl)>
            IndexPeopleAndCharacters(JsonElement root)
        {
            var map = new Dictionary<(string, string), (string, string)>();

            if (!root.TryGetProperty("included", out var included))
            {
                return map;
            }

            foreach (var item in included.EnumerateArray())
            {
                var type = item.GetProperty("type").GetString();
                var id = item.GetProperty("id").GetString();

                if (type == null || id == null || !item.TryGetProperty("attributes", out var a))
                {
                    continue;
                }

                map[(type, id)] = (
                    Text(a, "name") ?? Text(a, "canonicalName") ?? "Unknown",
                    Text(a, "image", "original") ?? Text(a, "image", "large") ?? string.Empty);
            }

            return map;
        }

        /// <summary>
        /// Kitsu keeps franchise links in a separate collection, reached by
        /// filtering on the source rather than through the anime's own path.
        /// </summary>
        public async Task<IReadOnlyList<ExternalRelationDto>> FetchRelationsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var document = await GetJsonAsync(
                "media-relationships"
                    + $"?filter[sourceId]={Uri.EscapeDataString(externalId)}"
                    + "&filter[sourceType]=Anime"
                    + "&include=destination"
                    + "&page[limit]=20",
                cancellationToken);

            if (document == null || !document.RootElement.TryGetProperty("data", out var data))
            {
                return Array.Empty<ExternalRelationDto>();
            }

            var destinations = IndexDestinations(document.RootElement);

            var relations = new List<ExternalRelationDto>();
            var order = 0;

            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("attributes", out var a)
                    || !item.TryGetProperty("relationships", out var links)
                    || !links.TryGetProperty("destination", out var destination)
                    || !destination.TryGetProperty("data", out var target)
                    || target.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // Manga and other non-anime destinations have no place in a
                // catalogue of things you watch.
                if (target.GetProperty("type").GetString() != "anime")
                {
                    continue;
                }

                var id = target.GetProperty("id").GetString();

                if (id == null || !destinations.TryGetValue(id, out var node))
                {
                    continue;
                }

                relations.Add(new ExternalRelationDto
                {
                    RelationType = MapRole(Text(a, "role")),
                    Provider = ProviderName,
                    ExternalId = id,
                    Type = "Anime",
                    Title = node.Title,
                    ReleaseYear = node.Year,
                    PosterUrl = node.PosterUrl,
                    SortOrder = ++order
                });
            }

            return relations;
        }

        private static string MapRole(string? role)
        {
            return role switch
            {
                "sequel" => "Sequel",
                "prequel" => "Prequel",
                "side_story" => "SideStory",
                "parent_story" => "ParentStory",
                "alternative_setting" or "alternative_version" => "AlternativeVersion",
                "spinoff" => "Spinoff",
                "summary" => "Summary",
                "full_story" => "FullStory",
                "adaptation" => "Adaptation",
                _ => "Other"
            };
        }

        private static Dictionary<string, (string Title, int Year, string PosterUrl)> IndexDestinations(
            JsonElement root)
        {
            var map = new Dictionary<string, (string, int, string)>();

            if (!root.TryGetProperty("included", out var included))
            {
                return map;
            }

            foreach (var item in included.EnumerateArray())
            {
                if (item.GetProperty("type").GetString() != "anime"
                    || !item.TryGetProperty("attributes", out var a))
                {
                    continue;
                }

                var id = item.GetProperty("id").GetString();

                if (id == null)
                {
                    continue;
                }

                var start = Text(a, "startDate");

                map[id] = (
                    Text(a, "titles", "en") ?? Text(a, "canonicalTitle") ?? "Untitled",
                    start is { Length: >= 4 } && int.TryParse(start[..4], out var year) ? year : 0,
                    Text(a, "posterImage", "original") ?? Text(a, "posterImage", "large") ?? string.Empty);
            }

            return map;
        }

        public async Task<IReadOnlyList<ExternalEpisodeDto>> FetchEpisodesAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var episodes = new List<ExternalEpisodeDto>();

            // Kitsu pages at twenty by default and long-running series go well
            // past that, so this walks on until a page comes back short.
            const int pageSize = 20;

            for (var offset = 0; offset < 2000; offset += pageSize)
            {
                var document = await GetJsonAsync(
                    $"anime/{Uri.EscapeDataString(externalId)}/episodes"
                        + $"?page[limit]={pageSize}&page[offset]={offset}&sort=number",
                    cancellationToken);

                if (document == null || !document.RootElement.TryGetProperty("data", out var data))
                {
                    break;
                }

                var returned = 0;

                foreach (var item in data.EnumerateArray())
                {
                    returned++;

                    if (!item.TryGetProperty("attributes", out var a))
                    {
                        continue;
                    }

                    var number = Number(a, "number");

                    if (number == null)
                    {
                        continue;
                    }

                    episodes.Add(new ExternalEpisodeDto
                    {
                        SeasonNumber = Number(a, "seasonNumber") ?? 1,
                        EpisodeNumber = number.Value,
                        AbsoluteNumber = number.Value,
                        Name = Text(a, "titles", "en_us")
                               ?? Text(a, "canonicalTitle")
                               ?? $"Episode {number}",
                        Overview = Text(a, "synopsis") ?? string.Empty,
                        AirDate = DateOnly.TryParse(Text(a, "airdate"), out var aired) ? aired : null,
                        Runtime = Number(a, "length"),
                        StillUrl = Text(a, "thumbnail", "original") ?? string.Empty
                    });
                }

                if (returned < pageSize)
                {
                    break;
                }
            }

            return episodes;
        }

        private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Kitsu answered {Status} for {Url}", (int)response.StatusCode, url);

                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Kitsu request failed for {Url}", url);

                return null;
            }
        }

        /// <summary>
        /// Kitsu answers in JSON:API, where related records arrive in a flat
        /// "included" array rather than nested inside the record.
        /// </summary>
        private static Dictionary<string, string> IndexIncluded(JsonElement root)
        {
            var map = new Dictionary<string, string>();

            if (!root.TryGetProperty("included", out var included))
            {
                return map;
            }

            foreach (var item in included.EnumerateArray())
            {
                if (item.GetProperty("type").GetString() != "categories")
                {
                    continue;
                }

                var id = item.GetProperty("id").GetString();
                var title = item.GetProperty("attributes").GetProperty("title").GetString();

                if (id != null && title != null)
                {
                    map[id] = title;
                }
            }

            return map;
        }

        private static ExternalTitleDto? Map(JsonElement element, Dictionary<string, string> categories)
        {
            if (!element.TryGetProperty("attributes", out var a))
            {
                return null;
            }

            var kitsuId = element.GetProperty("id").GetString();

            var english = Text(a, "titles", "en") ?? Text(a, "titles", "en_jp");
            var canonical = Text(a, "canonicalTitle");

            var start = Text(a, "startDate");

            var dto = new ExternalTitleDto
            {
                Provider = ProviderName,
                ExternalId = kitsuId ?? string.Empty,
                KitsuId = int.TryParse(kitsuId, out var parsed) ? parsed : null,

                Title = english ?? canonical ?? "Untitled",
                OriginalTitle = Text(a, "titles", "ja_jp") ?? canonical ?? string.Empty,
                Description = Text(a, "synopsis") ?? string.Empty,

                // Kitsu splits films out as their own subtype; everything else
                // it serves is episodic and belongs under Anime here.
                Type = "Anime",

                ReleaseYear = start != null && start.Length >= 4 && int.TryParse(start[..4], out var year)
                    ? year
                    : 0,

                PosterUrl = Text(a, "posterImage", "original")
                            ?? Text(a, "posterImage", "large")
                            ?? string.Empty,

                BackdropUrl = Text(a, "coverImage", "original")
                              ?? Text(a, "coverImage", "large")
                              ?? string.Empty,

                AiringStatus = Text(a, "status") switch
                {
                    "current" => "Airing",
                    "upcoming" or "unreleased" or "tba" => "Upcoming",
                    _ => "Finished"
                },

                TotalEpisodes = Number(a, "episodeCount"),
                EpisodeDuration = Number(a, "episodeLength"),
                Genres = CategoryNames(element, categories)
            };

            if (Text(a, "subtype") == "movie")
            {
                dto.TotalEpisodes = 1;
            }

            // Kitsu rates out of a hundred; everything in this application is
            // on a ten-point scale.
            if (double.TryParse(
                    Text(a, "averageRating"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var rating))
            {
                dto.ExternalRating = Math.Round(rating / 10.0, 2);
            }

            dto.ExternalVotes = Number(a, "userCount");

            var youtubeId = Text(a, "youtubeVideoId");

            if (!string.IsNullOrEmpty(youtubeId))
            {
                dto.TrailerUrl = $"https://www.youtube.com/watch?v={youtubeId}";
            }

            return dto;
        }

        private static List<string> CategoryNames(JsonElement element, Dictionary<string, string> categories)
        {
            var names = new List<string>();

            if (!element.TryGetProperty("relationships", out var relationships)
                || !relationships.TryGetProperty("categories", out var link)
                || !link.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                return names;
            }

            foreach (var item in data.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString();

                if (id != null && categories.TryGetValue(id, out var name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static string? Text(JsonElement element, params string[] path)
        {
            var current = element;

            foreach (var key in path)
            {
                if (current.ValueKind != JsonValueKind.Object
                    || !current.TryGetProperty(key, out current))
                {
                    return null;
                }
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        }

        private static int? Number(JsonElement element, string key)
        {
            return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : null;
        }
    }
}
