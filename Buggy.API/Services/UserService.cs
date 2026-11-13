using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Services;

public class UserService : IUserService
{
    private readonly BuggyDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ILogger<UserService> _logger;

    // Verified against when the email is unknown, so a miss costs the same time as a hit.
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString()));

    public UserService(BuggyDbContext db, IPasswordHasher<User> hasher, ILogger<UserService> logger)
    {
        _db = db;
        _hasher = hasher;
        _logger = logger;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        var normalized = NormalizeEmail(email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);

        if (user == null)
        {
            _hasher.VerifyHashedPassword(new User(), DummyHash.Value, password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, password);

        user.LastLoginDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return user;
    }

    public Task<User?> GetEntityAsync(Guid id) => _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<UserDto>> GetAllAsync() =>
        (await _db.Users.OrderBy(u => u.DisplayName).ToListAsync())
            .Select(IUserService.ToDto)
            .ToList();

    public async Task<UserResult> CreateAsync(CreateUserDto dto)
    {
        var email = NormalizeEmail(dto.Email);
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return UserResult.Fail(UserError.EmailTaken, "A user with that email already exists.");

        var user = new User
        {
            Email = email,
            DisplayName = dto.DisplayName.Trim(),
            IsAdmin = dto.IsAdmin
        };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created user {Email} (admin: {IsAdmin})", user.Email, user.IsAdmin);
        return UserResult.Ok(IUserService.ToDto(user));
    }

    public async Task<UserResult> UpdateAsync(Guid id, UpdateUserDto dto, Guid actingUserId)
    {
        var user = await GetEntityAsync(id);
        if (user == null)
            return UserResult.Fail(UserError.NotFound, "User not found.");

        // Prevents an admin from locking themselves (and possibly everyone) out.
        if (id == actingUserId && !dto.IsAdmin)
            return UserResult.Fail(UserError.CannotModifySelf, "You cannot remove your own admin rights.");

        var invalidateSessions = user.IsAdmin != dto.IsAdmin;
        user.DisplayName = dto.DisplayName.Trim();
        user.IsAdmin = dto.IsAdmin;

        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);
            invalidateSessions = true;
        }

        if (invalidateSessions)
            user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync();
        return UserResult.Ok(IUserService.ToDto(user));
    }

    public async Task<UserResult> DeleteAsync(Guid id, Guid actingUserId)
    {
        if (id == actingUserId)
            return UserResult.Fail(UserError.CannotModifySelf, "You cannot delete yourself.");

        var user = await GetEntityAsync(id);
        if (user == null)
            return UserResult.Fail(UserError.NotFound, "User not found.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted user {Email}", user.Email);
        return UserResult.Ok();
    }

    public async Task<UserResult> ChangePasswordAsync(Guid id, ChangePasswordDto dto)
    {
        var user = await GetEntityAsync(id);
        if (user == null)
            return UserResult.Fail(UserError.NotFound, "User not found.");

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            return UserResult.Fail(UserError.WrongPassword, "Current password is incorrect.");

        user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync();
        return UserResult.Ok(IUserService.ToDto(user));
    }

    public async Task EnsureBootstrapAdminAsync(IConfiguration configuration)
    {
        if (await _db.Users.AnyAsync())
            return;

        var email = configuration["Auth:BootstrapAdmin:Email"];
        var password = configuration["Auth:BootstrapAdmin:Password"];
        var name = configuration["Auth:BootstrapAdmin:DisplayName"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "No users exist - nobody can log in. Add the first admin with deploy/add-admin.sh.");
            return;
        }

        if (password.Length < UserRules.MinPasswordLength)
        {
            _logger.LogError(
                "Auth:BootstrapAdmin:Password must be at least {Min} characters; bootstrap admin not created.",
                UserRules.MinPasswordLength);
            return;
        }

        await CreateAsync(new CreateUserDto(
            email,
            string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name,
            password,
            IsAdmin: true));
        _logger.LogInformation("Bootstrap admin {Email} created. You can now remove the Auth:BootstrapAdmin settings.", email);
    }
}
