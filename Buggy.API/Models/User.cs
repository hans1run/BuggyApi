namespace Buggy.API.Models;

public class User
{
    public Guid Id { get; set; }

    /// <summary>Stored lower-cased and trimmed; unique.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Rotated on password change, role change or deletion; sessions whose
    /// stamp no longer matches are rejected on the next request.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
}
