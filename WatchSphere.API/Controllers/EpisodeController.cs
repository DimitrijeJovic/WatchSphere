using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Extensions;
using WatchSphere.API.Interfaces;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EpisodeController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IEpisodeProgressService _progress;

        public EpisodeController(AppDbContext context, IEpisodeProgressService progress)
        {
            _context = context;
            _progress = progress;
        }

        /// <summary>
        /// Every season of a title with its episodes. Public, but a signed-in
        /// caller also gets which ones they have watched and how they scored
        /// them, so the list renders in one request.
        /// </summary>
        [HttpGet("media/{mediaId:int}")]
        public async Task<IActionResult> GetForMedia(int mediaId)
        {
            var userId = User.GetUserId();

            var seasons = await _context.Seasons
                .AsNoTracking()
                .Where(s => s.MediaId == mediaId)
                .OrderBy(s => s.SeasonNumber)
                .Select(s => new
                {
                    s.Id,
                    s.SeasonNumber,
                    s.Name,
                    s.Overview,
                    s.AirDate,
                    s.PosterUrl,

                    EpisodeCount = s.Episodes.Count,

                    WatchedCount = userId == null
                        ? 0
                        : s.Episodes.Count(e => e.UserEpisodes.Any(ue => ue.UserId == userId.Value)),

                    Episodes = s.Episodes
                        .OrderBy(e => e.EpisodeNumber)
                        .Select(e => new
                        {
                            e.Id,
                            e.EpisodeNumber,
                            e.AbsoluteNumber,
                            e.Name,
                            e.Overview,
                            e.AirDate,
                            e.Runtime,
                            e.StillUrl,
                            e.ExternalRating,

                            Watched = userId != null
                                      && e.UserEpisodes.Any(ue => ue.UserId == userId.Value),

                            MyRating = userId == null
                                ? null
                                : e.UserEpisodes
                                    .Where(ue => ue.UserId == userId.Value)
                                    .Select(ue => ue.Rating)
                                    .FirstOrDefault()
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(seasons);
        }

        /// <summary>
        /// Marks one episode watched or not, and moves the title's progress
        /// counter to match.
        /// </summary>
        [Authorize]
        [HttpPut("{episodeId:int}/watched")]
        public async Task<IActionResult> SetWatched(
            int episodeId,
            [FromBody] SetEpisodeWatchedDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _progress.SetWatchedAsync(
                userId.Value, episodeId, dto.Watched, dto.Rating, dto.ClearRating);

            if (result == null)
            {
                return NotFound(new { message = "Episode not found." });
            }

            return Ok(result);
        }

        /// <summary>
        /// Ticks off everything up to and including one episode, which is how
        /// people catch a list up after watching several in a sitting.
        /// </summary>
        [Authorize]
        [HttpPost("{episodeId:int}/watched-through")]
        public async Task<IActionResult> WatchedThrough(int episodeId)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _progress.MarkWatchedThroughAsync(userId.Value, episodeId);

            if (result == null)
            {
                return NotFound(new { message = "Episode not found." });
            }

            return Ok(result);
        }

        /// <summary>Clears every tick for one season.</summary>
        [Authorize]
        [HttpDelete("season/{seasonId:int}/watched")]
        public async Task<IActionResult> ClearSeason(int seasonId)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _progress.ClearSeasonAsync(userId.Value, seasonId);

            if (result == null)
            {
                return NotFound(new { message = "Season not found." });
            }

            return Ok(result);
        }
    }
}
