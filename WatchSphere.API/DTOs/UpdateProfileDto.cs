using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class UpdateProfileDto
    {
        [StringLength(500)]
        [Url]
        public string AvatarUrl { get; set; } = string.Empty;

        [StringLength(500)]
        public string Bio { get; set; } = string.Empty;
    }
}
