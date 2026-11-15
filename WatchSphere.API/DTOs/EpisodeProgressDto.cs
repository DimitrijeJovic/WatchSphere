namespace WatchSphere.API.DTOs
{
    /// <summary>
    /// What the client needs to redraw after a tick: the episode itself, and
    /// the title-level counters that moved with it.
    /// </summary>
    public class EpisodeProgressDto
    {
        public int EpisodeId { get; set; }

        public bool Watched { get; set; }

        public int? MyRating { get; set; }

        public int MediaId { get; set; }

        public int SeasonId { get; set; }

        public int SeasonWatchedCount { get; set; }

        /// <summary>Null when the title is not in the caller's library.</summary>
        public int? EpisodesWatched { get; set; }

        public int? TotalEpisodes { get; set; }

        public int ProgressPercent { get; set; }

        public string? Status { get; set; }
    }
}
