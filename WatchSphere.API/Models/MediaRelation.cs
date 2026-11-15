namespace WatchSphere.API.Models
{
    /// <summary>
    /// A link between two titles in the same franchise: a sequel, a prequel, a
    /// side story, or simply another entry in the same film series.
    /// </summary>
    public class MediaRelation
    {
        public int MediaId { get; set; }

        public int RelatedMediaId { get; set; }

        /// <summary>
        /// Sequel, Prequel, SideStory, AlternativeVersion, Spinoff, Summary,
        /// Adaptation, Collection or Other.
        /// </summary>
        public string RelationType { get; set; } = "Other";

        /// <summary>Orders entries within a film series by release.</summary>
        public int SortOrder { get; set; }

        public Media Media { get; set; } = null!;

        public Media RelatedMedia { get; set; } = null!;
    }
}
