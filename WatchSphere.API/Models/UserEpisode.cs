namespace WatchSphere.API.Models
{
    /// <summary>
    /// One person having watched one episode. The running count on
    /// UserLibrary stays as the fast path for progress bars; this is the
    /// detail behind it, and what per-episode scores hang off.
    /// </summary>
    public class UserEpisode
    {
        public int UserId { get; set; }

        public int EpisodeId { get; set; }

        public DateTime WatchedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Optional score from 1 to 10 for this episode alone.</summary>
        public int? Rating { get; set; }

        public User User { get; set; } = null!;

        public Episode Episode { get; set; } = null!;
    }
}
