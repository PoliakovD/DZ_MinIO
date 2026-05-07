using System.Reactive.Linq;
using DZ_MinIO.Models;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace DZ_MinIO.Services;

public class FileService : IFileService
{
    private readonly IMinioClient _minio;
    private readonly string _bucketName;
    private readonly ILogger<FileService> _logger;

    public FileService(IConfiguration config, ILogger<FileService> logger)
    {
        _logger = logger;
        var minioConfig = config.GetSection("Minio");
        _bucketName = minioConfig["BucketName"]!;

        _minio = new MinioClient()
            .WithEndpoint(minioConfig["Endpoint"])
            .WithCredentials(minioConfig["AccessKey"], minioConfig["SecretKey"])
            .WithSSL(bool.Parse(minioConfig["UseSsl"] ?? "false"))
            .Build();
    }

    public async Task EnsureBucketExistsAsync()
    {
        try
        {
            bool found = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName));
            if (!found)
            {
                await _minio.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName));
                _logger.LogInformation("Бакет {Bucket} создан автоматически", _bucketName);
            }
        }
        catch (MinioException ex)
        {
            _logger.LogError(ex, "Ошибка проверки/создания бакета");
            throw;
        }
    }

    public async Task UploadAsync(IFormFile file)
    {
        var fileName = Path.GetFileName(file.FileName);
        await using var stream = file.OpenReadStream();

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileName)
            .WithStreamData(stream)
            .WithObjectSize(file.Length)
            .WithContentType(file.ContentType);

        await _minio.PutObjectAsync(putArgs);
        _logger.LogInformation("Файл {FileName} загружен", fileName);
    }

    public async Task<Stream> DownloadAsync(string fileName)
    {
        var memoryStream = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
            });

        await _minio.GetObjectAsync(getArgs);
        return memoryStream;
    }

    public async Task<IEnumerable<FileItem>> ListAsync()
    {
        var items = new List<FileItem>();
        var listArgs = new ListObjectsArgs()
            .WithBucket(_bucketName)
            .WithRecursive(false);

         await foreach (var item in _minio.ListObjectsEnumAsync(listArgs))
        {
            items.Add(new FileItem
            {
                FileName = item.Key,
                Size = (long)(item.Size),
                LastModified = item.LastModifiedDateTime
            });
        }

        return items;
    }

    public async Task DeleteAsync(string fileName)
    {
        var removeArgs = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileName);

        await _minio.RemoveObjectAsync(removeArgs);
        _logger.LogInformation("Файл {FileName} удалён", fileName);
    }
}