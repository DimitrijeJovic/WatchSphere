using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class CreateGenreDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
