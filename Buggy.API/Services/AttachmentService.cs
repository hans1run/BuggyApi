using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Services;

public class AttachmentService : IAttachmentService
{
    private readonly BuggyDbContext _db;
    private readonly IBlobStorageService _storage;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(BuggyDbContext db, IBlobStorageService storage, ILogger<AttachmentService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<List<AttachmentDto>> GetByItemAsync(Guid backlogItemId)
    {
        return await _db.Attachments
            .Where(a => a.BacklogItemId == backlogItemId)
            .OrderBy(a => a.CreatedDate)
            .Select(a => new AttachmentDto(a.Id, a.BacklogItemId, a.FileName, a.ContentType, a.FileSize, a.CreatedDate))
            .ToListAsync();
    }

    public async Task<AttachmentDto?> UploadAsync(Guid backlogItemId, string fileName, Stream stream, long size, string contentType)
    {
        var itemExists = await _db.BacklogItems.AnyAsync(b => b.Id == backlogItemId);
        if (!itemExists) return null;

        var blobName = await _storage.UploadAsync(fileName, stream, size, contentType);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            BacklogItemId = backlogItemId,
            FileName = fileName,
            BlobName = blobName,
            ContentType = contentType,
            FileSize = size
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Attachment {FileName} uploaded for item {ItemId}", fileName, backlogItemId);
        return new AttachmentDto(attachment.Id, attachment.BacklogItemId, attachment.FileName, attachment.ContentType, attachment.FileSize, attachment.CreatedDate);
    }

    public async Task<bool> DeleteAsync(Guid backlogItemId, Guid attachmentId)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.BacklogItemId == backlogItemId);
        if (attachment == null) return false;

        await _storage.DeleteAsync(attachment.BlobName);
        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetPresignedUrlAsync(Guid attachmentId)
    {
        var attachment = await _db.Attachments.FindAsync(attachmentId);
        if (attachment == null) return null;

        return await _storage.GetPresignedUrlAsync(attachment.BlobName);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(Guid attachmentId)
    {
        var attachment = await _db.Attachments.FindAsync(attachmentId);
        if (attachment == null) return null;

        var stream = await _storage.DownloadAsync(attachment.BlobName);
        return (stream, attachment.ContentType, attachment.FileName);
    }
}
