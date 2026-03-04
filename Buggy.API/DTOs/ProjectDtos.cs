using System.ComponentModel.DataAnnotations;

namespace Buggy.API.DTOs;

public record ProjectDto(Guid Id, string Name, int ItemCounter, DateTime CreatedDate, bool IsArchived);
public record CreateProjectDto([Required, MaxLength(100)] string Name);
public record UpdateProjectDto([Required, MaxLength(100)] string Name);
