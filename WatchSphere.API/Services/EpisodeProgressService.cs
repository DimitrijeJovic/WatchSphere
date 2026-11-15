using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Models;

namespace WatchSphere.API.Services
{
    public class EpisodeProgressService : IEpisodeProgressService
    {
        private readonly AppDbContext _context;

        public EpisodeProgressService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<EpisodeProgressDto?> SetWatchedAsync(
            int userId,
            int episodeId,
            bool watched,
            int? rating,
            bool clearRating,
            CancellationToken cancellationToken = default)
        {
            var episode = await _context.Episodes
                .FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);

            if (episode == null)
            {
                return null;
            }

            var mark = await _context.UserEpisodes
                .FirstOrDefaultAsync(
                    ue => ue.UserId == userId && ue.EpisodeId == episodeId, cancellationToken);

            if (watched)
            {
                if (mark == null)
                {
                    mark = new UserEpisode { UserId = userId, EpisodeId = episodeId };

                    _context.UserEpisodes.Add(mark);
                }

                if (clearRating)
                {
                    mark.Rating = null;
                }
                else if (rating.HasValue)
                {
                    mark.Rating = rating.Value;
                }
            }
            else if (mark != null)
            {
                // Un-ticking an episode drops any score with it; a score for
                // something you have not watched would be meaningless.
                _context.UserEpisodes.Remove(mark);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return await BuildAsync(userId, episode, cancellationToken);
        }

        public async Task<EpisodeProgressDto?> MarkWatchedThroughAsync(
            int userId,
            int episodeId,
            CancellationToken cancellationToken = default)
        {
            var episode = await _context.Episodes
                .Include(e => e.Season)
                .FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);

            if (episode == null)
            {
                return null;
            }

            // Everything that airs before this one, ordered the way the series
            // is actually watched rather than by id.
            var upTo = await _context.Episodes
                .Where(e => e.MediaId == episode.MediaId)
                .Where(e => e.Season.SeasonNumber < episode.Season.SeasonNumber
                            || (e.Season.SeasonNumber == episode.Season.SeasonNumber
                                && e.EpisodeNumber <= episode.EpisodeNumber))
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            var already = await _context.UserEpisodes
                .Where(ue => ue.UserId == userId && upTo.Contains(ue.EpisodeId))
                .Select(ue => ue.EpisodeId)
                .ToListAsync(cancellationToken);

            var missing = upTo.Except(already).ToList();

            if (missing.Count > 0)
            {
                _context.UserEpisodes.AddRange(
                    missing.Select(id => new UserEpisode { UserId = userId, EpisodeId = id }));

                await _context.SaveChangesAsync(cancellationToken);
            }

            return await BuildAsync(userId, episode, cancellationToken);
        }

        public async Task<EpisodeProgressDto?> ClearSeasonAsync(
            int userId,
            int seasonId,
            CancellationToken cancellationToken = default)
        {
            var season = await _context.Seasons
                .Include(s => s.Episodes)
                .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

            if (season == null)
            {
                return null;
            }

            var episodeIds = season.Episodes.Select(e => e.Id).ToList();

            var marks = await _context.UserEpisodes
                .Where(ue => ue.UserId == userId && episodeIds.Contains(ue.EpisodeId))
                .ToListAsync(cancellationToken);

            if (marks.Count > 0)
            {
                _context.UserEpisodes.RemoveRange(marks);

                await _context.SaveChangesAsync(cancellationToken);
            }

            var sample = season.Episodes.FirstOrDefault();

            if (sample == null)
            {
                return new EpisodeProgressDto { MediaId = season.MediaId, SeasonId = season.Id };
            }

            return await BuildAsync(userId, sample, cancellationToken);
        }

        /// <summary>
        /// Recounts what the caller has watched and writes it back to the
        /// library entry, so the poster progress bar and the episode list can
        /// never drift apart.
        /// </summary>
        private async Task<EpisodeProgressDto> BuildAsync(
            int userId,
            Episode episode,
            CancellationToken cancellationToken)
        {
            var watchedForMedia = await _context.UserEpisodes
                .CountAsync(
                    ue => ue.UserId == userId && ue.Episode.MediaId == episode.MediaId,
                    cancellationToken);

            var seasonWatched = await _context.UserEpisodes
                .CountAsync(
                    ue => ue.UserId == userId && ue.Episode.SeasonId == episode.SeasonId,
                    cancellationToken);

            var mark = await _context.UserEpisodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    ue => ue.UserId == userId && ue.EpisodeId == episode.Id, cancellationToken);

            var dto = new EpisodeProgressDto
            {
                EpisodeId = episode.Id,
                Watched = mark != null,
                MyRating = mark?.Rating,
                MediaId = episode.MediaId,
                SeasonId = episode.SeasonId,
                SeasonWatchedCount = seasonWatched
            };

            var entry = await _context.UserLibraries
                .Include(ul => ul.Media)
                .FirstOrDefaultAsync(
                    ul => ul.UserId == userId && ul.MediaId == episode.MediaId, cancellationToken);

            if (entry == null)
            {
                return dto;
            }

            var total = entry.Media.Type == "Movie" ? 1 : entry.Media.TotalEpisodes;

            entry.EpisodesWatched = total.HasValue
                ? Math.Min(watchedForMedia, total.Value)
                : watchedForMedia;

            if (entry.EpisodesWatched > 0 && entry.StartedAt == null)
            {
                entry.StartedAt = DateTime.UtcNow;
            }

            if (total.HasValue && entry.EpisodesWatched == total.Value)
            {
                entry.Status = "Completed";
                entry.FinishedAt ??= DateTime.UtcNow;
            }
            else if (entry.EpisodesWatched > 0 && entry.Status is "Planned" or "Completed")
            {
                entry.Status = "Watching";
                entry.FinishedAt = null;
            }
            else if (entry.EpisodesWatched == 0 && entry.Status == "Watching")
            {
                entry.Status = "Planned";
                entry.StartedAt = null;
            }

            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            dto.EpisodesWatched = entry.EpisodesWatched;
            dto.TotalEpisodes = total;
            dto.Status = entry.Status;
            dto.ProgressPercent = total is > 0
                ? Math.Min(100, (int)Math.Round(entry.EpisodesWatched * 100.0 / total.Value))
                : 0;

            return dto;
        }
    }
}
