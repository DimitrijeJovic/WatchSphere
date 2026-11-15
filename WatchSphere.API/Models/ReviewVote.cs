namespace WatchSphere.API.Models
{
    /// <summary>One helpful / not-helpful vote per user per review.</summary>
    public class ReviewVote
    {
        public int ReviewId { get; set; }

        public int UserId { get; set; }

        public bool IsHelpful { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Review Review { get; set; } = null!;

        public User User { get; set; } = null!;
    }
}
