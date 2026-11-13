namespace Buggy.API.Services;

public enum ItemError
{
    None,
    NotFound,
    /// <summary>The request breaks an item rule (e.g. subtasks are one level deep); see the message.</summary>
    Invalid
}

/// <summary>Outcome of an item operation that can fail for an expected reason. Messages are user-facing.</summary>
public record ItemResult<T>(ItemError Error, T? Value = default, string? Message = null)
{
    public static ItemResult<T> Ok(T value) => new(ItemError.None, value);
    public static ItemResult<T> NotFound(string? message = null) => new(ItemError.NotFound, default, message);
    public static ItemResult<T> Invalid(string message) => new(ItemError.Invalid, default, message);

    /// <summary>The same failure, carrying a different value type.</summary>
    public ItemResult<TOther> As<TOther>() => new(Error, default, Message);
}
