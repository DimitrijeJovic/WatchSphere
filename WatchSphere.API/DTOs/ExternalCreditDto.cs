namespace WatchSphere.API.DTOs
{
    /// <summary>One credit as a provider describes it.</summary>
    public class ExternalCreditDto
    {
        /// <summary>Actor, VoiceActor, Director, Writer, Producer or Creator.</summary>
        public string Role { get; set; } = "Actor";

        public string Provider { get; set; } = string.Empty;

        /// <summary>The provider's id for the person, where it has one.</summary>
        public string? PersonExternalId { get; set; }

        public string PersonName { get; set; } = string.Empty;

        public string PersonPhotoUrl { get; set; } = string.Empty;

        public string CharacterName { get; set; } = string.Empty;

        public string CharacterImageUrl { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }

    public class ExternalStudioDto
    {
        public string Name { get; set; } = string.Empty;

        public string LogoUrl { get; set; } = string.Empty;

        public int? TmdbId { get; set; }
    }

    /// <summary>Everything a provider knows about who made a title.</summary>
    public record ExternalCreditsDto(
        IReadOnlyList<ExternalCreditDto> Credits,
        IReadOnlyList<ExternalStudioDto> Studios)
    {
        public static readonly ExternalCreditsDto Empty =
            new(Array.Empty<ExternalCreditDto>(), Array.Empty<ExternalStudioDto>());
    }
}
