namespace Buggy.API.Models;

public abstract class BacklogItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int ItemNumber { get; set; }
    public BacklogItemType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Priority Priority { get; set; } = Priority.None;
    public BacklogItemStatus Status { get; set; } = BacklogItemStatus.Backlog;
    public string? AssignedTo { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
    public bool IsArchived { get; set; }
    public string? Tags { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
