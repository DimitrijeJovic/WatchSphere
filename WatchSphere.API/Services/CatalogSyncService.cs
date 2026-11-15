using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Services
{
    /// <summary>
    /// Keeps imported titles current without anyone pressing anything.
    /// </summary>
    /// <remarks>
    /// A running show gains episodes week by week, and a title's synopsis,
    /// poster and score all drift. Rather than re-reading the whole catalogue
    /// nightly — which would spend a great deal of someone else's rate limit to
    /// learn that a series which ended in 2013 still ended in 2013 — each run
    /// takes the titles that can actually have changed: those still airing,
    /// then the least recently checked.
    /// </remarks>
    public class CatalogSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ILogger<CatalogSyncService> _logger;

        private readonly SyncOptions _options;

        public CatalogSyncService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<CatalogSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = SyncOptions.From(configuration);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Catalogue sync is switched off.");

                return;
            }

            // Nothing useful happens in the first seconds of a start-up, and a
            // sync competing with the first requests only slows both.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A failed run must not take the service down with it; the
                    // next one is only a day away.
                    _logger.LogError(ex, "Catalogue sync failed.");
                }

                var wait = _options.NextRunDelay(DateTime.UtcNow);

                _logger.LogInformation(
                    "Next catalogue sync in {Hours:F1} hours.", wait.TotalHours);

                await Task.Delay(wait, stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var import = scope.ServiceProvider.GetRequiredService<ICatalogImportService>();
            var imdb = scope.ServiceProvider.GetRequiredService<IImdbRatingService>();

            // Cheap next to the per-title refreshes below: one file, one pass,
            // and IMDb republishes it daily anyway.
            try
            {
                var scores = await imdb.RefreshAsync(cancellationToken);

                _logger.LogInformation(
                    "Catalogue sync: IMDb scores matched {Matched}, changed {Updated}.",
                    scores.Matched, scores.Updated);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalogue sync: IMDb scores could not be refreshed.");
            }

            var cutoff = DateTime.UtcNow.AddDays(-_options.StaleAfterDays);

            var due = await context.Media
                .AsNoTracking()
                .Where(m => m.Source != null)
                .Where(m => m.AiringStatus == "Airing"
                            || m.AiringStatus == "Upcoming"
                            || m.LastSyncedAt == null
                            || m.LastSyncedAt < cutoff)
                // Airing first, then whatever has gone longest without a look.
                .OrderByDescending(m => m.AiringStatus == "Airing")
                .ThenBy(m => m.LastSyncedAt)
                .Take(_options.MaxPerRun)
                .Select(m => new { m.Id, m.Title })
                .ToListAsync(cancellationToken);

            if (due.Count == 0)
            {
                _logger.LogInformation("Catalogue sync: nothing due.");

                return;
            }

            var refreshed = 0;
            var failed = 0;

            foreach (var title in due)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var media = await import.RefreshAsync(title.Id, cancellationToken);

                    if (media != null)
                    {
                        refreshed++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;

                    _logger.LogWarning(ex, "Could not refresh {Title}.", title.Title);
                }

                // Paced so a nightly run never looks like a burst to the
                // providers, whose limits this shares with live traffic.
                await Task.Delay(_options.DelayBetweenTitles, cancellationToken);
            }

            _logger.LogInformation(
                "Catalogue sync: refreshed {Refreshed} of {Due}, {Failed} failed.",
                refreshed, due.Count, failed);
        }
    }

    public class SyncOptions
    {
        public bool Enabled { get; private init; } = true;

        /// <summary>Hour of the day, in UTC, a run should start.</summary>
        public int HourUtc { get; private init; } = 3;

        public int MaxPerRun { get; private init; } = 60;

        public int StaleAfterDays { get; private init; } = 14;

        public TimeSpan DelayBetweenTitles { get; private init; } = TimeSpan.FromSeconds(2);

        public static SyncOptions From(IConfiguration configuration)
        {
            return new SyncOptions
            {
                Enabled = configuration.GetValue("Sync:Enabled", true),
                HourUtc = Math.Clamp(configuration.GetValue("Sync:HourUtc", 3), 0, 23),
                MaxPerRun = Math.Clamp(configuration.GetValue("Sync:MaxPerRun", 60), 1, 500),
                StaleAfterDays = Math.Clamp(configuration.GetValue("Sync:StaleAfterDays", 14), 1, 365),
                DelayBetweenTitles = TimeSpan.FromMilliseconds(
                    Math.Clamp(configuration.GetValue("Sync:DelayMs", 2000), 0, 60000))
            };
        }

        /// <summary>How long until the next run at the configured hour.</summary>
        public TimeSpan NextRunDelay(DateTime utcNow)
        {
            var next = utcNow.Date.AddHours(HourUtc);

            if (next <= utcNow)
            {
                next = next.AddDays(1);
            }

            return next - utcNow;
        }
    }
}
