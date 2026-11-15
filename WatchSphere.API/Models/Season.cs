namespace WatchSphere.API.Models
{
    public class Season
    {
        public int Id { get; set; }

        public int MediaId { get; set; }

        /// <summary>
        /// Season zero is the convention every catalogue uses for specials and
        /// OVAs, so it is stored as-is rather than filtered out.
        /// </summary>
        public int SeasonNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public DateOnly? AirDate { get; set; }

        public string PosterUrl { get; set; } = string.Empty;

        public Media Media { get; set; } = null!;

        public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
    }
}
