using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Services.Providers
{
    /// <summary>
    /// Television from TVmaze. It needs no key and no registration at all,
    /// which makes it the source that works before anything is configured. It
    /// carries no films, so TMDB still covers those.
    /// </summary>
    public partial class TvMazeProvider : ICatalogProvider
    {
        public const string ProviderName = "tvmaze";

        private readonly HttpClient _http;

        private readonly ILogger<TvMazeProvider> _logger;

        public TvMazeProvider(HttpClient http, ILogger<TvMazeProvider> logger)
        {
            _http = http;
            _logger = logger;
        }

        public string Name => ProviderName;

        public IReadOnlyCollection<string> SupportedTypes { get; } = new[] { "Series", "Anime" };

        public bool IsConfigured => true;

        public async Task<IReadOnlyList<ExternalTitleDto>> SearchAsync(
            string query,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var document = await GetJsonAsync(
                $"search/shows?q={Uri.EscapeDataString(query)}", cancellationToken);

            if (document == null || document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ExternalTitleDto>();
            }

            var results = document.RootElement
                .EnumerateArray()
                .Take(12)
                .Where(entry => entry.TryGetProperty("show", out _))
                .Select(entry => Map(entry.GetProperty("show")))
                .ToList();

            // TVmaze files anime under its own genre rather than a separate
            // endpoint, so a caller asking for one kind gets only that kind.
            if (!string.IsNullOrEmpty(type))
            {
                results = results.Where(r => r.Type == type).ToList();
            }

            return results;
        }

        public async Task<ExternalTitleDto?> FetchAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var id = Uri.EscapeDataString(externalId);

            var document = await GetJsonAsync($"shows/{id}?embed[]=episodes", cancellationToken);

            if (document == null)
            {
                return null;
            }

            var dto = Map(document.RootElement);

            if (document.RootElement.TryGetProperty("_embedded", out var embedded)
                && embedded.TryGetProperty("episodes", out var episodes)
                && episodes.ValueKind == JsonValueKind.Array)
            {
                // Specials are numbered separately and would inflate the count
                // people track against.
                dto.TotalEpisodes = episodes.EnumerateArray()
                    .Count(e => e.TryGetProperty("number", out var number)
                                && number.ValueKind == JsonValueKind.Number);
            }

            dto.BackdropUrl = await FindBackdropAsync(id, cancellationToken) ?? dto.BackdropUrl;

            return dto;
        }

        public async Task<ExternalCreditsDto> FetchCreditsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var document = await GetJsonAsync(
                $"shows/{Uri.EscapeDataString(externalId)}/cast", cancellationToken);

            if (document == null || document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ExternalCreditsDto.Empty;
            }

            var credits = new List<ExternalCreditDto>();
            var order = 0;

            foreach (var item in document.RootElement.EnumerateArray().Take(25))
            {
                if (!item.TryGetProperty("person", out var person))
                {
                    continue;
                }

                var name = Text(person, "name");

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var character = item.TryGetProperty("character", out var ch) ? ch : default;

                credits.Add(new ExternalCreditDto
                {
                    Role = "Actor",
                    Provider = ProviderName,
                    PersonExternalId = person.TryGetProperty("id", out var pid)
                                       && pid.ValueKind == JsonValueKind.Number
                        ? pid.GetInt32().ToString()
                        : null,
                    PersonName = name,
                    PersonPhotoUrl = Text(person, "image", "medium")
                                     ?? Text(person, "image", "original")
                                     ?? string.Empty,
                    CharacterName = character.ValueKind == JsonValueKind.Object
                        ? Text(character, "name") ?? string.Empty
                        : string.Empty,
                    CharacterImageUrl = character.ValueKind == JsonValueKind.Object
                        ? Text(character, "image", "medium") ?? string.Empty
                        : string.Empty,
                    SortOrder = order++
                });
            }

            // The network is already carried on the title itself, and TVmaze
            // publishes no production company beyond it.
            return new ExternalCreditsDto(credits, Array.Empty<ExternalStudioDto>());
        }

        /// <summary>
        /// Always empty. TVmaze records no relationship between shows, so a
        /// spin-off and its parent are simply two unconnected entries.
        /// </summary>
        public Task<IReadOnlyList<ExternalRelationDto>> FetchRelationsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ExternalRelationDto>>(
                Array.Empty<ExternalRelationDto>());
        }

        /// <summary>
        /// Always empty. TVmaze indexes shows by id and publishes no popularity
        /// or trending ranking, and ordering one arbitrary page by its average
        /// rating would be a ranking of nothing in particular.
        /// </summary>
        public Task<IReadOnlyList<ExternalTitleDto>> FetchPopularAsync(
            string type,
            int count,
            string ranking,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ExternalTitleDto>>(Array.Empty<ExternalTitleDto>());
        }

        public async Task<IReadOnlyList<ExternalEpisodeDto>> FetchEpisodesAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var document = await GetJsonAsync(
                $"shows/{Uri.EscapeDataString(externalId)}/episodes", cancellationToken);

            if (document == null || document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ExternalEpisodeDto>();
            }

            var episodes = new List<ExternalEpisodeDto>();
            var absolute = 0;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var number = Number(item, "number");

                // Specials arrive with a null number and no place in the run.
                if (number == null)
                {
                    continue;
                }

                episodes.Add(new ExternalEpisodeDto
                {
                    SeasonNumber = Number(item, "season") ?? 1,
                    EpisodeNumber = number.Value,
                    AbsoluteNumber = ++absolute,
                    Name = Text(item, "name") ?? $"Episode {number}",
                    Overview = StripHtml(Text(item, "summary")),
                    AirDate = ParseDate(Text(item, "airdate")),
                    Runtime = Number(item, "runtime"),
                    StillUrl = Text(item, "image", "original")
                               ?? Text(item, "image", "medium")
                               ?? string.Empty
                });
            }

            return episodes;
        }

        private static DateOnly? ParseDate(string? value)
        {
            return DateOnly.TryParse(value, out var parsed) ? parsed : null;
        }

        /// <summary>
        /// The show record only carries a poster, so a wide image for the
        /// banner comes from the separate images endpoint.
        /// </summary>
        private async Task<string?> FindBackdropAsync(string id, CancellationToken cancellationToken)
        {
            var document = await GetJsonAsync($"shows/{id}/images", cancellationToken);

            if (document == null || document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var backgrounds = document.RootElement
                .EnumerateArray()
                .Where(image => Text(image, "type") == "background")
                .ToList();

            var chosen = backgrounds.FirstOrDefault(
                image => image.TryGetProperty("main", out var main)
                         && main.ValueKind == JsonValueKind.True);

            if (chosen.ValueKind == JsonValueKind.Undefined && backgrounds.Count > 0)
            {
                chosen = backgrounds[0];
            }

            if (chosen.ValueKind == JsonValueKind.Undefined
                || !chosen.TryGetProperty("resolutions", out var resolutions)
                || !resolutions.TryGetProperty("original", out var original))
            {
                return null;
            }

            return Text(original, "url");
        }

        private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "TVmaze answered {Status} for {Url}", (int)response.StatusCode, url);

                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "TVmaze request failed for {Url}", url);

                return null;
            }
        }

        private static ExternalTitleDto Map(JsonElement show)
        {
            var id = show.GetProperty("id").GetInt32();

            var genres = show.TryGetProperty("genres", out var genreList)
                         && genreList.ValueKind == JsonValueKind.Array
                ? genreList.EnumerateArray()
                    .Select(g => g.GetString())
                    .Where(g => !string.IsNullOrEmpty(g))
                    .Select(g => g!)
                    .ToList()
                : new List<string>();

            var premiered = Text(show, "premiered");

            var dto = new ExternalTitleDto
            {
                Provider = ProviderName,
                ExternalId = id.ToString(),
                TvMazeId = id,

                Title = Text(show, "name") ?? "Untitled",
                Description = StripHtml(Text(show, "summary")),

                // An "Anime" genre is how TVmaze marks animation from Japan,
                // and it maps onto the type this application already uses.
                Type = genres.Contains("Anime") ? "Anime" : "Series",

                ReleaseYear = premiered is { Length: >= 4 } && int.TryParse(premiered[..4], out var year)
                    ? year
                    : 0,

                PosterUrl = Text(show, "image", "original") ?? Text(show, "image", "medium") ?? string.Empty,

                AiringStatus = Text(show, "status") switch
                {
                    "Running" => "Airing",
                    "In Development" or "To Be Determined" => "Upcoming",
                    _ => "Finished"
                },

                EpisodeDuration = Number(show, "averageRuntime") ?? Number(show, "runtime"),

                Studio = Text(show, "network", "name") ?? Text(show, "webChannel", "name") ?? string.Empty,

                Genres = genres.Where(g => g != "Anime").ToList()
            };

            if (show.TryGetProperty("rating", out var rating)
                && rating.TryGetProperty("average", out var average)
                && average.ValueKind == JsonValueKind.Number)
            {
                dto.ExternalRating = Math.Round(average.GetDouble(), 2);
            }

            if (show.TryGetProperty("externals", out var externals))
            {
                dto.ImdbId = Text(externals, "imdb");
            }

            return dto;
        }

        /// <summary>TVmaze summaries arrive as small fragments of HTML.</summary>
        private static string StripHtml(string? html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            return WebUtility.HtmlDecode(TagPattern().Replace(html, string.Empty)).Trim();
        }

        [GeneratedRegex("<[^>]+>")]
        private static partial Regex TagPattern();

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
