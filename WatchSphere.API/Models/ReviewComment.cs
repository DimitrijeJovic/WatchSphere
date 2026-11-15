namespace WatchSphere.API.Models
{
    public class ReviewComment
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }

        public int UserId { get; set; }

        public string Text { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Review Review { get; set; } = null!;

        public User User { get; set; } = null!;
    }
}
