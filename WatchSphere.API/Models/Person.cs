namespace WatchSphere.API.Models
{
    /// <summary>
    /// Someone who worked on a title: an actor, a voice actor, a director.
    /// One row per person, however many titles they appear in.
    /// </summary>
    public class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string PhotoUrl { get; set; } = string.Empty;

        public int? TmdbId { get; set; }

        public int? TvMazeId { get; set; }

        public int? KitsuId { get; set; }

        public ICollection<MediaCredit> Credits { get; set; } = new List<MediaCredit>();
    }
}
