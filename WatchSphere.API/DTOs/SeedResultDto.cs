namespace WatchSphere.API.DTOs
{
    /// <summary>What a catalogue fill actually did, title by title.</summary>
    public class SeedResultDto
    {
        public string Type { get; set; } = string.Empty;

        public string? Provider { get; set; }

        public int Imported { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }

        public List<string> Titles { get; set; } = new();

        public string? Message { get; set; }
    }
}
