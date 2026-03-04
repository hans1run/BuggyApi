using System.Text.Json;
using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Services;

public class BacklogItemService : IBacklogItemService
{
    private readonly BuggyDbContext _db;

    public BacklogItemService(BuggyDbContext db) => _db = db;

    public async Task<List<BacklogItemDto>> GetByProjectAsync(
        Guid projectId, BacklogItemType? type = null, Priority? priority = null)
    {
        var query = _db.BacklogItems
            .Where(b => b.ProjectId == projectId && !b.IsArchived);

        if (type.HasValue) query = query.Where(b => b.Type == type.Value);
        if (priority.HasValue) query = query.Where(b => b.Priority == priority.Value);

        return await query
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedDate)
            .Select(b => new BacklogItemDto(
                b.Id, b.ProjectId, b.ItemNumber, b.Type,
                b.Title, b.Description, b.Priority, b.Status,
                b.AssignedTo, b.CreatedBy, b.CreatedDate, b.UpdatedDate,
                b.IsArchived,
                string.IsNullOrEmpty(b.Tags) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(b.Tags)!,
                b.Comments.Count,
                b.Attachments.Count))
            .ToListAsync();
    }

    public async Task<BacklogItemDto?> GetByNumberAsync(Guid projectId, int itemNumber)
    {
        var item = await _db.BacklogItems
            .Include(b => b.Comments)
            .Include(b => b.Attachments)
            .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.ItemNumber == itemNumber);
        return item == null ? null : ToDto(item);
    }

    public async Task<BacklogItemDto> CreateAsync(Guid projectId, CreateBacklogItemDto dto, string createdBy)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId))
            throw new ArgumentException("Project not found");

        // Atomic increment to prevent race conditions on concurrent item creation
        var newItemNumber = await _db.Database.SqlQueryRaw<int>(
            """UPDATE "Projects" SET "ItemCounter" = "ItemCounter" + 1 WHERE "Id" = {0} RETURNING "ItemCounter" """,
            projectId).SingleAsync();

        BacklogItem item = dto.Type switch
        {
            BacklogItemType.Bug => new Bug(),
            BacklogItemType.Feature => new Feature(),
            BacklogItemType.Task => new TaskItem(),
            _ => throw new ArgumentException($"Unknown type: {dto.Type}")
        };

        item.Id = Guid.NewGuid();
        item.ProjectId = projectId;
        item.ItemNumber = newItemNumber;
        item.Title = dto.Title;
        item.Description = dto.Description;
        item.Priority = dto.Priority;
        item.Status = dto.Status;
        item.AssignedTo = dto.AssignedTo;
        item.CreatedBy = createdBy;
        item.Tags = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : null;

        _db.BacklogItems.Add(item);
        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    public async Task<BacklogItemDto?> UpdateAsync(Guid projectId, int itemNumber, UpdateBacklogItemDto dto)
    {
        var item = await _db.BacklogItems
            .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.ItemNumber == itemNumber);
        if (item == null) return null;

        item.Title = dto.Title;
        item.Description = dto.Description;
        item.Priority = dto.Priority;
        item.AssignedTo = dto.AssignedTo;
        item.Tags = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : null;
        item.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    public async Task<BacklogItemDto?> UpdateStatusAsync(Guid projectId, int itemNumber, UpdateStatusDto dto)
    {
        var item = await _db.BacklogItems
            .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.ItemNumber == itemNumber);
        if (item == null) return null;

        item.Status = dto.Status;
        item.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    public async Task<bool> ArchiveAsync(Guid projectId, int itemNumber)
    {
        var item = await _db.BacklogItems
            .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.ItemNumber == itemNumber);
        if (item == null) return false;

        item.IsArchived = true;
        item.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<BacklogItemDto>> GetArchivedByProjectAsync(Guid projectId)
    {
        return await _db.BacklogItems
            .Where(b => b.ProjectId == projectId && b.IsArchived)
            .OrderByDescending(b => b.UpdatedDate ?? b.CreatedDate)
            .Select(b => new BacklogItemDto(
                b.Id, b.ProjectId, b.ItemNumber, b.Type,
                b.Title, b.Description, b.Priority, b.Status,
                b.AssignedTo, b.CreatedBy, b.CreatedDate, b.UpdatedDate,
                b.IsArchived,
                string.IsNullOrEmpty(b.Tags) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(b.Tags)!,
                b.Comments.Count,
                b.Attachments.Count))
            .ToListAsync();
    }

    public async Task<BacklogItemDto?> UnarchiveAsync(Guid projectId, int itemNumber)
    {
        var item = await _db.BacklogItems
            .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.ItemNumber == itemNumber);
        if (item == null) return null;

        item.IsArchived = false;
        item.Status = BacklogItemStatus.Backlog;
        item.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    private static BacklogItemDto ToDto(BacklogItem b) => new(
        b.Id, b.ProjectId, b.ItemNumber, b.Type,
        b.Title, b.Description, b.Priority, b.Status,
        b.AssignedTo, b.CreatedBy, b.CreatedDate, b.UpdatedDate,
        b.IsArchived,
        string.IsNullOrEmpty(b.Tags) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(b.Tags) ?? new(),
        b.Comments?.Count ?? 0,
        b.Attachments?.Count ?? 0);
}
