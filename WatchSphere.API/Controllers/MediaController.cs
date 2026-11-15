using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Models;
using System.Linq.Expressions;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IMediaService _mediaService;

        public MediaController(AppDbContext context, IMediaService mediaService)
        {
            _context = context;
            _mediaService = mediaService;
        }

        /// <summary>
        /// The catalogue, filtered and paged. Search, type, genre and sort all
        /// compose, so the browse page can apply several at once.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            string? search = null,
            string? type = null,
            int? genreId = null,
            int? year = null,
            string? sortBy = null,
            int page = 1,
            int pageSize = 24)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Media.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                query = query.Where(m => m.Title.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(m => m.Type == type);
            }

            if (genreId.HasValue)
            {
                query = query.Where(m => m.MediaGenres.Any(mg => mg.GenreId == genreId.Value));
            }

            if (year.HasValue)
            {
                query = query.Where(m => m.ReleaseYear == year.Value);
            }

            // Ordering happens before projection so it runs in SQL rather than
            // pulling every row and its reviews into memory first.
            query = sortBy?.ToLowerInvariant() switch
            {
                "title" => query.OrderBy(m => m.Title),
                "year" => query.OrderByDescending(m => m.ReleaseYear),
                "rating" => query.OrderByDescending(
                        m => m.Reviews.Any() ? m.Reviews.Average(r => r.Rating) : 0)
                    .ThenByDescending(m => m.Reviews.Count),
                "popular" => query.OrderByDescending(m => m.UserLibraries.Count),
                _ => query.OrderByDescending(m => m.CreatedAt)
            };

            var totalItems = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(SummaryProjection)
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                Data = data
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var media = await _context.Media
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Description,
                    m.ReleaseYear,
                    m.Type,
                    m.PosterUrl,
                    m.BackdropUrl,
                    m.AiringStatus,
                    m.TotalEpisodes,
                    m.EpisodeDuration,
                    m.Studio,
                    m.TrailerUrl,
                    m.ImdbId,
                    m.Source,
                    m.CreatedAt,

                    // The source's own score and IMDb's, alongside this site's
                    // own average, so the three can be read together.
                    m.ExternalRating,
                    m.ExternalVotes,
                    m.ImdbRating,
                    m.ImdbVotes,

                    Genres = m.MediaGenres
                        .Select(mg => new { mg.Genre.Id, mg.Genre.Name })
                        .ToList(),

                    ReviewsCount = m.Reviews.Count,
                    AverageRating = m.Reviews.Any() ? m.Reviews.Average(r => r.Rating) : 0,
                    MembersCount = m.UserLibraries.Count,
                    FavoritesCount = m.UserLibraries.Count(ul => ul.IsFavorite),

                    // Ten buckets so the details page can draw the histogram
                    // without a second round trip.
                    RatingDistribution = m.Reviews
                        .GroupBy(r => r.Rating)
                        .Select(g => new { Rating = g.Key, Count = g.Count() })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (media == null)
            {
                return NotFound(new { message = "Media not found." });
            }

            return Ok(media);
        }

        /// <summary>
        /// Who made the title. Cast comes back in billing order; anime carries
        /// a voice actor per language, so those are grouped by character with
        /// the original Japanese performance first.
        /// </summary>
        [HttpGet("{id:int}/credits")]
        public async Task<IActionResult> GetCredits(int id)
        {
            var credits = await _context.MediaCredits
                .AsNoTracking()
                .Where(c => c.MediaId == id)
                .OrderBy(c => c.SortOrder)
                .Select(c => new
                {
                    c.Id,
                    c.Role,
                    c.CharacterName,
                    c.CharacterImageUrl,
                    c.Language,
                    c.SortOrder,
                    PersonId = c.PersonId,
                    PersonName = c.Person.Name,
                    PersonPhotoUrl = c.Person.PhotoUrl
                })
                .ToListAsync();

            var studios = await _context.MediaStudios
                .AsNoTracking()
                .Where(ms => ms.MediaId == id)
                .Select(ms => new { ms.Studio.Id, ms.Studio.Name, ms.Studio.LogoUrl })
                .ToListAsync();

            var cast = credits
                .Where(c => c.Role is "Actor" or "VoiceActor")
                .ToList();

            var crew = credits
                .Where(c => c.Role is not ("Actor" or "VoiceActor"))
                .ToList();

            return Ok(new
            {
                Cast = cast,
                Crew = crew,
                Studios = studios
            });
        }

        /// <summary>
        /// Other titles in the same franchise, ordered so a film series reads
        /// in release order and everything else groups by kind of link.
        /// </summary>
        [HttpGet("{id:int}/related")]
        public async Task<IActionResult> GetRelated(int id)
        {
            var related = await _context.MediaRelations
                .AsNoTracking()
                .Where(r => r.MediaId == id)
                .OrderBy(r => r.RelationType)
                .ThenBy(r => r.SortOrder)
                .ThenBy(r => r.RelatedMedia.ReleaseYear)
                .Select(r => new
                {
                    r.RelationType,
                    Id = r.RelatedMedia.Id,
                    r.RelatedMedia.Title,
                    r.RelatedMedia.Type,
                    r.RelatedMedia.ReleaseYear,
                    r.RelatedMedia.PosterUrl,
                    r.RelatedMedia.TotalEpisodes,
                    r.RelatedMedia.AiringStatus,
                    AverageRating = r.RelatedMedia.Reviews.Any()
                        ? r.RelatedMedia.Reviews.Average(review => review.Rating)
                        : 0
                })
                .ToListAsync();

            return Ok(related);
        }

        /// <summary>Newest additions, for the home page rail.</summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent(int take = 12)
        {
            var media = await _context.Media
                .AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .Take(Math.Clamp(take, 1, 50))
                .Select(SummaryProjection)
                .ToListAsync();

            return Ok(media);
        }

        /// <summary>
        /// Best rated titles. Requires at least one review so a single
        /// ten-out-of-ten does not outrank a title with a hundred reviews.
        /// </summary>
        [HttpGet("top-rated")]
        public async Task<IActionResult> GetTopRated(int take = 12)
        {
            var media = await _context.Media
                .AsNoTracking()
                .Where(m => m.Reviews.Any())
                .OrderByDescending(m => m.Reviews.Average(r => r.Rating))
                .ThenByDescending(m => m.Reviews.Count)
                .Take(Math.Clamp(take, 1, 50))
                .Select(SummaryProjection)
                .ToListAsync();

            return Ok(media);
        }

        /// <summary>Most listed titles - the closest thing to a trending rail.</summary>
        [HttpGet("popular")]
        public async Task<IActionResult> GetPopular(int take = 12)
        {
            var media = await _context.Media
                .AsNoTracking()
                .OrderByDescending(m => m.UserLibraries.Count)
                .ThenByDescending(m => m.Reviews.Count)
                .Take(Math.Clamp(take, 1, 50))
                .Select(SummaryProjection)
                .ToListAsync();

            return Ok(media);
        }

        /// <summary>Currently airing titles, used for the hero carousel.</summary>
        [HttpGet("airing")]
        public async Task<IActionResult> GetAiring(int take = 6)
        {
            var media = await _context.Media
                .AsNoTracking()
                .Where(m => m.AiringStatus == "Airing")
                .OrderByDescending(m => m.UserLibraries.Count)
                .Take(Math.Clamp(take, 1, 20))
                .Select(SummaryProjection)
                .ToListAsync();

            return Ok(media);
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            return Ok(new
            {
                TotalMedia = await _context.Media.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalReviews = await _context.Reviews.CountAsync(),
                TotalEpisodesTracked = await _context.UserLibraries.SumAsync(ul => ul.EpisodesWatched)
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(CreateMediaDto dto)
        {
            var media = await _mediaService.CreateAsync(dto);

            return CreatedAtAction(nameof(GetById), new { id = media.Id }, new { media.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, CreateMediaDto dto)
        {
            var media = await _mediaService.UpdateAsync(id, dto);

            if (media == null)
            {
                return NotFound(new { message = "Media not found." });
            }

            return Ok(new { media.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _mediaService.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound(new { message = "Media not found." });
            }

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{mediaId:int}/genre/{genreId:int}")]
        public async Task<IActionResult> AddGenreToMedia(int mediaId, int genreId)
        {
            if (!await _context.Media.AnyAsync(m => m.Id == mediaId))
            {
                return NotFound(new { message = "Media not found." });
            }

            if (!await _context.Genres.AnyAsync(g => g.Id == genreId))
            {
                return NotFound(new { message = "Genre not found." });
            }

            var exists = await _context.MediaGenres
                .AnyAsync(mg => mg.MediaId == mediaId && mg.GenreId == genreId);

            if (exists)
            {
                return Conflict(new { message = "Media already has that genre." });
            }

            _context.MediaGenres.Add(new MediaGenre { MediaId = mediaId, GenreId = genreId });

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{mediaId:int}/genre/{genreId:int}")]
        public async Task<IActionResult> RemoveGenreFromMedia(int mediaId, int genreId)
        {
            var link = await _context.MediaGenres
                .FirstOrDefaultAsync(mg => mg.MediaId == mediaId && mg.GenreId == genreId);

            if (link == null)
            {
                return NotFound(new { message = "Media does not have that genre." });
            }

            _context.MediaGenres.Remove(link);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Kept in one place so every list endpoint returns an identical card
        /// shape, and so the projection runs in SQL rather than in memory.
        /// </summary>
        private static readonly Expression<Func<Media, MediaSummaryDto>> SummaryProjection =
            m => new MediaSummaryDto
            {
                Id = m.Id,
                Title = m.Title,
                Type = m.Type,
                ReleaseYear = m.ReleaseYear,
                PosterUrl = m.PosterUrl,
                BackdropUrl = m.BackdropUrl,
                AiringStatus = m.AiringStatus,
                TotalEpisodes = m.TotalEpisodes,
                Description = m.Description,

                Genres = m.MediaGenres.Select(mg => mg.Genre.Name).ToList(),

                ReviewsCount = m.Reviews.Count,
                MembersCount = m.UserLibraries.Count,
                AverageRating = m.Reviews.Any() ? m.Reviews.Average(r => r.Rating) : 0
            };
    }
}
