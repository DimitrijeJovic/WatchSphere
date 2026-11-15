using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Extensions;
using WatchSphere.API.Models;
using WatchSphere.API.Services;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly JwtService _jwtService;

        public AuthController(AppDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var username = dto.Username.Trim();

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Conflict(new { message = "An account with that email already exists." });
            }

            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                return Conflict(new { message = "That username is taken." });
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "User"
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            var (token, expiresAt) = _jwtService.GenerateToken(user);

            return Ok(new
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = Describe(user)
            });
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Same message either way, so the endpoint can't be used to find
            // out which emails are registered.
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Incorrect email or password." });
            }

            var (token, expiresAt) = _jwtService.GenerateToken(user);

            return Ok(new
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = Describe(user)
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            // Read through to the database so a changed role or avatar shows up
            // without waiting for the token to expire.
            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(Describe(user));
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return Unauthorized();
            }

            user.AvatarUrl = dto.AvatarUrl;
            user.Bio = dto.Bio;

            await _context.SaveChangesAsync();

            return Ok(Describe(user));
        }

        private static object Describe(User user)
        {
            return new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.AvatarUrl,
                user.Bio,
                user.CreatedAt
            };
        }

    }
}
