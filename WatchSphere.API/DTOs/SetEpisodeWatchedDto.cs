using System.ComponentModel.DataAnnotations;

namespace WatchSphere.API.DTOs
{
    public class SetEpisodeWatchedDto
    {
        public bool Watched { get; set; } = true;

        [Range(1, 10)]
        public int? Rating { get; set; }

        /// <summary>Removes an existing score, since null means unchanged.</summary>
        public bool ClearRating { get; set; }
    }
}
