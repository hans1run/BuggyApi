namespace Buggy.API.DTOs;

public record ProjectDto(Guid Id, string Name, int ItemCounter, DateTime CreatedDate, bool IsArchived);
public record CreateProjectDto(string Name);
public record UpdateProjectDto(string Name);
