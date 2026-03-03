namespace Buggy.API.Models;

public enum BacklogItemType
{
    Bug,
    Feature,
    Task
}

public enum Priority
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum BacklogItemStatus
{
    Backlog,
    ToDo,
    InProgress,
    InReview,
    Done
}
