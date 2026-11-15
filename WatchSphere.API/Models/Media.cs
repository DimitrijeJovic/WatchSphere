namespace WatchSphere.API.Models
{
    public class Media
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int ReleaseYear { get; set; }

        /// <summary>Anime, Movie or Series.</summary>
        public string Type { get; set; } = string.Empty;

        public string PosterUrl { get; set; } = string.Empty;

        /// <summary>Wide 16:9 image used behind the hero banner.</summary>
        public string BackdropUrl { get; set; } = string.Empty;

        /// <summary>Airing, Finished or Upcoming.</summary>
        public string AiringStatus { get; set; } = "Finished";

        /// <summary>
        /// Total episodes for a series or anime. Null when the count is not
        /// announced yet; movies use 1.
        /// </summary>
        public int? TotalEpisodes { get; set; }

        /// <summary>Runtime of a single episode in minutes.</summary>
        public int? EpisodeDuration { get; set; }

        public string Studio { get; set; } = string.Empty;

        /// <summary>Title in its own language, when it differs from Title.</summary>
        public string OriginalTitle { get; set; } = string.Empty;

        public string TrailerUrl { get; set; } = string.Empty;

        // --- external catalogue links -------------------------------------
        // Nullable and uniquely indexed: MySQL allows any number of NULLs in a
        // unique index, so titles added by hand simply carry none of these.

        public int? TmdbId { get; set; }

        public int? KitsuId { get; set; }

        public int? MalId { get; set; }

        public int? AniListId { get; set; }

        public int? TvMazeId { get; set; }

        public string? ImdbId { get; set; }

        /// <summary>The provider this record was imported from, if any.</summary>
        public string? Source { get; set; }

        /// <summary>Score the source site reports, normalised to ten points.</summary>
        public double? ExternalRating { get; set; }

        public int? ExternalVotes { get; set; }

        public DateTime? LastSyncedAt { get; set; }

        // --- IMDb scores ---------------------------------------------------
        // IMDb publishes no API for these, only a daily dataset dump, so they
        // are filled in by a separate job rather than arriving with an import.

        public double? ImdbRating { get; set; }

        public int? ImdbVotes { get; set; }

        public DateTime? ImdbRatingUpdatedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MediaGenre> MediaGenres { get; set; } = new List<MediaGenre>();

        public ICollection<UserLibrary> UserLibraries { get; set; } = new List<UserLibrary>();

        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<Season> Seasons { get; set; } = new List<Season>();

        public ICollection<Episode> Episodes { get; set; } = new List<Episode>();

        /// <summary>Other titles in the same franchise, from this one outward.</summary>
        public ICollection<MediaRelation> Relations { get; set; } = new List<MediaRelation>();

        public ICollection<MediaRelation> RelatedFrom { get; set; } = new List<MediaRelation>();

        public ICollection<MediaCredit> Credits { get; set; } = new List<MediaCredit>();

        public ICollection<MediaStudio> MediaStudios { get; set; } = new List<MediaStudio>();
    }
}
