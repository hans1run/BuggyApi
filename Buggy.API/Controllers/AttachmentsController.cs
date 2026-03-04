using Buggy.API.DTOs;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buggy.API.Controllers;

[ApiController]
[Authorize(Policy = "SingleUser")]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _service;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public AttachmentsController(IAttachmentService service) => _service = service;

    [HttpGet("api/items/{itemId:guid}/attachments")]
    public async Task<ActionResult<List<AttachmentDto>>> GetByItem(Guid itemId) =>
        Ok(await _service.GetByItemAsync(itemId));

    [HttpPost("api/items/{itemId:guid}/attachments")]
    [Authorize(Policy = "ApiKeyOrSingleUser")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid itemId, IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxFileSize)
            return BadRequest("File is empty or exceeds 10 MB limit.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest($"Content type '{file.ContentType}' is not allowed. Only images are accepted.");

        var result = await _service.UploadAsync(itemId, file.FileName, file.OpenReadStream(), file.Length, file.ContentType);
        return result == null ? NotFound("Backlog item not found.") : Ok(result);
    }

    [HttpDelete("api/items/{itemId:guid}/attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid itemId, Guid id) =>
        await _service.DeleteAsync(itemId, id) ? NoContent() : NotFound();

    [HttpGet("api/attachments/{id:guid}/url")]
    public async Task<ActionResult<object>> GetUrl(Guid id)
    {
        var url = await _service.GetPresignedUrlAsync(id);
        return url == null ? NotFound() : Ok(new { url });
    }
}
