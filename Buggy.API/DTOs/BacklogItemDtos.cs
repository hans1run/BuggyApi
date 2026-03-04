using System.ComponentModel.DataAnnotations;
using Buggy.API.Models;

namespace Buggy.API.DTOs;

public record BacklogItemDto(
    Guid Id, Guid ProjectId, int ItemNumber, BacklogItemType Type,
    string Title, string? Description, Priority Priority, BacklogItemStatus Status,
    string? AssignedTo, string CreatedBy, DateTime CreatedDate, DateTime? UpdatedDate,
    bool IsArchived, List<string> Tags, int CommentCount, int AttachmentCount);

public record CreateBacklogItemDto(
    BacklogItemType Type,
    [property: Required, MaxLength(200)] string Title,
    [property: MaxLength(4000)] string? Description,
    Priority Priority, BacklogItemStatus Status, string? AssignedTo, List<string>? Tags);

public record UpdateBacklogItemDto(
    [property: Required, MaxLength(200)] string Title,
    [property: MaxLength(4000)] string? Description,
    Priority Priority, string? AssignedTo, List<string>? Tags);

public record UpdateStatusDto(BacklogItemStatus Status);
