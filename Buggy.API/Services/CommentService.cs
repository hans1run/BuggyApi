using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Services;

public class CommentService : ICommentService
{
    private readonly BuggyDbContext _db;

    public CommentService(BuggyDbContext db) => _db = db;

    public async Task<List<CommentDto>> GetByItemAsync(Guid backlogItemId)
    {
        return await _db.Comments
            .Where(c => c.BacklogItemId == backlogItemId)
            .OrderBy(c => c.CreatedDate)
            .Select(c => new CommentDto(c.Id, c.BacklogItemId, c.Text, c.CreatedBy, c.CreatedDate))
            .ToListAsync();
    }

    public async Task<CommentDto?> CreateAsync(Guid backlogItemId, CreateCommentDto dto, string createdBy)
    {
        var itemExists = await _db.BacklogItems.AnyAsync(b => b.Id == backlogItemId);
        if (!itemExists) return null;

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            BacklogItemId = backlogItemId,
            Text = dto.Text,
            CreatedBy = createdBy
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return new CommentDto(comment.Id, comment.BacklogItemId, comment.Text, comment.CreatedBy, comment.CreatedDate);
    }
}
