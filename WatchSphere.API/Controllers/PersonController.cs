using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PersonController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// One person and everything in the catalogue they worked on, newest
        /// first, with the part they played on each.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var person = await _context.People
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.PhotoUrl,
                    p.TmdbId,

                    CreditCount = p.Credits.Count,

                    Titles = p.Credits
                        .OrderByDescending(c => c.Media.ReleaseYear)
                        .ThenBy(c => c.SortOrder)
                        .Select(c => new
                        {
                            MediaId = c.MediaId,
                            c.Media.Title,
                            c.Media.Type,
                            c.Media.ReleaseYear,
                            c.Media.PosterUrl,
                            c.Role,
                            c.CharacterName,
                            c.Language,
                            AverageRating = c.Media.Reviews.Any()
                                ? c.Media.Reviews.Average(r => r.Rating)
                                : 0
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (person == null)
            {
                return NotFound(new { message = "Person not found." });
            }

            return Ok(person);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(string query, int take = 12)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Ok(Array.Empty<object>());
            }

            var term = query.Trim();

            var people = await _context.People
                .AsNoTracking()
                .Where(p => p.Name.Contains(term))
                // Someone with fifty credits is far likelier to be the person
                // being looked for than a one-off bit part.
                .OrderByDescending(p => p.Credits.Count)
                .Take(Math.Clamp(take, 1, 50))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.PhotoUrl,
                    CreditCount = p.Credits.Count
                })
                .ToListAsync();

            return Ok(people);
        }
    }
}
