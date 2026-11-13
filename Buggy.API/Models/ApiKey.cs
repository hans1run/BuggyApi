namespace Buggy.API.Models;

/// <summary>
/// A named key for integrations (e.g. "Claude"). Only a SHA-256 hash of the
/// secret is stored; the secret is shown once when the key is created.
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; }

    /// <summary>Unique; recorded as the author of items and comments created with the key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-case hex SHA-256 of the full secret.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First characters of the secret, to tell keys apart in the UI.</summary>
    public string Prefix { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedDate { get; set; }
}
