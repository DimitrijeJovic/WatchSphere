namespace WatchSphere.API.DTOs
{
    /// <summary>
    /// A title as one of the external catalogues describes it, flattened into
    /// the shape this application stores. Every provider maps into this, so the
    /// import service and the admin screen never learn provider specifics.
    /// </summary>
    public class ExternalTitleDto
    {
        /// <summary>Which catalogue this came from: tmdb or kitsu.</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>The provider's own id, as it appears in their URLs.</summary>
        public string ExternalId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string OriginalTitle { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>Anime, Movie or Series.</summary>
        public string Type { get; set; } = string.Empty;

        public int ReleaseYear { get; set; }

        public string PosterUrl { get; set; } = string.Empty;

        public string BackdropUrl { get; set; } = string.Empty;

        public string AiringStatus { get; set; } = "Finished";

        public int? TotalEpisodes { get; set; }

        public int? EpisodeDuration { get; set; }

        public string Studio { get; set; } = string.Empty;

        public string TrailerUrl { get; set; } = string.Empty;

        /// <summary>The source's own score, rescaled to ten points.</summary>
        public double? ExternalRating { get; set; }

        public int? ExternalVotes { get; set; }

        public List<string> Genres { get; set; } = new();

        public int? TmdbId { get; set; }

        public int? KitsuId { get; set; }

        public int? MalId { get; set; }

        public int? AniListId { get; set; }

        public int? TvMazeId { get; set; }

        public string? ImdbId { get; set; }

        /// <summary>Filled in by the import service so the admin list can show
        /// which results are already in the catalogue.</summary>
        public int? ExistingMediaId { get; set; }
    }
}
