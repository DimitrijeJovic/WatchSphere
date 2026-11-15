using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class AddToLibraryDto
    {
        [Range(1, int.MaxValue)]
        public int MediaId { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression("^(Watching|Completed|Planned|OnHold|Dropped)$")]
        public string Status { get; set; } = "Planned";

        public bool IsFavorite { get; set; }
    }
}
