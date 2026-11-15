namespace WatchSphere.API.Models
{
    public class MediaStudio
    {
        public int MediaId { get; set; }

        public int StudioId { get; set; }

        public Media Media { get; set; } = null!;

        public Studio Studio { get; set; } = null!;
    }
}
