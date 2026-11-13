using Buggy.API.DTOs;
using Buggy.API.Models;

namespace Buggy.API.Services;

public interface IBacklogItemService
{
    Task<List<BacklogItemDto>> GetByProjectAsync(Guid projectId, BacklogItemType? type = null, Priority? priority = null);
    Task<BacklogItemDto?> GetByNumberAsync(Guid projectId, int itemNumber);
    /// <summary>Fails with NotFound for an unknown project, Invalid when the parent breaks a subtask rule.</summary>
    Task<ItemResult<BacklogItemDto>> CreateAsync(Guid projectId, CreateBacklogItemDto dto, string createdBy);
    Task<BacklogItemDto?> UpdateAsync(Guid projectId, int itemNumber, UpdateBacklogItemDto dto);
    Task<BacklogItemDto?> UpdateStatusAsync(Guid projectId, int itemNumber, UpdateStatusDto dto);
    /// <summary>The item's non-archived subtasks in order; null if the item doesn't exist.</summary>
    Task<List<BacklogItemDto>?> GetSubtasksAsync(Guid projectId, int itemNumber);
    /// <summary>Makes the item a subtask of another item, or a top-level item when the parent is null.</summary>
    Task<ItemResult<BacklogItemDto>> SetParentAsync(Guid projectId, int itemNumber, SetParentDto dto);
    /// <summary>Fails with Invalid unless the new order lists each subtask exactly once.</summary>
    Task<ItemResult<List<BacklogItemDto>>> ReorderSubtasksAsync(Guid projectId, int itemNumber, ReorderSubtasksDto dto);
    /// <summary>Archives the item together with its open subtasks. Archiving an archived item changes nothing.</summary>
    Task<bool> ArchiveAsync(Guid projectId, int itemNumber);
    Task<List<BacklogItemDto>> GetArchivedByProjectAsync(Guid projectId);
    /// <summary>Restores the item and the subtasks archived with it.</summary>
    Task<BacklogItemDto?> UnarchiveAsync(Guid projectId, int itemNumber);
}
