namespace Buggy.API.Services;

public interface IBlobStorageService
{
    const int DefaultPresignedUrlExpirySeconds = 3600;

    Task<string> UploadAsync(string fileName, Stream stream, long size, string contentType);
    Task<Stream> DownloadAsync(string blobName);
    Task DeleteAsync(string blobName);
    Task<string> GetPresignedUrlAsync(string blobName, int expirySeconds = DefaultPresignedUrlExpirySeconds);
}
