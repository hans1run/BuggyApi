using Buggy.API.Data;
using Buggy.API.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Buggy.API.Tests;

/// <summary>
/// One throwaway Postgres (Docker) per test run, with the real migrations applied.
/// Tests isolate themselves by creating their own project.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public BuggyDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BuggyDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

    public async Task<Guid> CreateProjectAsync()
    {
        await using var db = CreateContext();
        var project = new Project { Id = Guid.NewGuid(), Name = $"Test {Guid.NewGuid():N}" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
