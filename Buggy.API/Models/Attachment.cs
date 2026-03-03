namespace Buggy.API.Models;

public class Attachment
{
    public Guid Id { get; set; }
    public Guid BacklogItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public BacklogItem BacklogItem { get; set; } = null!;
}
