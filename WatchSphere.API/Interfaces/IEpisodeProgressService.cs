using WatchSphere.API.DTOs;

namespace WatchSphere.API.Interfaces
{
    /// <summary>
    /// Per-episode ticking, and keeping the title-level progress counter in
    /// step with it.
    /// </summary>
    public interface IEpisodeProgressService
    {
        Task<EpisodeProgressDto?> SetWatchedAsync(
            int userId,
            int episodeId,
            bool watched,
            int? rating,
            bool clearRating,
            CancellationToken cancellationToken = default);

        Task<EpisodeProgressDto?> MarkWatchedThroughAsync(
            int userId,
            int episodeId,
            CancellationToken cancellationToken = default);

        Task<EpisodeProgressDto?> ClearSeasonAsync(
            int userId,
            int seasonId,
            CancellationToken cancellationToken = default);
    }
}
