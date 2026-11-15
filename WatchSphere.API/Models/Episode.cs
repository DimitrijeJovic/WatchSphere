namespace WatchSphere.API.Models
{
    public class Episode
    {
        public int Id { get; set; }

        public int SeasonId { get; set; }

        /// <summary>
        /// Also held here so the episode list for a title is one query rather
        /// than a join through every season.
        /// </summary>
        public int MediaId { get; set; }

        public int EpisodeNumber { get; set; }

        /// <summary>
        /// Position counted across the whole run. Anime is usually numbered
        /// this way, and it is what the progress counter advances through.
        /// </summary>
        public int AbsoluteNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public DateOnly? AirDate { get; set; }

        public int? Runtime { get; set; }

        public string StillUrl { get; set; } = string.Empty;

        /// <summary>The source site's own score for this episode, out of ten.</summary>
        public double? ExternalRating { get; set; }

        public Season Season { get; set; } = null!;

        public Media Media { get; set; } = null!;

        public ICollection<UserEpisode> UserEpisodes { get; set; } = new List<UserEpisode>();
    }
}
