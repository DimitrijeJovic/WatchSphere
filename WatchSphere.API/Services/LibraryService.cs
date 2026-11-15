using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Models;

namespace WatchSphere.API.Services
{
    public class LibraryService : ILibraryService
    {
        private readonly AppDbContext _context;

        public LibraryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserLibrary> AddAsync(int userId, AddToLibraryDto dto)
        {
            var media = await _context.Media.FindAsync(dto.MediaId)
                ?? throw new KeyNotFoundException("Media not found.");

            var exists = await _context.UserLibraries
                .AnyAsync(ul => ul.UserId == userId && ul.MediaId == dto.MediaId);

            if (exists)
            {
                throw new InvalidOperationException("Media is already in your library.");
            }

            var entry = new UserLibrary
            {
                UserId = userId,
                MediaId = dto.MediaId,
                Status = dto.Status,
                IsFavorite = dto.IsFavorite
            };

            ApplyStatusSideEffects(entry, media, dto.Status);

            _context.UserLibraries.Add(entry);

            await _context.SaveChangesAsync();

            return entry;
        }

        public async Task<UserLibrary?> UpdateAsync(int userId, int entryId, UpdateLibraryDto dto)
        {
            var entry = await LoadOwnedAsync(userId, entryId);

            if (entry == null)
            {
                return null;
            }

            if (dto.EpisodesWatched.HasValue)
            {
                SetProgress(entry, entry.Media, dto.EpisodesWatched.Value);
            }

            // An explicit status wins over whatever the progress change implied.
            if (!string.IsNullOrEmpty(dto.Status) && dto.Status != entry.Status)
            {
                ApplyStatusSideEffects(entry, entry.Media, dto.Status);
            }

            if (dto.ClearScore)
            {
                entry.Score = null;
            }
            else if (dto.Score.HasValue)
            {
                entry.Score = dto.Score.Value;
            }

            if (dto.RewatchCount.HasValue)
            {
                entry.RewatchCount = dto.RewatchCount.Value;
            }

            if (dto.IsFavorite.HasValue)
            {
                entry.IsFavorite = dto.IsFavorite.Value;
            }

            if (dto.Notes != null)
            {
                entry.Notes = dto.Notes;
            }

            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return entry;
        }

        public async Task<UserLibrary?> IncrementEpisodeAsync(int userId, int entryId)
        {
            var entry = await LoadOwnedAsync(userId, entryId);

            if (entry == null)
            {
                return null;
            }

            SetProgress(entry, entry.Media, entry.EpisodesWatched + 1);

            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return entry;
        }

        public async Task<bool> RemoveAsync(int userId, int entryId)
        {
            var entry = await LoadOwnedAsync(userId, entryId);

            if (entry == null)
            {
                return false;
            }

            _context.UserLibraries.Remove(entry);

            await _context.SaveChangesAsync();

            return true;
        }

        private Task<UserLibrary?> LoadOwnedAsync(int userId, int entryId)
        {
            return _context.UserLibraries
                .Include(ul => ul.Media)
                .FirstOrDefaultAsync(ul => ul.Id == entryId && ul.UserId == userId);
        }

        /// <summary>
        /// Moves progress to <paramref name="episodes"/>, clamped to what the
        /// title actually has, and lets the status follow: starting a Planned
        /// title makes it Watching, reaching the last episode completes it.
        /// </summary>
        private static void SetProgress(UserLibrary entry, Media media, int episodes)
        {
            var total = EffectiveTotal(media);

            episodes = Math.Max(0, episodes);

            if (total.HasValue)
            {
                episodes = Math.Min(episodes, total.Value);
            }

            entry.EpisodesWatched = episodes;

            if (episodes > 0 && entry.StartedAt == null)
            {
                entry.StartedAt = DateTime.UtcNow;
            }

            if (total.HasValue && episodes == total.Value)
            {
                entry.Status = "Completed";
                entry.FinishedAt ??= DateTime.UtcNow;
            }
            else if (episodes > 0 && entry.Status is "Planned" or "Completed")
            {
                entry.Status = "Watching";
                entry.FinishedAt = null;
            }
        }

        private static void ApplyStatusSideEffects(UserLibrary entry, Media media, string status)
        {
            entry.Status = status;

            var total = EffectiveTotal(media);

            switch (status)
            {
                case "Completed":
                    if (total.HasValue)
                    {
                        entry.EpisodesWatched = total.Value;
                    }

                    entry.StartedAt ??= DateTime.UtcNow;
                    entry.FinishedAt ??= DateTime.UtcNow;
                    break;

                case "Watching":
                    entry.StartedAt ??= DateTime.UtcNow;
                    entry.FinishedAt = null;
                    break;

                case "Planned":
                    entry.EpisodesWatched = 0;
                    entry.StartedAt = null;
                    entry.FinishedAt = null;
                    break;

                // OnHold and Dropped keep whatever progress the user had.
            }
        }

        /// <summary>A movie is one "episode" so progress works the same way.</summary>
        private static int? EffectiveTotal(Media media)
        {
            if (media.Type == "Movie")
            {
                return 1;
            }

            return media.TotalEpisodes;
        }
    }
}
