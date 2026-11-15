namespace WatchSphere.API.Models
{
    /// <summary>
    /// One person's involvement in one title.
    /// </summary>
    /// <remarks>
    /// The character lives here rather than in a table of its own. A character
    /// belongs to the performance, not to the person, and anime carries a
    /// separate voice actor per language - so the same character appears on
    /// several credits, which is exactly how it needs to be listed.
    /// </remarks>
    public class MediaCredit
    {
        public int Id { get; set; }

        public int MediaId { get; set; }

        public int PersonId { get; set; }

        /// <summary>Actor, VoiceActor, Director, Writer, Producer or Creator.</summary>
        public string Role { get; set; } = "Actor";

        public string CharacterName { get; set; } = string.Empty;

        public string CharacterImageUrl { get; set; } = string.Empty;

        /// <summary>Set for anime, where a character has a voice per language.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Billing order, so the leads come first.</summary>
        public int SortOrder { get; set; }

        public Media Media { get; set; } = null!;

        public Person Person { get; set; } = null!;
    }
}
