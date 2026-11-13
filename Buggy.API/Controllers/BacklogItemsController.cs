using Buggy.API.DTOs;
using Buggy.API.Extensions;
using Buggy.API.Models;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buggy.API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/items")]
[Authorize]
public class BacklogItemsController : ControllerBase
{
    private readonly IBacklogItemService _service;

    public BacklogItemsController(IBacklogItemService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<List<BacklogItemDto>>> GetAll(
        Guid projectId, [FromQuery] BacklogItemType? type, [FromQuery] Priority? priority) =>
        Ok(await _service.GetByProjectAsync(projectId, type, priority));

    [HttpGet("{itemNumber:int}")]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<BacklogItemDto>> GetByNumber(Guid projectId, int itemNumber)
    {
        var item = await _service.GetByNumberAsync(projectId, itemNumber);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<BacklogItemDto>> Create(Guid projectId, CreateBacklogItemDto dto)
    {
        var result = await _service.CreateAsync(projectId, dto, User.GetDisplayName());
        return result.Error == ItemError.None
            ? CreatedAtAction(nameof(GetByNumber), new { projectId, itemNumber = result.Value!.ItemNumber }, result.Value)
            : ToActionResult(result);
    }

    [HttpPut("{itemNumber:int}")]
    public async Task<ActionResult<BacklogItemDto>> Update(Guid projectId, int itemNumber, UpdateBacklogItemDto dto)
    {
        var item = await _service.UpdateAsync(projectId, itemNumber, dto);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPut("{itemNumber:int}/status")]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<BacklogItemDto>> UpdateStatus(Guid projectId, int itemNumber, UpdateStatusDto dto)
    {
        var item = await _service.UpdateStatusAsync(projectId, itemNumber, dto);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpGet("{itemNumber:int}/subtasks")]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<List<BacklogItemDto>>> GetSubtasks(Guid projectId, int itemNumber)
    {
        var subtasks = await _service.GetSubtasksAsync(projectId, itemNumber);
        return subtasks == null ? NotFound() : Ok(subtasks);
    }

    [HttpPut("{itemNumber:int}/parent")]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<BacklogItemDto>> SetParent(Guid projectId, int itemNumber, SetParentDto dto) =>
        ToActionResult(await _service.SetParentAsync(projectId, itemNumber, dto));

    [HttpPut("{itemNumber:int}/subtasks/order")]
    public async Task<ActionResult<List<BacklogItemDto>>> ReorderSubtasks(
        Guid projectId, int itemNumber, ReorderSubtasksDto dto) =>
        ToActionResult(await _service.ReorderSubtasksAsync(projectId, itemNumber, dto));

    [HttpPut("{itemNumber:int}/archive")]
    public async Task<IActionResult> Archive(Guid projectId, int itemNumber) =>
        await _service.ArchiveAsync(projectId, itemNumber) ? NoContent() : NotFound();

    [HttpGet("archived")]
    public async Task<ActionResult<List<BacklogItemDto>>> GetArchived(Guid projectId) =>
        Ok(await _service.GetArchivedByProjectAsync(projectId));

    [HttpPut("{itemNumber:int}/unarchive")]
    public async Task<ActionResult<BacklogItemDto>> Unarchive(Guid projectId, int itemNumber)
    {
        var item = await _service.UnarchiveAsync(projectId, itemNumber);
        return item == null ? NotFound() : Ok(item);
    }

    private ActionResult<T> ToActionResult<T>(ItemResult<T> result) => result.Error switch
    {
        ItemError.None => Ok(result.Value),
        ItemError.NotFound when result.Message != null => NotFound(new ProblemDetails { Title = result.Message }),
        ItemError.NotFound => NotFound(),
        _ => BadRequest(new ProblemDetails { Title = result.Message })
    };
}
