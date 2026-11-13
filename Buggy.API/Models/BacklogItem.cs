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
    /// <summary>When the item was archived; subtasks archived with their parent share the parent's value.</summary>
    public DateTime? ArchivedDate { get; set; }
    public string? Tags { get; set; }

    /// <summary>Set when this item is a subtask. Only one level: a parent never has a parent itself.</summary>
    public Guid? ParentId { get; set; }
    /// <summary>Position among the parent's subtasks (ascending). Unused for top-level items.</summary>
    public int SortOrder { get; set; }

    public Project Project { get; set; } = null!;
    public BacklogItem? Parent { get; set; }
    public ICollection<BacklogItem> Subtasks { get; set; } = new List<BacklogItem>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
