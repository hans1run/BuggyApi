using System.ComponentModel.DataAnnotations;

namespace Buggy.API.DTOs;

public record CommentDto(Guid Id, Guid BacklogItemId, string Text, string CreatedBy, DateTime CreatedDate);
public record CreateCommentDto([Required, MaxLength(4000)] string Text);
