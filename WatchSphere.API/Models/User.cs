namespace WatchSphere.API.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public string AvatarUrl { get; set; } = string.Empty;

        public string Bio { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserLibrary> UserLibraries { get; set; } = new List<UserLibrary>();

        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<ReviewComment> ReviewComments { get; set; } = new List<ReviewComment>();

        public ICollection<ReviewVote> ReviewVotes { get; set; } = new List<ReviewVote>();

        public ICollection<UserEpisode> WatchedEpisodes { get; set; } = new List<UserEpisode>();
    }
}
