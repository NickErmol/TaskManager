using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Domain.Interfaces;

/// <summary>Filter for <see cref="ITaskRepository.QueryAsync"/>.</summary>
public record TaskFilterParams(
    Guid? BoardId = null,
    Guid? AssignedTo = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null,
    DateTimeOffset? DueBefore = null,
    int Limit = 200,
    Guid? MemberUserId = null); // restricts to boards where this user is a member (used when BoardId is absent)

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Hard-cap is 200 (spec §4.3 pagination policy). The bool indicates whether more rows
    /// matched than were returned, surfaced to callers as <c>X-Result-Truncated: true</c>.
    /// </summary>
    Task<(List<TaskItem> Results, bool Truncated)> QueryAsync(TaskFilterParams filter, CancellationToken ct = default);

    void Add(TaskItem task);
    void Remove(TaskItem task);

    /// <summary>Assigned, non-Done tasks with a due date inside (now, now+window]. Used by the deadline scanner.</summary>
    Task<List<TaskItem>> GetDueWithinAsync(TimeSpan window, CancellationToken ct = default);
}
