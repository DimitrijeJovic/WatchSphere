using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class CreateCommentDto
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Text { get; set; } = string.Empty;
    }
}
