using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Buggy.API.Models;

namespace Buggy.API.Data;

public class BuggyDbContext : DbContext, IDataProtectionKeyContext
{
    public BuggyDbContext(DbContextOptions<BuggyDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<BacklogItem> BacklogItems => Set<BacklogItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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

        modelBuilder.Entity<User>(u =>
        {
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email).HasMaxLength(256);
            u.Property(x => x.DisplayName).HasMaxLength(100);
            u.Property(x => x.SecurityStamp).HasMaxLength(64);
        });

        modelBuilder.Entity<ApiKey>(k =>
        {
            k.HasIndex(x => x.Name).IsUnique();
            k.HasIndex(x => x.KeyHash).IsUnique();
            k.Property(x => x.Name).HasMaxLength(50);
            k.Property(x => x.KeyHash).HasMaxLength(64);
            k.Property(x => x.Prefix).HasMaxLength(16);
            k.Property(x => x.CreatedBy).HasMaxLength(100);
        });
    }
}
