using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Models;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GenreController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GenreController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var genres = await _context.Genres
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    MediaCount = g.MediaGenres.Count
                })
                .ToListAsync();

            return Ok(genres);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(CreateGenreDto dto)
        {
            var name = dto.Name.Trim();

            if (await _context.Genres.AnyAsync(g => g.Name == name))
            {
                return Conflict(new { message = "That genre already exists." });
            }

            var genre = new Genre { Name = name };

            _context.Genres.Add(genre);

            await _context.SaveChangesAsync();

            return Ok(new { genre.Id, genre.Name });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var genre = await _context.Genres.FindAsync(id);

            if (genre == null)
            {
                return NotFound(new { message = "Genre not found." });
            }

            _context.Genres.Remove(genre);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
