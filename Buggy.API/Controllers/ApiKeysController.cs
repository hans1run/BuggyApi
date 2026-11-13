using Buggy.API.DTOs;
using Buggy.API.Extensions;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buggy.API.Controllers;

[ApiController]
[Route("api/api-keys")]
[Authorize(Policy = AuthExtensions.AdminPolicy)]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _keys;

    public ApiKeysController(IApiKeyService keys) => _keys = keys;

    [HttpGet]
    public async Task<ActionResult<List<ApiKeyDto>>> GetAll() => Ok(await _keys.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<CreatedApiKeyDto>> Create(CreateApiKeyDto dto)
    {
        var created = await _keys.CreateAsync(dto.Name, User.GetDisplayName());
        return created == null
            ? Conflict(new ProblemDetails { Title = "An API key with that name already exists." })
            : StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        await _keys.DeleteAsync(id) ? NoContent() : NotFound();
}
