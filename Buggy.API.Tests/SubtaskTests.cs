using Buggy.API.DTOs;
using Buggy.API.Models;
using Buggy.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Buggy.API.Tests;

[Collection(PostgresCollection.Name)]
public class SubtaskTests(PostgresFixture db)
{
    // ---- helpers: every call uses a fresh DbContext, so assertions see what was saved ----

    private async Task<T> Items<T>(Func<BacklogItemService, Task<T>> action)
    {
        await using var context = db.CreateContext();
        return await action(new BacklogItemService(context));
    }

    private Task<ItemResult<BacklogItemDto>> TryCreate(Guid projectId, string title, int? parent = null,
        BacklogItemStatus status = BacklogItemStatus.Backlog) =>
        Items(s => s.CreateAsync(projectId,
            new CreateBacklogItemDto(BacklogItemType.Task, title, null, Priority.None, status, null, null, parent),
            "Tester"));

    private async Task<BacklogItemDto> Create(Guid projectId, string title, int? parent = null,
        BacklogItemStatus status = BacklogItemStatus.Backlog) =>
        Ok(await TryCreate(projectId, title, parent, status));

    private Task<ItemResult<BacklogItemDto>> SetParent(Guid projectId, int number, int? parent) =>
        Items(s => s.SetParentAsync(projectId, number, new SetParentDto(parent)));

    private Task<ItemResult<List<BacklogItemDto>>> Reorder(Guid projectId, int number, params int[] order) =>
        Items(s => s.ReorderSubtasksAsync(projectId, number, new ReorderSubtasksDto([.. order])));

    private async Task<BacklogItemDto> Get(Guid projectId, int number) =>
        (await Items(s => s.GetByNumberAsync(projectId, number)))!;

    private Task<BacklogItemDto?> SetStatus(Guid projectId, int number, BacklogItemStatus status) =>
        Items(s => s.UpdateStatusAsync(projectId, number, new UpdateStatusDto(status)));

    private static T Ok<T>(ItemResult<T> result)
    {
        Assert.Equal(ItemError.None, result.Error);
        return result.Value!;
    }

    private static void AssertInvalid<T>(ItemResult<T> result, string? messagePart = null)
    {
        Assert.Equal(ItemError.Invalid, result.Error);
        if (messagePart != null) Assert.Contains(messagePart, result.Message);
    }

    // ---- creating ----

    [Fact]
    public async Task Creating_a_subtask_links_it_and_counts_it_on_the_parent()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Login page broken");

        var first = await Create(project, "Reproduce", parent: parent.ItemNumber);
        var second = await Create(project, "Fix cookie", parent: parent.ItemNumber);

        Assert.Equal(parent.Id, first.ParentId);
        Assert.Equal(parent.ItemNumber, first.ParentNumber);
        Assert.Equal("Login page broken", first.ParentTitle);
        Assert.True(second.SortOrder > first.SortOrder);

        var reloaded = await Get(project, parent.ItemNumber);
        Assert.Equal(2, reloaded.SubtaskCount);
        Assert.Equal(0, reloaded.SubtaskDoneCount);
        Assert.Null(reloaded.ParentId);
    }

    [Fact]
    public async Task Subtasks_are_one_level_deep()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var child = await Create(project, "Child", parent: parent.ItemNumber);

        AssertInvalid(await TryCreate(project, "Grandchild", parent: child.ItemNumber), "one level");
    }

    [Fact]
    public async Task Rejected_subtask_does_not_use_up_an_item_number()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        await Items(s => s.ArchiveAsync(project, parent.ItemNumber));

        AssertInvalid(await TryCreate(project, "Under archived", parent: parent.ItemNumber), "archived");
        AssertInvalid(await TryCreate(project, "Under missing", parent: 999), "does not exist");

        var next = await Create(project, "Next");
        Assert.Equal(parent.ItemNumber + 1, next.ItemNumber);
    }

    [Fact]
    public async Task Creating_in_an_unknown_project_is_not_found()
    {
        var result = await TryCreate(Guid.NewGuid(), "Nowhere");
        Assert.Equal(ItemError.NotFound, result.Error);
    }

    [Fact]
    public async Task Parent_must_be_in_the_same_project()
    {
        var projectA = await db.CreateProjectAsync();
        var projectB = await db.CreateProjectAsync();
        await Create(projectA, "A1");
        await Create(projectA, "A2");
        var b1 = await Create(projectB, "B1");

        // #2 exists in project A but not in B.
        AssertInvalid(await SetParent(projectB, b1.ItemNumber, 2), "does not exist");
    }

    // ---- linking and detaching ----

    [Fact]
    public async Task An_existing_item_can_be_linked_and_detached()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var item = await Create(project, "Loose item");

        var linked = Ok(await SetParent(project, item.ItemNumber, parent.ItemNumber));
        Assert.Equal(parent.Id, linked.ParentId);
        Assert.Equal(1, (await Get(project, parent.ItemNumber)).SubtaskCount);

        // Linking to the same parent again is a no-op, not an error.
        Ok(await SetParent(project, item.ItemNumber, parent.ItemNumber));

        var detached = Ok(await SetParent(project, item.ItemNumber, null));
        Assert.Null(detached.ParentId);
        Assert.Equal(0, (await Get(project, parent.ItemNumber)).SubtaskCount);
    }

    [Fact]
    public async Task Linking_rejects_self_and_items_that_have_subtasks()
    {
        var project = await db.CreateProjectAsync();
        var a = await Create(project, "A");
        var b = await Create(project, "B");
        await Create(project, "B's child", parent: b.ItemNumber);

        AssertInvalid(await SetParent(project, a.ItemNumber, a.ItemNumber), "own subtask");
        AssertInvalid(await SetParent(project, b.ItemNumber, a.ItemNumber), "subtasks of its own");
    }

    [Fact]
    public async Task An_archived_item_cannot_be_linked()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var item = await Create(project, "Archived");
        await Items(s => s.ArchiveAsync(project, item.ItemNumber));

        AssertInvalid(await SetParent(project, item.ItemNumber, parent.ItemNumber), "restore it first");
    }

    [Fact]
    public async Task Missing_item_is_not_found()
    {
        var project = await db.CreateProjectAsync();
        Assert.Equal(ItemError.NotFound, (await SetParent(project, 42, null)).Error);
        Assert.Equal(ItemError.NotFound, (await Reorder(project, 42)).Error);
        Assert.Null(await Items(s => s.GetSubtasksAsync(project, 42)));
    }

    // ---- listing, ordering, progress ----

    [Fact]
    public async Task Subtasks_can_be_reordered()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var s1 = await Create(project, "One", parent: parent.ItemNumber);
        var s2 = await Create(project, "Two", parent: parent.ItemNumber);
        var s3 = await Create(project, "Three", parent: parent.ItemNumber);

        var reordered = Ok(await Reorder(project, parent.ItemNumber, s3.ItemNumber, s1.ItemNumber, s2.ItemNumber));
        Assert.Equal(["Three", "One", "Two"], reordered.Select(x => x.Title));

        var listed = await Items(s => s.GetSubtasksAsync(project, parent.ItemNumber));
        Assert.Equal(["Three", "One", "Two"], listed!.Select(x => x.Title));
    }

    [Fact]
    public async Task Reorder_must_list_each_subtask_exactly_once()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var s1 = await Create(project, "One", parent: parent.ItemNumber);
        var s2 = await Create(project, "Two", parent: parent.ItemNumber);

        AssertInvalid(await Reorder(project, parent.ItemNumber, s1.ItemNumber));
        AssertInvalid(await Reorder(project, parent.ItemNumber, s1.ItemNumber, s1.ItemNumber, s2.ItemNumber));
        AssertInvalid(await Reorder(project, parent.ItemNumber, s1.ItemNumber, s2.ItemNumber, parent.ItemNumber));
    }

    [Fact]
    public async Task Done_count_follows_subtask_status_and_ignores_archived()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var s1 = await Create(project, "One", parent: parent.ItemNumber);
        var s2 = await Create(project, "Two", parent: parent.ItemNumber);
        var s3 = await Create(project, "Three", parent: parent.ItemNumber, status: BacklogItemStatus.Done);

        await SetStatus(project, s1.ItemNumber, BacklogItemStatus.Done);
        var p = await Get(project, parent.ItemNumber);
        Assert.Equal((3, 2), (p.SubtaskCount, p.SubtaskDoneCount));

        await Items(s => s.ArchiveAsync(project, s3.ItemNumber));
        p = await Get(project, parent.ItemNumber);
        Assert.Equal((2, 1), (p.SubtaskCount, p.SubtaskDoneCount));

        // The project list (the board) carries the same counts and parent info.
        var board = await Items(s => s.GetByProjectAsync(project));
        Assert.Equal(1, board.Single(i => i.ItemNumber == parent.ItemNumber).SubtaskDoneCount);
        Assert.Equal(parent.ItemNumber, board.Single(i => i.ItemNumber == s2.ItemNumber).ParentNumber);
    }

    // ---- archive and restore ----

    [Fact]
    public async Task Archiving_a_parent_archives_its_open_subtasks_and_restore_brings_back_only_those()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var kept = await Create(project, "Kept", parent: parent.ItemNumber);
        var done = await Create(project, "Done one", parent: parent.ItemNumber);
        var deletedEarlier = await Create(project, "Deleted earlier", parent: parent.ItemNumber);
        await SetStatus(project, done.ItemNumber, BacklogItemStatus.Done);
        await Items(s => s.ArchiveAsync(project, deletedEarlier.ItemNumber));

        await Items(s => s.ArchiveAsync(project, parent.ItemNumber));
        Assert.Empty(await Items(s => s.GetByProjectAsync(project)));

        var restored = await Items(s => s.UnarchiveAsync(project, parent.ItemNumber));
        Assert.Equal(2, restored!.SubtaskCount);
        Assert.Equal(1, restored.SubtaskDoneCount); // restored subtasks keep their status

        var board = await Items(s => s.GetByProjectAsync(project));
        Assert.Equal(
            new[] { parent.ItemNumber, kept.ItemNumber, done.ItemNumber }.Order(),
            board.Select(i => i.ItemNumber).Order());
        Assert.True((await Get(project, deletedEarlier.ItemNumber)).IsArchived);
    }

    [Fact]
    public async Task Archiving_an_archived_parent_again_still_restores_its_subtasks()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var child = await Create(project, "Child", parent: parent.ItemNumber);

        await Items(s => s.ArchiveAsync(project, parent.ItemNumber));
        Assert.True(await Items(s => s.ArchiveAsync(project, parent.ItemNumber)));
        await Items(s => s.UnarchiveAsync(project, parent.ItemNumber));

        Assert.False((await Get(project, child.ItemNumber)).IsArchived);
    }

    [Fact]
    public async Task Restoring_a_subtask_of_an_archived_parent_detaches_it()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var child = await Create(project, "Child", parent: parent.ItemNumber);
        await Items(s => s.ArchiveAsync(project, parent.ItemNumber));

        var restored = await Items(s => s.UnarchiveAsync(project, child.ItemNumber));

        Assert.False(restored!.IsArchived);
        Assert.Null(restored.ParentId);
        Assert.True((await Get(project, parent.ItemNumber)).IsArchived);
    }

    [Fact]
    public async Task Restoring_a_subtask_of_an_active_parent_keeps_the_link()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        var child = await Create(project, "Child", parent: parent.ItemNumber);
        await Items(s => s.ArchiveAsync(project, child.ItemNumber));

        var restored = await Items(s => s.UnarchiveAsync(project, child.ItemNumber));

        Assert.Equal(parent.Id, restored!.ParentId);
        Assert.Equal(1, (await Get(project, parent.ItemNumber)).SubtaskCount);
    }

    // ---- other ----

    [Fact]
    public async Task Update_returns_current_comment_count()
    {
        var project = await db.CreateProjectAsync();
        var item = await Create(project, "Item");
        await using (var context = db.CreateContext())
            await new CommentService(context).CreateAsync(item.Id, new CreateCommentDto("Hi"), "Tester");

        var updated = await Items(s => s.UpdateAsync(project, item.ItemNumber,
            new UpdateBacklogItemDto(BacklogItemType.Bug, "Renamed", null, Priority.High, "Hanne", null)));

        Assert.Equal(1, updated!.CommentCount);
        Assert.Equal(BacklogItemType.Bug, updated.Type);
    }

    [Fact]
    public async Task Deleting_a_project_removes_parents_and_subtasks_together()
    {
        var project = await db.CreateProjectAsync();
        var parent = await Create(project, "Parent");
        await Create(project, "Child", parent: parent.ItemNumber);

        await using var context = db.CreateContext();
        await context.Projects.Where(p => p.Id == project).ExecuteDeleteAsync();

        Assert.False(await context.BacklogItems.AnyAsync(b => b.ProjectId == project));
    }
}
