using Buggy.API.DTOs;

namespace Buggy.API.Services;

public interface IAttachmentService
{
    Task<List<AttachmentDto>> GetByItemAsync(Guid backlogItemId);
    Task<AttachmentDto?> UploadAsync(Guid backlogItemId, string fileName, Stream stream, long size, string contentType);
    Task<bool> DeleteAsync(Guid backlogItemId, Guid attachmentId);
    Task<string?> GetPresignedUrlAsync(Guid attachmentId);
    Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(Guid attachmentId);
}
