namespace Buggy.API.Models;

public class Comment
{
    public Guid Id { get; set; }
    public Guid BacklogItemId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public BacklogItem BacklogItem { get; set; } = null!;
}
