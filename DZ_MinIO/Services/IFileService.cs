using DZ_MinIO.Models;

namespace DZ_MinIO.Services;

public interface IFileService
{
    Task UploadAsync(IFormFile file);
    Task<Stream> DownloadAsync(string fileName);
    Task<IEnumerable<FileItem>> ListAsync();
    Task DeleteAsync(string fileName);
    Task EnsureBucketExistsAsync();
}