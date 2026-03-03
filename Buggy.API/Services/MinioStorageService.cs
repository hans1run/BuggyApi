using Minio;
using Minio.DataModel.Args;

namespace Buggy.API.Services;

public class MinioStorageService : IBlobStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IMinioClient minioClient, IConfiguration config, ILogger<MinioStorageService> logger)
    {
        _minioClient = minioClient;
        _bucketName = config["Storage:BucketName"] ?? "buggy-attachments";
        _logger = logger;
    }

    public async Task<string> UploadAsync(string fileName, Stream stream, long size, string contentType)
    {
        var objectName = $"{Guid.NewGuid()}-{fileName}";
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType));
        _logger.LogInformation("Uploaded {ObjectName} to {Bucket}", objectName, _bucketName);
        return objectName;
    }

    public async Task<Stream> DownloadAsync(string blobName)
    {
        var ms = new MemoryStream();
        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(blobName)
            .WithCallbackStream(stream => stream.CopyTo(ms)));
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string blobName)
    {
        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(blobName));
        _logger.LogInformation("Deleted {ObjectName} from {Bucket}", blobName, _bucketName);
    }

    public async Task<string> GetPresignedUrlAsync(string blobName, int expirySeconds = 3600)
    {
        return await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(blobName)
            .WithExpiry(expirySeconds));
    }
}
