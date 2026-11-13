using System.ComponentModel.DataAnnotations;
using Buggy.API.Models;

namespace Buggy.API.DTOs;

/// <param name="ParentId">Set when the item is a subtask.</param>
/// <param name="SubtaskCount">Non-archived subtasks.</param>
/// <param name="SubtaskDoneCount">Non-archived subtasks with status Done.</param>
public record BacklogItemDto(
    Guid Id, Guid ProjectId, int ItemNumber, BacklogItemType Type,
    string Title, string? Description, Priority Priority, BacklogItemStatus Status,
    string? AssignedTo, string CreatedBy, DateTime CreatedDate, DateTime? UpdatedDate,
    bool IsArchived, List<string> Tags, int CommentCount, int AttachmentCount,
    Guid? ParentId, int? ParentNumber, string? ParentTitle, int SortOrder,
    int SubtaskCount, int SubtaskDoneCount);

/// <param name="ParentNumber">Create the item as a subtask of this item.</param>
public record CreateBacklogItemDto(
    BacklogItemType Type,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    Priority Priority, BacklogItemStatus Status, string? AssignedTo, List<string>? Tags,
    int? ParentNumber = null);

public record UpdateBacklogItemDto(
    BacklogItemType Type,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    Priority Priority, string? AssignedTo, List<string>? Tags);

public record UpdateStatusDto(BacklogItemStatus Status);

/// <param name="ParentNumber">The new parent, or null to detach the item.</param>
public record SetParentDto(int? ParentNumber);

/// <param name="ItemNumbers">Every current subtask of the parent, in the new order.</param>
public record ReorderSubtasksDto([Required] List<int> ItemNumbers);
