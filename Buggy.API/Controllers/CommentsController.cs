using Buggy.API.DTOs;
using Buggy.API.Extensions;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buggy.API.Controllers;

[ApiController]
[Route("api/items/{itemId:guid}/comments")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _service;

    public CommentsController(ICommentService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<List<CommentDto>>> GetAll(Guid itemId) =>
        Ok(await _service.GetByItemAsync(itemId));

    [HttpPost]
    [Authorize(Policy = AuthExtensions.ApiKeyOrUserPolicy)]
    public async Task<ActionResult<CommentDto>> Create(Guid itemId, CreateCommentDto dto)
    {
        var createdBy = User.GetDisplayName();
        var comment = await _service.CreateAsync(itemId, dto, createdBy);
        return comment == null ? NotFound() : Ok(comment);
    }
}
