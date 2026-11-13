using System.Linq.Expressions;
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

    /// <summary>The one mapping to BacklogItemDto, translated to SQL so counts are always current.</summary>
    private static readonly Expression<Func<BacklogItem, BacklogItemDto>> ToDto = b => new BacklogItemDto(
        b.Id, b.ProjectId, b.ItemNumber, b.Type,
        b.Title, b.Description, b.Priority, b.Status,
        b.AssignedTo, b.CreatedBy, b.CreatedDate, b.UpdatedDate,
        b.IsArchived,
        string.IsNullOrEmpty(b.Tags) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(b.Tags)!,
        b.Comments.Count,
        b.Attachments.Count,
        b.ParentId,
        b.Parent == null ? null : b.Parent.ItemNumber,
        b.Parent == null ? null : b.Parent.Title,
        b.SortOrder,
        b.Subtasks.Count(s => !s.IsArchived),
        b.Subtasks.Count(s => !s.IsArchived && s.Status == BacklogItemStatus.Done));

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
            .Select(ToDto)
            .ToListAsync();
    }

    public Task<BacklogItemDto?> GetByNumberAsync(Guid projectId, int itemNumber) =>
        _db.BacklogItems
            .Where(b => b.ProjectId == projectId && b.ItemNumber == itemNumber)
            .Select(ToDto)
            .FirstOrDefaultAsync();

    public async Task<ItemResult<BacklogItemDto>> CreateAsync(Guid projectId, CreateBacklogItemDto dto, string createdBy)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId))
            return ItemResult<BacklogItemDto>.NotFound("Project not found.");

        // Validate before taking an item number, so a rejected subtask doesn't use one up.
        BacklogItem? parent = null;
        if (dto.ParentNumber is int parentNumber)
        {
            var parentResult = await LoadValidParentAsync(projectId, parentNumber);
            if (parentResult.Error != ItemError.None) return parentResult.As<BacklogItemDto>();
            parent = parentResult.Value;
        }

        // Atomic increment to prevent race conditions on concurrent item creation
        await _db.Database.ExecuteSqlRawAsync(
            """UPDATE "Projects" SET "ItemCounter" = "ItemCounter" + 1 WHERE "Id" = {0}""",
            projectId);
        var newItemNumber = await _db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.ItemCounter)
            .SingleAsync();

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
        if (parent != null) await AttachAsync(item, parent);

        _db.BacklogItems.Add(item);
        await _db.SaveChangesAsync();
        return ItemResult<BacklogItemDto>.Ok(await GetByIdAsync(item.Id));
    }

    public async Task<BacklogItemDto?> UpdateAsync(Guid projectId, int itemNumber, UpdateBacklogItemDto dto)
    {
        var item = await FindAsync(projectId, itemNumber);
        if (item == null) return null;

        item.Title = dto.Title;
        item.Description = dto.Description;
        item.Priority = dto.Priority;
        item.AssignedTo = dto.AssignedTo;
        item.Tags = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : null;
        item.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // EF Core doesn't allow changing the TPH discriminator on a tracked entity,
        // so update via raw SQL when the type changes.
        if (item.Type != dto.Type)
        {
            await _db.Database.ExecuteSqlRawAsync(
                """UPDATE "BacklogItems" SET "Type" = {0} WHERE "Id" = {1}""",
                (int)dto.Type, item.Id);
            _db.Entry(item).State = EntityState.Detached;
        }

        return await GetByIdAsync(item.Id);
    }

    public async Task<BacklogItemDto?> UpdateStatusAsync(Guid projectId, int itemNumber, UpdateStatusDto dto)
    {
        var item = await FindAsync(projectId, itemNumber);
        if (item == null) return null;

        item.Status = dto.Status;
        item.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(item.Id);
    }

    public async Task<List<BacklogItemDto>?> GetSubtasksAsync(Guid projectId, int itemNumber)
    {
        var parent = await FindAsync(projectId, itemNumber);
        if (parent == null) return null;

        return await _db.BacklogItems
            .Where(b => b.ParentId == parent.Id && !b.IsArchived)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.ItemNumber)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<ItemResult<BacklogItemDto>> SetParentAsync(Guid projectId, int itemNumber, SetParentDto dto)
    {
        var item = await FindAsync(projectId, itemNumber);
        if (item == null) return ItemResult<BacklogItemDto>.NotFound();
        if (item.IsArchived)
            return ItemResult<BacklogItemDto>.Invalid($"#{itemNumber} is archived; restore it first.");

        if (dto.ParentNumber is not int parentNumber)
        {
            Detach(item);
        }
        else if (item.Parent?.ItemNumber != parentNumber)
        {
            var parentResult = await LoadValidParentAsync(projectId, parentNumber);
            if (parentResult.Error != ItemError.None) return parentResult.As<BacklogItemDto>();
            var parent = parentResult.Value!;

            var problem = await WhyCannotBecomeSubtaskAsync(item, parent);
            if (problem != null) return ItemResult<BacklogItemDto>.Invalid(problem);
            await AttachAsync(item, parent);
        }

        item.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ItemResult<BacklogItemDto>.Ok(await GetByIdAsync(item.Id));
    }

    public async Task<ItemResult<List<BacklogItemDto>>> ReorderSubtasksAsync(
        Guid projectId, int itemNumber, ReorderSubtasksDto dto)
    {
        var parent = await FindAsync(projectId, itemNumber);
        if (parent == null) return ItemResult<List<BacklogItemDto>>.NotFound();

        var subtasks = await _db.BacklogItems
            .Where(b => b.ParentId == parent.Id && !b.IsArchived)
            .ToListAsync();

        if (!subtasks.Select(s => s.ItemNumber).Order().SequenceEqual(dto.ItemNumbers.Order()))
            return ItemResult<List<BacklogItemDto>>.Invalid(
                $"The new order must list each subtask of #{itemNumber} exactly once.");

        foreach (var subtask in subtasks)
            subtask.SortOrder = dto.ItemNumbers.IndexOf(subtask.ItemNumber);

        await _db.SaveChangesAsync();
        return ItemResult<List<BacklogItemDto>>.Ok((await GetSubtasksAsync(projectId, itemNumber))!);
    }

    public async Task<bool> ArchiveAsync(Guid projectId, int itemNumber)
    {
        var item = await FindAsync(projectId, itemNumber);
        if (item == null) return false;
        // Re-archiving would give the item a new ArchivedDate and strand the subtasks archived with it.
        if (item.IsArchived) return true;

        var now = UtcNowForDatabase();
        Archive(item, now);

        // Open subtasks go with the parent and share its ArchivedDate,
        // so restoring the parent can bring back exactly these.
        var subtasks = await _db.BacklogItems
            .Where(b => b.ParentId == item.Id && !b.IsArchived)
            .ToListAsync();
        foreach (var subtask in subtasks)
            Archive(subtask, now);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<BacklogItemDto>> GetArchivedByProjectAsync(Guid projectId)
    {
        return await _db.BacklogItems
            .Where(b => b.ProjectId == projectId && b.IsArchived)
            .OrderByDescending(b => b.ArchivedDate ?? b.UpdatedDate ?? b.CreatedDate)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<BacklogItemDto?> UnarchiveAsync(Guid projectId, int itemNumber)
    {
        var item = await FindAsync(projectId, itemNumber);
        if (item == null) return null;

        // A subtask can't come back under an archived parent; it returns as a normal item.
        if (item.Parent is { IsArchived: true }) Detach(item);

        if (item.ArchivedDate is DateTime archivedDate)
        {
            // Subtasks archived together with this item come back as they were.
            var subtasks = await _db.BacklogItems
                .Where(b => b.ParentId == item.Id && b.IsArchived && b.ArchivedDate == archivedDate)
                .ToListAsync();
            foreach (var subtask in subtasks)
                Restore(subtask);
        }

        Restore(item);
        item.Status = BacklogItemStatus.Backlog;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(item.Id);
    }

    private Task<BacklogItem?> FindAsync(Guid projectId, int itemNumber) =>
        _db.BacklogItems
            .Include(b => b.Parent)
            .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.ItemNumber == itemNumber);

    private Task<BacklogItemDto> GetByIdAsync(Guid id) =>
        _db.BacklogItems.Where(b => b.Id == id).Select(ToDto).SingleAsync();

    /// <summary>
    /// Loads <paramref name="parentNumber"/> as a parent for a subtask: it must exist in the project,
    /// not be archived, and not be a subtask itself (subtasks are one level deep).
    /// The parent number comes from the request body, so a missing parent is Invalid rather than NotFound.
    /// </summary>
    private async Task<ItemResult<BacklogItem>> LoadValidParentAsync(Guid projectId, int parentNumber)
    {
        var parent = await FindAsync(projectId, parentNumber);
        if (parent == null)
            return ItemResult<BacklogItem>.Invalid($"Item #{parentNumber} does not exist in this project.");
        if (parent.IsArchived)
            return ItemResult<BacklogItem>.Invalid($"#{parentNumber} is archived.");
        if (parent.ParentId != null)
            return ItemResult<BacklogItem>.Invalid(
                $"#{parentNumber} is itself a subtask; subtasks can only be one level deep.");
        return ItemResult<BacklogItem>.Ok(parent);
    }

    /// <summary>Why an existing item cannot go under <paramref name="parent"/>, or null if it can.</summary>
    private async Task<string?> WhyCannotBecomeSubtaskAsync(BacklogItem item, BacklogItem parent)
    {
        if (parent.Id == item.Id) return "An item cannot be its own subtask.";
        // Archived subtasks count too: restoring them later would make a second level.
        if (await _db.BacklogItems.AnyAsync(b => b.ParentId == item.Id))
            return $"#{item.ItemNumber} has subtasks of its own, so it cannot become a subtask.";
        return null;
    }

    /// <summary>Puts the item last among the parent's subtasks.</summary>
    private async Task AttachAsync(BacklogItem item, BacklogItem parent)
    {
        var lastSortOrder = await _db.BacklogItems
            .Where(b => b.ParentId == parent.Id)
            .MaxAsync(b => (int?)b.SortOrder);
        item.ParentId = parent.Id;
        item.SortOrder = (lastSortOrder ?? -1) + 1;
    }

    /// <summary>Makes the item top-level; SortOrder only means something under a parent.</summary>
    private static void Detach(BacklogItem item)
    {
        item.ParentId = null;
        item.Parent = null;
        item.SortOrder = 0;
    }

    private static void Archive(BacklogItem item, DateTime now)
    {
        item.IsArchived = true;
        item.ArchivedDate = now;
        item.UpdatedDate = now;
    }

    private static void Restore(BacklogItem item)
    {
        item.IsArchived = false;
        item.ArchivedDate = null;
        item.UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Postgres stores microseconds; .NET ticks are 100 ns. Truncating up front keeps the value
    /// written for the parent and its subtasks identical to what is read back and compared later.
    /// </summary>
    private static DateTime UtcNowForDatabase()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }
}
