using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Controllers
{
    /// <summary>
    /// Pulls titles in from the external catalogues. Administrators only: this
    /// writes to the shared catalogue and spends someone else's rate limit.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    public class ImportController : ControllerBase
    {
        private readonly ICatalogImportService _import;

        private readonly IImdbRatingService _imdbRatings;

        public ImportController(ICatalogImportService import, IImdbRatingService imdbRatings)
        {
            _import = import;
            _imdbRatings = imdbRatings;
        }

        /// <summary>Which sources are available, and which need a key first.</summary>
        [HttpGet("providers")]
        public IActionResult GetProviders()
        {
            return Ok(_import.Providers.Select(p => new
            {
                Name = p.Name,
                Configured = p.Configured,
                Types = p.Types
            }));
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
            string query,
            string? provider = null,
            string? type = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return BadRequest(new { message = "Enter at least two characters to search." });
            }

            var results = await _import.SearchAsync(query, provider, type, cancellationToken);

            return Ok(results);
        }

        [HttpPost]
        public async Task<IActionResult> Import(
            string provider,
            string externalId,
            string? type = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var media = await _import.ImportAsync(provider, externalId, type, cancellationToken);

                if (media == null)
                {
                    return NotFound(new { message = "The provider does not have that title." });
                }

                return Ok(new { media.Id, media.Title, media.Type, media.PosterUrl });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Fills the catalogue with what a source currently ranks highest.
        /// Runs to completion before answering, so it is capped rather than
        /// left open ended - a television batch is one request per season.
        /// </summary>
        [HttpPost("popular")]
        public async Task<IActionResult> SeedPopular(
            string type,
            int count = 20,
            string ranking = "trending",
            CancellationToken cancellationToken = default)
        {
            var result = await _import.SeedPopularAsync(type, count, ranking, cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Follows a title's franchise: imports every sequel, prequel, side
        /// story and film-series entry the source knows about, and links them.
        /// </summary>
        [HttpPost("{mediaId:int}/franchise")]
        public async Task<IActionResult> ImportFranchise(
            int mediaId,
            CancellationToken cancellationToken = default)
        {
            var result = await _import.ImportFranchiseAsync(mediaId, cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Pulls IMDb scores in from their published dataset. Every title that
        /// carries an IMDb id is updated in one pass.
        /// </summary>
        [HttpPost("imdb-ratings")]
        public async Task<IActionResult> RefreshImdbRatings(CancellationToken cancellationToken = default)
        {
            var result = await _imdbRatings.RefreshAsync(cancellationToken);

            return Ok(result);
        }

        /// <summary>Re-reads just the cast, crew and studios for one title.</summary>
        [HttpPost("{mediaId:int}/credits")]
        public async Task<IActionResult> RefreshCredits(
            int mediaId,
            CancellationToken cancellationToken = default)
        {
            var count = await _import.RefreshCreditsAsync(mediaId, cancellationToken);

            return Ok(new { MediaId = mediaId, Credits = count });
        }

        /// <summary>Re-reads a title from the source it was imported from.</summary>
        [HttpPost("{mediaId:int}/refresh")]
        public async Task<IActionResult> Refresh(int mediaId, CancellationToken cancellationToken = default)
        {
            var media = await _import.RefreshAsync(mediaId, cancellationToken);

            if (media == null)
            {
                return NotFound(new
                {
                    message = "That title was not imported from a source that can be refreshed."
                });
            }

            return Ok(new { media.Id, media.Title, media.LastSyncedAt });
        }
    }
}
