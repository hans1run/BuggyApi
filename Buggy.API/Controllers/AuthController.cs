using Buggy.API.DTOs;
using Buggy.API.Extensions;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Buggy.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _users;

    public AuthController(IUserService users) => _users = users;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthExtensions.LoginRateLimit)]
    public async Task<ActionResult<UserDto>> Login(LoginDto dto)
    {
        var user = await _users.ValidateCredentialsAsync(dto.Email, dto.Password);
        if (user == null)
            return Unauthorized(new ProblemDetails { Title = "Invalid email or password." });

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthExtensions.CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = true });

        return Ok(IUserService.ToDto(user));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = User.GetUserId();
        var user = id == null ? null : await _users.GetEntityAsync(id.Value);
        return user == null ? Unauthorized() : Ok(IUserService.ToDto(user));
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<ActionResult<UserDto>> ChangePassword(ChangePasswordDto dto)
    {
        var id = User.GetUserId();
        if (id == null)
            return Unauthorized();

        var result = await _users.ChangePasswordAsync(id.Value, dto);
        if (result.Error == UserError.WrongPassword)
            return BadRequest(new ProblemDetails { Title = result.Message });
        if (result.Error != UserError.None)
            return NotFound();

        // The stamp rotated, which invalidates the current cookie; issue a fresh one.
        var user = await _users.GetEntityAsync(id.Value);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthExtensions.CreatePrincipal(user!),
            new AuthenticationProperties { IsPersistent = true });

        return Ok(result.User);
    }
}
