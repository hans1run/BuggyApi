namespace Buggy.API.Models;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ItemCounter { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }
    public ICollection<BacklogItem> Items { get; set; } = new List<BacklogItem>();
}
