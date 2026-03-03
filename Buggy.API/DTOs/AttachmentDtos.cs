namespace Buggy.API.DTOs;

public record AttachmentDto(Guid Id, Guid BacklogItemId, string FileName, string ContentType, long FileSize, DateTime CreatedDate);
