using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class CreateMediaDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Range(1888, 2100)]
        public int ReleaseYear { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression("^(Anime|Movie|Series)$")]
        public string Type { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Url]
        public string PosterUrl { get; set; } = string.Empty;

        [StringLength(500)]
        public string BackdropUrl { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression("^(Airing|Finished|Upcoming)$")]
        public string AiringStatus { get; set; } = "Finished";

        [Range(1, 10000)]
        public int? TotalEpisodes { get; set; }

        [Range(1, 1000)]
        public int? EpisodeDuration { get; set; }

        [StringLength(120)]
        public string Studio { get; set; } = string.Empty;

        public List<int> GenreIds { get; set; } = new();
    }
}
