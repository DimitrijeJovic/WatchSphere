namespace WatchSphere.API.Models
{
    public class Genre
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<MediaGenre> MediaGenres { get; set; } = new List<MediaGenre>();

    }
}