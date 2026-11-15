using WatchSphere.API.DTOs;
using WatchSphere.API.Models;

namespace WatchSphere.API.Interfaces
{
    public interface IMediaService
    {
        Task<Media> CreateAsync(CreateMediaDto dto);

        Task<Media?> UpdateAsync(int id, CreateMediaDto dto);

        Task<bool> DeleteAsync(int id);
    }
}
