using Buggy.API.DTOs;
using Buggy.API.Models;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buggy.API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/items")]
[Authorize(Policy = "SingleUser")]
public class BacklogItemsController : ControllerBase
{
    private readonly IBacklogItemService _service;

    public BacklogItemsController(IBacklogItemService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<BacklogItemDto>>> GetAll(
        Guid projectId, [FromQuery] BacklogItemType? type, [FromQuery] Priority? priority) =>
        Ok(await _service.GetByProjectAsync(projectId, type, priority));

    [HttpGet("{itemNumber:int}")]
    public async Task<ActionResult<BacklogItemDto>> GetByNumber(Guid projectId, int itemNumber)
    {
        var item = await _service.GetByNumberAsync(projectId, itemNumber);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<BacklogItemDto>> Create(Guid projectId, CreateBacklogItemDto dto)
    {
        var createdBy = User.FindFirst("sub")?.Value ?? "unknown";
        var item = await _service.CreateAsync(projectId, dto, createdBy);
        return CreatedAtAction(nameof(GetByNumber), new { projectId, itemNumber = item.ItemNumber }, item);
    }

    [HttpPut("{itemNumber:int}")]
    public async Task<ActionResult<BacklogItemDto>> Update(Guid projectId, int itemNumber, UpdateBacklogItemDto dto)
    {
        var item = await _service.UpdateAsync(projectId, itemNumber, dto);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPut("{itemNumber:int}/status")]
    public async Task<ActionResult<BacklogItemDto>> UpdateStatus(Guid projectId, int itemNumber, UpdateStatusDto dto)
    {
        var item = await _service.UpdateStatusAsync(projectId, itemNumber, dto);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPut("{itemNumber:int}/archive")]
    public async Task<IActionResult> Archive(Guid projectId, int itemNumber) =>
        await _service.ArchiveAsync(projectId, itemNumber) ? NoContent() : NotFound();
}
