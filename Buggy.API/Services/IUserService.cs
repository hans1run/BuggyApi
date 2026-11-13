using Buggy.API.DTOs;
using Buggy.API.Models;

namespace Buggy.API.Services;

public interface IUserService
{
    /// <summary>Returns the user when the credentials are valid, otherwise null.</summary>
    Task<User?> ValidateCredentialsAsync(string email, string password);

    Task<User?> GetEntityAsync(Guid id);
    Task<List<UserDto>> GetAllAsync();
    Task<UserResult> CreateAsync(CreateUserDto dto);
    Task<UserResult> UpdateAsync(Guid id, UpdateUserDto dto, Guid actingUserId);
    Task<UserResult> DeleteAsync(Guid id, Guid actingUserId);
    Task<UserResult> ChangePasswordAsync(Guid id, ChangePasswordDto dto);

    /// <summary>Creates the first admin from configuration when no users exist.</summary>
    Task EnsureBootstrapAdminAsync(IConfiguration configuration);

    static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.IsAdmin, u.CreatedDate, u.LastLoginDate);
}

public enum UserError
{
    None,
    NotFound,
    EmailTaken,
    WrongPassword,
    CannotModifySelf
}

public record UserResult(UserError Error, UserDto? User = null, string? Message = null)
{
    public static UserResult Ok(UserDto? user = null) => new(UserError.None, user);
    public static UserResult Fail(UserError error, string message) => new(error, null, message);
}
