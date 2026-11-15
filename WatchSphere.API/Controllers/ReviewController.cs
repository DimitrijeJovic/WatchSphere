using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Extensions;
using WatchSphere.API.Models;

namespace WatchSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReviewController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Reviews for one title, most helpful first. Public, but the caller's
        /// own vote is included when they are signed in.
        /// </summary>
        [HttpGet("media/{mediaId:int}")]
        public async Task<IActionResult> GetByMedia(int mediaId, string sortBy = "helpful")
        {
            var currentUserId = User.GetUserId();

            var query = _context.Reviews
                .AsNoTracking()
                .Where(r => r.MediaId == mediaId);

            query = sortBy == "recent"
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderByDescending(r => r.Votes.Count(v => v.IsHelpful))
                       .ThenByDescending(r => r.CreatedAt);

            var reviews = await query
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    Username = r.User.Username,
                    AvatarUrl = r.User.AvatarUrl,
                    r.Rating,
                    r.ReviewText,
                    r.ContainsSpoilers,
                    r.CreatedAt,
                    r.UpdatedAt,

                    HelpfulCount = r.Votes.Count(v => v.IsHelpful),
                    NotHelpfulCount = r.Votes.Count(v => !v.IsHelpful),
                    CommentsCount = r.Comments.Count,

                    MyVote = currentUserId == null
                        ? null
                        : r.Votes
                            .Where(v => v.UserId == currentUserId.Value)
                            .Select(v => (bool?)v.IsHelpful)
                            .FirstOrDefault(),

                    IsMine = currentUserId != null && r.UserId == currentUserId.Value
                })
                .ToListAsync();

            return Ok(reviews);
        }

        /// <summary>A user's public review history.</summary>
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.MediaId,
                    MediaTitle = r.Media.Title,
                    MediaPosterUrl = r.Media.PosterUrl,
                    r.Rating,
                    r.ReviewText,
                    r.ContainsSpoilers,
                    r.CreatedAt,
                    HelpfulCount = r.Votes.Count(v => v.IsHelpful)
                })
                .ToListAsync();

            return Ok(reviews);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(CreateReviewDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            if (!await _context.Media.AnyAsync(m => m.Id == dto.MediaId))
            {
                return NotFound(new { message = "Media not found." });
            }

            var exists = await _context.Reviews
                .AnyAsync(r => r.UserId == userId.Value && r.MediaId == dto.MediaId);

            if (exists)
            {
                return Conflict(new { message = "You already reviewed this title." });
            }

            var review = new Review
            {
                UserId = userId.Value,
                MediaId = dto.MediaId,
                Rating = dto.Rating,
                ReviewText = dto.ReviewText,
                ContainsSpoilers = dto.ContainsSpoilers
            };

            _context.Reviews.Add(review);

            await _context.SaveChangesAsync();

            return Ok(new { review.Id });
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, CreateReviewDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews.FindAsync(id);

            if (review == null)
            {
                return NotFound(new { message = "Review not found." });
            }

            if (review.UserId != userId.Value)
            {
                return Forbid();
            }

            review.Rating = dto.Rating;
            review.ReviewText = dto.ReviewText;
            review.ContainsSpoilers = dto.ContainsSpoilers;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { review.Id });
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews.FindAsync(id);

            if (review == null)
            {
                return NotFound(new { message = "Review not found." });
            }

            if (review.UserId != userId.Value && !User.IsAdmin())
            {
                return Forbid();
            }

            _context.Reviews.Remove(review);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Marks a review helpful or not. Sending the same value again clears
        /// the vote, so the button toggles.
        /// </summary>
        [Authorize]
        [HttpPost("{id:int}/vote")]
        public async Task<IActionResult> Vote(int id, bool helpful)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews.FindAsync(id);

            if (review == null)
            {
                return NotFound(new { message = "Review not found." });
            }

            if (review.UserId == userId.Value)
            {
                return BadRequest(new { message = "You cannot vote on your own review." });
            }

            var vote = await _context.ReviewVotes
                .FirstOrDefaultAsync(v => v.ReviewId == id && v.UserId == userId.Value);

            if (vote == null)
            {
                _context.ReviewVotes.Add(new ReviewVote
                {
                    ReviewId = id,
                    UserId = userId.Value,
                    IsHelpful = helpful
                });
            }
            else if (vote.IsHelpful == helpful)
            {
                _context.ReviewVotes.Remove(vote);
            }
            else
            {
                vote.IsHelpful = helpful;
            }

            await _context.SaveChangesAsync();

            var counts = await _context.ReviewVotes
                .Where(v => v.ReviewId == id)
                .GroupBy(v => v.IsHelpful)
                .Select(g => new { IsHelpful = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(new
            {
                HelpfulCount = counts.FirstOrDefault(c => c.IsHelpful)?.Count ?? 0,
                NotHelpfulCount = counts.FirstOrDefault(c => !c.IsHelpful)?.Count ?? 0,
                MyVote = await _context.ReviewVotes
                    .Where(v => v.ReviewId == id && v.UserId == userId.Value)
                    .Select(v => (bool?)v.IsHelpful)
                    .FirstOrDefaultAsync()
            });
        }

        [HttpGet("{id:int}/comments")]
        public async Task<IActionResult> GetComments(int id)
        {
            var currentUserId = User.GetUserId();

            var comments = await _context.ReviewComments
                .AsNoTracking()
                .Where(c => c.ReviewId == id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.UserId,
                    Username = c.User.Username,
                    AvatarUrl = c.User.AvatarUrl,
                    c.Text,
                    c.CreatedAt,
                    IsMine = currentUserId != null && c.UserId == currentUserId.Value
                })
                .ToListAsync();

            return Ok(comments);
        }

        [Authorize]
        [HttpPost("{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, CreateCommentDto dto)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            if (!await _context.Reviews.AnyAsync(r => r.Id == id))
            {
                return NotFound(new { message = "Review not found." });
            }

            var comment = new ReviewComment
            {
                ReviewId = id,
                UserId = userId.Value,
                Text = dto.Text.Trim()
            };

            _context.ReviewComments.Add(comment);

            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(userId.Value);

            return Ok(new
            {
                comment.Id,
                comment.UserId,
                Username = user!.Username,
                user.AvatarUrl,
                comment.Text,
                comment.CreatedAt,
                IsMine = true
            });
        }

        [Authorize]
        [HttpDelete("comments/{commentId:int}")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var userId = User.GetUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var comment = await _context.ReviewComments.FindAsync(commentId);

            if (comment == null)
            {
                return NotFound(new { message = "Comment not found." });
            }

            if (comment.UserId != userId.Value && !User.IsAdmin())
            {
                return Forbid();
            }

            _context.ReviewComments.Remove(comment);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>Moderation view - every review in the system, paged.</summary>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Reviews.AsNoTracking();

            var totalItems = await query.CountAsync();

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    Username = r.User.Username,
                    MediaTitle = r.Media.Title,
                    r.Rating,
                    r.ReviewText,
                    r.ContainsSpoilers,
                    r.CreatedAt,
                    CommentsCount = r.Comments.Count
                })
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                Data = reviews
            });
        }
    }
}
