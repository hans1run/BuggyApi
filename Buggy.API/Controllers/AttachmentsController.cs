using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Controllers;

[ApiController]
[Authorize(Policy = "SingleUser")]
public class AttachmentsController : ControllerBase
{
    private readonly BuggyDbContext _db;
    private readonly IBlobStorageService _storage;
    private readonly ILogger<AttachmentsController> _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public AttachmentsController(BuggyDbContext db, IBlobStorageService storage, ILogger<AttachmentsController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet("api/items/{itemId:guid}/attachments")]
    public async Task<ActionResult<List<AttachmentDto>>> GetByItem(Guid itemId)
    {
        var attachments = await _db.Attachments
            .Where(a => a.BacklogItemId == itemId)
            .OrderBy(a => a.CreatedDate)
            .Select(a => new AttachmentDto(a.Id, a.BacklogItemId, a.FileName, a.ContentType, a.FileSize, a.CreatedDate))
            .ToListAsync();
        return Ok(attachments);
    }

    [HttpPost("api/items/{itemId:guid}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid itemId, IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxFileSize)
            return BadRequest("File is empty or exceeds 10 MB limit.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest($"Content type '{file.ContentType}' is not allowed. Only images are accepted.");

        var blobName = await _storage.UploadAsync(file.FileName, file.OpenReadStream(), file.Length, file.ContentType);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            BacklogItemId = itemId,
            FileName = file.FileName,
            BlobName = blobName,
            ContentType = file.ContentType,
            FileSize = file.Length
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Attachment {FileName} uploaded for item {ItemId}", file.FileName, itemId);
        return Ok(new AttachmentDto(attachment.Id, attachment.BacklogItemId, attachment.FileName, attachment.ContentType, attachment.FileSize, attachment.CreatedDate));
    }

    [HttpDelete("api/items/{itemId:guid}/attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid itemId, Guid id)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == id && a.BacklogItemId == itemId);
        if (attachment == null) return NotFound();

        await _storage.DeleteAsync(attachment.BlobName);
        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("api/attachments/{id:guid}/url")]
    public async Task<ActionResult<object>> GetUrl(Guid id)
    {
        var attachment = await _db.Attachments.FindAsync(id);
        if (attachment == null) return NotFound();

        var url = await _storage.GetPresignedUrlAsync(attachment.BlobName);
        return Ok(new { url });
    }
}
