namespace WatchSphere.API.DTOs
{
    public class FranchiseResultDto
    {
        public int MediaId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Imported { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }

        public List<string> Titles { get; set; } = new();

        public string? Message { get; set; }
    }
}
