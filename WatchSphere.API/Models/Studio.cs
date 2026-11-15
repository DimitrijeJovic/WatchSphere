namespace WatchSphere.API.Models
{
    /// <summary>A production company or animation studio.</summary>
    public class Studio
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string LogoUrl { get; set; } = string.Empty;

        public int? TmdbId { get; set; }

        public ICollection<MediaStudio> MediaStudios { get; set; } = new List<MediaStudio>();
    }
}
