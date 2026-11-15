using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class CreateReviewDto
    {
        [Range(1, 10)]
        public int Rating { get; set; }

        [Required]
        [StringLength(4000, MinimumLength = 3)]
        public string ReviewText { get; set; } = string.Empty;

        public bool ContainsSpoilers { get; set; }

        [Range(1, int.MaxValue)]
        public int MediaId { get; set; }
    }
}
