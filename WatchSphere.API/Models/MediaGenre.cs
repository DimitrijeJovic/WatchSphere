namespace WatchSphere.API.Models
{
    public class MediaGenre
    {
        public int MediaId { get; set; }

        public int GenreId { get; set; }

        public Media Media { get; set; } = null!;

        public Genre Genre { get; set; } = null!;
    }
}