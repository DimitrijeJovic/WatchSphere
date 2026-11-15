namespace WatchSphere.API.Interfaces
{
    public interface IImdbRatingService
    {
        /// <summary>
        /// Reads IMDb's public ratings dump and writes the scores onto the
        /// titles that carry an IMDb id.
        /// </summary>
        Task<ImdbRatingResult> RefreshAsync(CancellationToken cancellationToken = default);
    }

    public record ImdbRatingResult(int Matched, int Updated, int Known, string? Message = null);
}
