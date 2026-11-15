using WatchSphere.API.DTOs;
using WatchSphere.API.Models;

namespace WatchSphere.API.Interfaces
{
    public interface ICatalogImportService
    {
        /// <summary>Which providers exist and whether each one is usable.</summary>
        IReadOnlyList<(string Name, bool Configured, IReadOnlyCollection<string> Types)> Providers { get; }

        /// <summary>
        /// Searches one provider, or every provider that can supply the given
        /// type, and marks results that are already in the catalogue.
        /// </summary>
        Task<IReadOnlyList<ExternalTitleDto>> SearchAsync(
            string query,
            string? provider,
            string? type,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies a title into the catalogue, or refreshes it when it is
        /// already there. Genres arrive as names and are created as needed.
        /// </summary>
        Task<Media?> ImportAsync(
            string provider,
            string externalId,
            string? type,
            CancellationToken cancellationToken = default);

        /// <summary>Re-reads an already imported title from its source.</summary>
        Task<Media?> RefreshAsync(int mediaId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Re-reads only the cast, crew and studios. Separate from a full
        /// refresh because that also walks the episode list, which for a long
        /// running series is dozens of requests to learn nothing new.
        /// </summary>
        Task<int> RefreshCreditsAsync(int mediaId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fills the catalogue from whichever source ranks the given type,
        /// skipping anything already present.
        /// </summary>
        Task<SeedResultDto> SeedPopularAsync(
            string type,
            int count,
            string ranking,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Pulls in every other title the source links to this one - sequels,
        /// prequels, side stories, the rest of a film series - and records the
        /// links in both directions.
        /// </summary>
        Task<FranchiseResultDto> ImportFranchiseAsync(
            int mediaId,
            CancellationToken cancellationToken = default);
    }
}
