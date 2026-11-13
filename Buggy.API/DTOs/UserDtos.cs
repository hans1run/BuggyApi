using System.ComponentModel.DataAnnotations;

namespace Buggy.API.DTOs;

public record UserDto(Guid Id, string Email, string DisplayName, bool IsAdmin, DateTime CreatedDate, DateTime? LastLoginDate);

public record LoginDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);

public record ChangePasswordDto(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MinLength(UserRules.MinPasswordLength), MaxLength(128)] string NewPassword);

public record CreateUserDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(100)] string DisplayName,
    [Required, MinLength(UserRules.MinPasswordLength), MaxLength(128)] string Password,
    bool IsAdmin);

/// <summary>Password is optional; when set it resets the user's password.</summary>
public record UpdateUserDto(
    [Required, MaxLength(100)] string DisplayName,
    bool IsAdmin,
    [MinLength(UserRules.MinPasswordLength), MaxLength(128)] string? Password);

public static class UserRules
{
    public const int MinPasswordLength = 8;
}
