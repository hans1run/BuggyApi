using Buggy.API.DTOs;
using Buggy.API.Models;

namespace Buggy.API.Services;

public interface IBacklogItemService
{
    Task<List<BacklogItemDto>> GetByProjectAsync(Guid projectId, BacklogItemType? type = null, Priority? priority = null);
    Task<BacklogItemDto?> GetByNumberAsync(Guid projectId, int itemNumber);
    Task<BacklogItemDto> CreateAsync(Guid projectId, CreateBacklogItemDto dto, string createdBy);
    Task<BacklogItemDto?> UpdateAsync(Guid projectId, int itemNumber, UpdateBacklogItemDto dto);
    Task<BacklogItemDto?> UpdateStatusAsync(Guid projectId, int itemNumber, UpdateStatusDto dto);
    Task<bool> ArchiveAsync(Guid projectId, int itemNumber);
    Task<List<BacklogItemDto>> GetArchivedByProjectAsync(Guid projectId);
    Task<BacklogItemDto?> UnarchiveAsync(Guid projectId, int itemNumber);
}
