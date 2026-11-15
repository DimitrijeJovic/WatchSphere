namespace WatchSphere.API.Models
{
    public class Review
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int MediaId { get; set; }

        public int Rating { get; set; }

        public string ReviewText { get; set; } = string.Empty;

        /// <summary>Hides the body behind a reveal button on the client.</summary>
        public bool ContainsSpoilers { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public User User { get; set; } = null!;

        public Media Media { get; set; } = null!;

        public ICollection<ReviewComment> Comments { get; set; } = new List<ReviewComment>();

        public ICollection<ReviewVote> Votes { get; set; } = new List<ReviewVote>();
    }
}
