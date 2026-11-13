using System.Security.Cryptography;
using System.Text;
using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Services;

public interface IApiKeyService
{
    Task<List<ApiKeyDto>> GetAllAsync();

    /// <summary>Returns null when the name is already taken.</summary>
    Task<CreatedApiKeyDto?> CreateAsync(string name, string createdBy);

    Task<bool> DeleteAsync(Guid id);

    /// <summary>Returns the key matching the secret, or null.</summary>
    Task<ApiKey?> ValidateAsync(string secret);
}

public class ApiKeyService : IApiKeyService
{
    private const string SecretPrefix = "bgy_";
    private const int PrefixDisplayLength = 12;
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(5);

    private readonly BuggyDbContext _db;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(BuggyDbContext db, ILogger<ApiKeyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ApiKeyDto>> GetAllAsync() =>
        (await _db.ApiKeys.OrderBy(k => k.Name).ToListAsync()).Select(ToDto).ToList();

    public async Task<CreatedApiKeyDto?> CreateAsync(string name, string createdBy)
    {
        name = name.Trim();
        if (await _db.ApiKeys.AnyAsync(k => k.Name == name))
            return null;

        // 32 random bytes, base64url: ~256 bits of entropy, so a fast hash is enough.
        var secret = SecretPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var key = new ApiKey
        {
            Name = name,
            KeyHash = Hash(secret),
            Prefix = secret[..PrefixDisplayLength],
            CreatedBy = createdBy
        };

        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();
        _logger.LogInformation("API key {Name} created by {CreatedBy}", key.Name, createdBy);
        return new CreatedApiKeyDto(ToDto(key), secret);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null)
            return false;

        _db.ApiKeys.Remove(key);
        await _db.SaveChangesAsync();
        _logger.LogInformation("API key {Name} revoked", key.Name);
        return true;
    }

    public async Task<ApiKey?> ValidateAsync(string secret)
    {
        if (!secret.StartsWith(SecretPrefix, StringComparison.Ordinal))
            return null;

        var hash = Hash(secret);
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash);
        if (key == null)
            return null;

        // Record usage, but not on every request.
        var now = DateTime.UtcNow;
        if (key.LastUsedDate == null || now - key.LastUsedDate > LastUsedResolution)
        {
            key.LastUsedDate = now;
            await _db.SaveChangesAsync();
        }

        return key;
    }

    private static ApiKeyDto ToDto(ApiKey k) =>
        new(k.Id, k.Name, k.Prefix, k.CreatedBy, k.CreatedDate, k.LastUsedDate);

    private static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
