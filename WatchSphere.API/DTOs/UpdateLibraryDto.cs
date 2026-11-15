using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    /// <summary>
    /// Patch-style update: every field is optional, only what is sent changes.
    /// MediaId is deliberately absent - moving an entry to another title would
    /// sidestep the unique (UserId, MediaId) constraint.
    /// </summary>
    public class UpdateLibraryDto
    {
        [StringLength(20)]
        [RegularExpression("^(Watching|Completed|Planned|OnHold|Dropped)$")]
        public string? Status { get; set; }

        [Range(1, 10)]
        public int? Score { get; set; }

        [Range(0, 10000)]
        public int? EpisodesWatched { get; set; }

        [Range(0, 999)]
        public int? RewatchCount { get; set; }

        public bool? IsFavorite { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Clears Score when true, since null means "leave unchanged".</summary>
        public bool ClearScore { get; set; }
    }
}
