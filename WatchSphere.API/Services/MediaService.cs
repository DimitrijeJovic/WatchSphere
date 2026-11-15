using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.DTOs;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Models;

namespace WatchSphere.API.Services
{
    public class MediaService : IMediaService
    {
        private readonly AppDbContext _context;

        public MediaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Media> CreateAsync(CreateMediaDto dto)
        {
            var media = new Media();

            Map(dto, media);

            await SyncGenresAsync(media, dto.GenreIds);

            _context.Media.Add(media);

            await _context.SaveChangesAsync();

            return media;
        }

        public async Task<Media?> UpdateAsync(int id, CreateMediaDto dto)
        {
            var media = await _context.Media
                .Include(m => m.MediaGenres)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (media == null)
            {
                return null;
            }

            Map(dto, media);

            media.MediaGenres.Clear();

            await SyncGenresAsync(media, dto.GenreIds);

            await _context.SaveChangesAsync();

            return media;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var media = await _context.Media.FindAsync(id);

            if (media == null)
            {
                return false;
            }

            _context.Media.Remove(media);

            await _context.SaveChangesAsync();

            return true;
        }

        private static void Map(CreateMediaDto dto, Media media)
        {
            media.Title = dto.Title;
            media.Description = dto.Description;
            media.ReleaseYear = dto.ReleaseYear;
            media.Type = dto.Type;
            media.PosterUrl = dto.PosterUrl;
            media.BackdropUrl = dto.BackdropUrl;
            media.AiringStatus = dto.AiringStatus;
            media.EpisodeDuration = dto.EpisodeDuration;
            media.Studio = dto.Studio;

            // A movie is always a single entry, whatever the form sent.
            media.TotalEpisodes = dto.Type == "Movie" ? 1 : dto.TotalEpisodes;
        }

        private async Task SyncGenresAsync(Media media, List<int> genreIds)
        {
            if (genreIds.Count == 0)
            {
                return;
            }

            var validIds = await _context.Genres
                .Where(g => genreIds.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync();

            foreach (var genreId in validIds)
            {
                media.MediaGenres.Add(new MediaGenre { GenreId = genreId });
            }
        }
    }
}
