using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.Extensions;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// A public profile: the parts of a user other members are meant to
        /// see. Email and role stay out of it.
        /// </summary>
        [HttpGet("{id:int}/profile")]
        public async Task<IActionResult> GetPublicProfile(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.AvatarUrl,
                    u.Bio,
                    u.CreatedAt,

                    ReviewsCount = u.Reviews.Count,
                    LibraryCount = u.UserLibraries.Count,
                    CompletedCount = u.UserLibraries.Count(ul => ul.Status == "Completed"),
                    EpisodesWatched = u.UserLibraries.Sum(ul => ul.EpisodesWatched),

                    Favorites = u.UserLibraries
                        .Where(ul => ul.IsFavorite)
                        .OrderByDescending(ul => ul.UpdatedAt)
                        .Take(12)
                        .Select(ul => new
                        {
                            MediaId = ul.MediaId,
                            ul.Media.Title,
                            ul.Media.PosterUrl,
                            ul.Media.Type,
                            ul.Media.ReleaseYear
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(user);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll(string? search = null)
        {
            var query = _context.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                query = query.Where(u => u.Username.Contains(term) || u.Email.Contains(term));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.AvatarUrl,
                    u.CreatedAt,
                    ReviewsCount = u.Reviews.Count,
                    LibraryCount = u.UserLibraries.Count
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] string role)
        {
            if (role is not ("User" or "Admin"))
            {
                return BadRequest(new { message = "Role must be User or Admin." });
            }

            var callerId = User.GetUserId();

            // Without this an admin can demote themselves and lock the last
            // account out of the dashboard.
            if (callerId == id)
            {
                return BadRequest(new { message = "You cannot change your own role." });
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.Role = role;

            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Role });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var callerId = User.GetUserId();

            if (callerId == id)
            {
                return BadRequest(new { message = "You cannot delete your own account here." });
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            _context.Users.Remove(user);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
