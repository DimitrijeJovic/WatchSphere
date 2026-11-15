using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Extensions;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Models;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UserLibraryController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly ILibraryService _library;

        public UserLibraryController(AppDbContext context, ILibraryService library)
        {
            _context = context;
            _library = library;
        }

        /// <summary>
        /// The whole list for the caller. Routing off the token instead of a
        /// {userId} in the URL means there is no other list to ask for.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyLibrary(string? status = null)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var query = _context.UserLibraries
                .AsNoTracking()
                .Where(ul => ul.UserId == userId.Value);

            if (status == "Favorites")
            {
                query = query.Where(ul => ul.IsFavorite);
            }
            else if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(ul => ul.Status == status);
            }

            var entries = await query
                .Include(ul => ul.Media)
                    .ThenInclude(m => m.MediaGenres)
                        .ThenInclude(mg => mg.Genre)
                .OrderByDescending(ul => ul.UpdatedAt)
                .ToListAsync();

            return Ok(entries.Select(Describe));
        }

        /// <summary>The entry for one title, or 204 when it is not listed.</summary>
        [HttpGet("me/media/{mediaId:int}")]
        public async Task<IActionResult> GetMyEntryFor(int mediaId)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var entry = await _context.UserLibraries
                .AsNoTracking()
                .Include(ul => ul.Media)
                    .ThenInclude(m => m.MediaGenres)
                        .ThenInclude(mg => mg.Genre)
                .FirstOrDefaultAsync(ul => ul.UserId == userId.Value && ul.MediaId == mediaId);

            if (entry == null)
            {
                return NoContent();
            }

            return Ok(Describe(entry));
        }

        [HttpGet("me/stats")]
        public async Task<IActionResult> GetMyStats()
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var rows = await _context.UserLibraries
                .AsNoTracking()
                .Where(ul => ul.UserId == userId.Value)
                .Select(ul => new
                {
                    ul.Status,
                    ul.Score,
                    ul.IsFavorite,
                    ul.EpisodesWatched,
                    ul.RewatchCount,
                    Duration = ul.Media.EpisodeDuration
                })
                .ToListAsync();

            var scored = rows.Where(r => r.Score.HasValue).ToList();

            var episodes = rows.Sum(r => r.EpisodesWatched * (1 + r.RewatchCount));

            return Ok(new
            {
                Total = rows.Count,
                Watching = rows.Count(r => r.Status == "Watching"),
                Completed = rows.Count(r => r.Status == "Completed"),
                Planned = rows.Count(r => r.Status == "Planned"),
                OnHold = rows.Count(r => r.Status == "OnHold"),
                Dropped = rows.Count(r => r.Status == "Dropped"),
                Favorites = rows.Count(r => r.IsFavorite),

                EpisodesWatched = episodes,

                // Falls back to 24 minutes when a title has no runtime set.
                MinutesWatched = rows.Sum(
                    r => r.EpisodesWatched * (1 + r.RewatchCount) * (r.Duration ?? 24)),

                MeanScore = scored.Count > 0
                    ? Math.Round(scored.Average(r => r.Score!.Value), 2)
                    : 0,

                ScoreDistribution = Enumerable.Range(1, 10)
                    .Select(score => new
                    {
                        Score = score,
                        Count = scored.Count(r => r.Score == score)
                    })
            });
        }

        [HttpPost]
        public async Task<IActionResult> Add(AddToLibraryDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            try
            {
                var entry = await _library.AddAsync(userId.Value, dto);

                return Ok(await ReloadAsync(entry.Id));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, UpdateLibraryDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var entry = await _library.UpdateAsync(userId.Value, id, dto);

            if (entry == null)
            {
                return NotFound(new { message = "Library entry not found." });
            }

            return Ok(await ReloadAsync(entry.Id));
        }

        /// <summary>Backs the +1 button next to a progress bar.</summary>
        [HttpPost("{id:int}/episode")]
        public async Task<IActionResult> IncrementEpisode(int id)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var entry = await _library.IncrementEpisodeAsync(userId.Value, id);

            if (entry == null)
            {
                return NotFound(new { message = "Library entry not found." });
            }

            return Ok(await ReloadAsync(entry.Id));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var removed = await _library.RemoveAsync(userId.Value, id);

            if (!removed)
            {
                return NotFound(new { message = "Library entry not found." });
            }

            return NoContent();
        }

        private async Task<object?> ReloadAsync(int entryId)
        {
            var entry = await _context.UserLibraries
                .AsNoTracking()
                .Include(ul => ul.Media)
                    .ThenInclude(m => m.MediaGenres)
                        .ThenInclude(mg => mg.Genre)
                .FirstOrDefaultAsync(ul => ul.Id == entryId);

            return entry == null ? null : Describe(entry);
        }

        private static object Describe(UserLibrary entry)
        {
            var total = entry.Media.Type == "Movie" ? 1 : entry.Media.TotalEpisodes;

            return new
            {
                entry.Id,
                entry.Status,
                entry.Score,
                entry.EpisodesWatched,
                entry.RewatchCount,
                entry.IsFavorite,
                entry.Notes,
                entry.StartedAt,
                entry.FinishedAt,
                entry.AddedAt,
                entry.UpdatedAt,

                MediaId = entry.Media.Id,
                entry.Media.Title,
                entry.Media.Type,
                entry.Media.ReleaseYear,
                entry.Media.PosterUrl,
                entry.Media.AiringStatus,
                entry.Media.EpisodeDuration,

                TotalEpisodes = total,

                ProgressPercent = total.HasValue && total.Value > 0
                    ? Math.Min(100, (int)Math.Round(entry.EpisodesWatched * 100.0 / total.Value))
                    : 0,

                Genres = entry.Media.MediaGenres.Select(mg => mg.Genre.Name).ToList()
            };
        }
    }
}
