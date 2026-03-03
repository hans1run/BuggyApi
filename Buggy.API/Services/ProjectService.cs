using Buggy.API.Data;
using Buggy.API.DTOs;
using Buggy.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Services;

public class ProjectService : IProjectService
{
    private readonly BuggyDbContext _db;

    public ProjectService(BuggyDbContext db) => _db = db;

    public async Task<List<ProjectDto>> GetAllAsync()
    {
        return await _db.Projects
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectDto(p.Id, p.Name, p.ItemCounter, p.CreatedDate, p.IsArchived))
            .ToListAsync();
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id)
    {
        var p = await _db.Projects.FindAsync(id);
        return p == null ? null : new ProjectDto(p.Id, p.Name, p.ItemCounter, p.CreatedDate, p.IsArchived);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto)
    {
        var project = new Project { Id = Guid.NewGuid(), Name = dto.Name };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return new ProjectDto(project.Id, project.Name, project.ItemCounter, project.CreatedDate, project.IsArchived);
    }

    public async Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return null;
        project.Name = dto.Name;
        await _db.SaveChangesAsync();
        return new ProjectDto(project.Id, project.Name, project.ItemCounter, project.CreatedDate, project.IsArchived);
    }

    public async Task<bool> ArchiveAsync(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return false;
        project.IsArchived = true;
        await _db.SaveChangesAsync();
        return true;
    }
}
