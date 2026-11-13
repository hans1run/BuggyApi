using Buggy.API.DTOs;
using Buggy.API.Extensions;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buggy.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthExtensions.AdminPolicy)]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll() => Ok(await _users.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto) =>
        ToActionResult(await _users.CreateAsync(dto), created: true);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserDto dto) =>
        ToActionResult(await _users.UpdateAsync(id, dto, User.GetUserId()!.Value));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var result = await _users.DeleteAsync(id, User.GetUserId()!.Value);
        return result.Error == UserError.None ? NoContent() : ToActionResult(result).Result!;
    }

    private ActionResult<UserDto> ToActionResult(UserResult result, bool created = false) => result.Error switch
    {
        UserError.None when created => StatusCode(StatusCodes.Status201Created, result.User),
        UserError.None => Ok(result.User),
        UserError.NotFound => NotFound(),
        UserError.EmailTaken => Conflict(new ProblemDetails { Title = result.Message }),
        _ => BadRequest(new ProblemDetails { Title = result.Message })
    };
}
