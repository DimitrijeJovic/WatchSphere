using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Services
{
    /// <summary>
    /// Brings IMDb scores in from the daily dataset IMDb publishes.
    /// </summary>
    /// <remarks>
    /// There is no IMDb API to ask, and scraping the site is not on. What they
    /// do publish is a set of TSV dumps, free for personal and non-commercial
    /// use, refreshed once a day. The ratings file is a few megabytes gzipped
    /// and about one and a half million rows, so it is streamed and matched
    /// against the handful of ids the catalogue actually holds rather than
    /// read into memory.
    /// </remarks>
    public class ImdbRatingService : IImdbRatingService
    {
        private const string DatasetUrl = "https://datasets.imdbws.com/title.ratings.tsv.gz";

        private readonly IHttpClientFactory _httpClientFactory;

        private readonly AppDbContext _context;

        private readonly ILogger<ImdbRatingService> _logger;

        public ImdbRatingService(
            IHttpClientFactory httpClientFactory,
            AppDbContext context,
            ILogger<ImdbRatingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
        }

        public async Task<ImdbRatingResult> RefreshAsync(CancellationToken cancellationToken = default)
        {
            var titles = await _context.Media
                .Where(m => m.ImdbId != null && m.ImdbId != "")
                .Select(m => new { m.Id, m.ImdbId, m.ImdbRating, m.ImdbVotes })
                .ToListAsync(cancellationToken);

            if (titles.Count == 0)
            {
                return new ImdbRatingResult(0, 0, 0, "No title in the catalogue carries an IMDb id.");
            }

            var wanted = titles.ToDictionary(t => t.ImdbId!, t => t, StringComparer.Ordinal);
            var found = new Dictionary<string, (double Rating, int Votes)>(StringComparer.Ordinal);

            var client = _httpClientFactory.CreateClient("imdb");

            try
            {
                using var response = await client.GetAsync(
                    DatasetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                response.EnsureSuccessStatusCode();

                await using var network = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var unzipped = new GZipStream(network, CompressionMode.Decompress);
                using var reader = new StreamReader(unzipped);

                // Skip the header row.
                await reader.ReadLineAsync(cancellationToken);

                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    var firstTab = line.IndexOf('\t');

                    if (firstTab <= 0)
                    {
                        continue;
                    }

                    var tconst = line[..firstTab];

                    if (!wanted.ContainsKey(tconst))
                    {
                        continue;
                    }

                    var secondTab = line.IndexOf('\t', firstTab + 1);

                    if (secondTab < 0)
                    {
                        continue;
                    }

                    var ratingText = line[(firstTab + 1)..secondTab];
                    var votesText = line[(secondTab + 1)..];

                    if (double.TryParse(
                            ratingText,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var rating)
                        && int.TryParse(votesText, out var votes))
                    {
                        found[tconst] = (rating, votes);
                    }

                    // The ids arrive in order, so once every title held has
                    // been seen there is nothing left in the file to want.
                    if (found.Count == wanted.Count)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                _logger.LogWarning(ex, "Could not read the IMDb ratings dataset.");

                return new ImdbRatingResult(0, 0, titles.Count, "IMDb's dataset could not be read.");
            }

            var updated = 0;

            foreach (var (tconst, score) in found)
            {
                var known = wanted[tconst];

                // Only touch rows whose score actually moved, so a daily run
                // does not rewrite the whole table for nothing.
                if (known.ImdbRating == score.Rating && known.ImdbVotes == score.Votes)
                {
                    continue;
                }

                await _context.Media
                    .Where(m => m.Id == known.Id)
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(m => m.ImdbRating, score.Rating)
                            .SetProperty(m => m.ImdbVotes, score.Votes)
                            .SetProperty(m => m.ImdbRatingUpdatedAt, DateTime.UtcNow),
                        cancellationToken);

                updated++;
            }

            _logger.LogInformation(
                "IMDb ratings: {Found} of {Known} matched, {Updated} changed.",
                found.Count, titles.Count, updated);

            return new ImdbRatingResult(found.Count, updated, titles.Count);
        }
    }
}
