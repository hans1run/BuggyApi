using Microsoft.EntityFrameworkCore;
using Buggy.API.Models;

namespace Buggy.API.Data;

public class BuggyDbContext : DbContext
{
    public BuggyDbContext(DbContextOptions<BuggyDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<BacklogItem> BacklogItems => Set<BacklogItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TPH inheritance
        modelBuilder.Entity<BacklogItem>()
            .HasDiscriminator(b => b.Type)
            .HasValue<Bug>(BacklogItemType.Bug)
            .HasValue<Feature>(BacklogItemType.Feature)
            .HasValue<TaskItem>(BacklogItemType.Task);

        modelBuilder.Entity<BacklogItem>()
            .HasIndex(b => new { b.ProjectId, b.ItemNumber })
            .IsUnique();

        modelBuilder.Entity<BacklogItem>()
            .HasIndex(b => b.Status);

        modelBuilder.Entity<BacklogItem>()
            .HasOne(b => b.Project)
            .WithMany(p => p.Items)
            .HasForeignKey(b => b.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.BacklogItem)
            .WithMany(b => b.Comments)
            .HasForeignKey(c => c.BacklogItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.BacklogItem)
            .WithMany(b => b.Attachments)
            .HasForeignKey(a => a.BacklogItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
