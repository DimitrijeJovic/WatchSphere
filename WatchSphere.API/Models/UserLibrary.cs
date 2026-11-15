namespace WatchSphere.API.Models
{
    public class UserLibrary
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int MediaId { get; set; }

        /// <summary>Watching, Completed, Planned, OnHold or Dropped.</summary>
        public string Status { get; set; } = "Planned";

        /// <summary>Personal score from 1 to 10, independent of any review.</summary>
        public int? Score { get; set; }

        /// <summary>How far the user got. Never exceeds Media.TotalEpisodes.</summary>
        public int EpisodesWatched { get; set; }

        public int RewatchCount { get; set; }

        public bool IsFavorite { get; set; } = false;

        public string Notes { get; set; } = string.Empty;

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;

        public Media Media { get; set; } = null!;
    }
}
