namespace WatchSphere.API.DTOs
{
    /// <summary>
    /// A related title as a provider describes it, before it is known whether
    /// the catalogue holds it.
    /// </summary>
    public class ExternalRelationDto
    {
        public string RelationType { get; set; } = "Other";

        public string Provider { get; set; } = string.Empty;

        public string ExternalId { get; set; } = string.Empty;

        /// <summary>Anime, Movie or Series, so the importer knows what to ask for.</summary>
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int ReleaseYear { get; set; }

        public string PosterUrl { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }
}
