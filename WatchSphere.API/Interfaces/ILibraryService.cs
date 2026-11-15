using WatchSphere.API.DTOs;
using WatchSphere.API.Models;

namespace WatchSphere.API.Interfaces
{
    /// <summary>
    /// Owns the rules that tie watch progress and status together, so the
    /// controller only maps HTTP to intent.
    /// </summary>
    public interface ILibraryService
    {
        Task<UserLibrary> AddAsync(int userId, AddToLibraryDto dto);

        Task<UserLibrary?> UpdateAsync(int userId, int entryId, UpdateLibraryDto dto);

        /// <summary>Bumps progress by one episode, used by the "+1" button.</summary>
        Task<UserLibrary?> IncrementEpisodeAsync(int userId, int entryId);

        Task<bool> RemoveAsync(int userId, int entryId);
    }
}
