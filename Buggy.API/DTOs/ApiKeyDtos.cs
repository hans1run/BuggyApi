using System.ComponentModel.DataAnnotations;

namespace Buggy.API.DTOs;

public record ApiKeyDto(Guid Id, string Name, string Prefix, string CreatedBy, DateTime CreatedDate, DateTime? LastUsedDate);

public record CreateApiKeyDto([Required, MaxLength(50)] string Name);

/// <summary>Returned only once, when the key is created.</summary>
public record CreatedApiKeyDto(ApiKeyDto Key, string Secret);

public record MemberDto(string DisplayName);
