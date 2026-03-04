using Buggy.API.DTOs;

namespace Buggy.API.Services;

public interface ICommentService
{
    Task<List<CommentDto>> GetByItemAsync(Guid backlogItemId);
    Task<CommentDto?> CreateAsync(Guid backlogItemId, CreateCommentDto dto, string createdBy);
}
