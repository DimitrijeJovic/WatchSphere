using WatchSphere.API.DTOs;

namespace WatchSphere.API.Interfaces
{
    /// <summary>
    /// An external catalogue this application can pull titles from.
    /// </summary>
    /// <remarks>
    /// There is more than one implementation for a practical reason rather than
    /// a theoretical one: anime metadata is served by several sites and they go
    /// down independently, so the import screen offers whichever is answering.
    /// </remarks>
    public interface ICatalogProvider
    {
        /// <summary>Short name used in the API and stored on imported rows.</summary>
        string Name { get; }

        /// <summary>Which of Anime, Movie and Series this provider can supply.</summary>
        IReadOnlyCollection<string> SupportedTypes { get; }

        /// <summary>False when the provider has no API key configured.</summary>
        bool IsConfigured { get; }

        Task<IReadOnlyList<ExternalTitleDto>> SearchAsync(
            string query,
            string? type,
            CancellationToken cancellationToken = default);

        /// <summary>The full record, which usually carries more than search returns.</summary>
        Task<ExternalTitleDto?> FetchAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Who made the title: cast, the crew roles worth naming, and the
        /// companies behind it.
        /// </summary>
        Task<ExternalCreditsDto> FetchCreditsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Other titles in the same franchise: sequels, prequels, side stories,
        /// or the rest of a film series. Empty where the source has no notion
        /// of one.
        /// </summary>
        Task<IReadOnlyList<ExternalRelationDto>> FetchRelationsAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// What the source considers popular right now, used to fill an empty
        /// catalogue. Returns an empty list where the provider has no such
        /// ranking to offer.
        /// </summary>
        /// <param name="ranking">
        /// "trending" for what is being watched this week, "top" for the
        /// highest rated of all time.
        /// </param>
        Task<IReadOnlyList<ExternalTitleDto>> FetchPopularAsync(
            string type,
            int count,
            string ranking,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Every episode of a title, in order. Returns an empty list for
        /// providers or titles that have no episode breakdown, which is the
        /// normal answer for a film.
        /// </summary>
        Task<IReadOnlyList<ExternalEpisodeDto>> FetchEpisodesAsync(
            string externalId,
            string? type,
            CancellationToken cancellationToken = default);
    }
}
