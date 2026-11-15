namespace WatchSphere.API.DTOs
{
    /// <summary>
    /// One episode as an external catalogue describes it. Providers number
    /// episodes differently, so both numbering schemes travel together and the
    /// import service fills in whichever the source left out.
    /// </summary>
    public class ExternalEpisodeDto
    {
        public int SeasonNumber { get; set; }

        public int EpisodeNumber { get; set; }

        /// <summary>Position across the whole run, zero when unknown.</summary>
        public int AbsoluteNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public DateOnly? AirDate { get; set; }

        public int? Runtime { get; set; }

        public string StillUrl { get; set; } = string.Empty;

        public double? ExternalRating { get; set; }
    }
}
