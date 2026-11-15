namespace WatchSphere.API.DTOs
{
    /// <summary>
    /// The card shape every list endpoint returns, so the rails and grids on
    /// the client can all render through one component.
    /// </summary>
    public class MediaSummaryDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public int ReleaseYear { get; set; }

        public string PosterUrl { get; set; } = string.Empty;

        public string BackdropUrl { get; set; } = string.Empty;

        public string AiringStatus { get; set; } = string.Empty;

        public int? TotalEpisodes { get; set; }

        public string Description { get; set; } = string.Empty;

        public List<string> Genres { get; set; } = new();

        public int ReviewsCount { get; set; }

        public int MembersCount { get; set; }

        public double AverageRating { get; set; }
    }
}
