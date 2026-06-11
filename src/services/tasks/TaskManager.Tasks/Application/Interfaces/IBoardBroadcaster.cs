using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Interfaces;

/// <summary>
/// Best-effort, fire-after-commit real-time fan-out to a board's SignalR group (spec §F3).
/// Deliberately NOT the durable RabbitMQ outbox: a missed frame self-heals on reload, which
/// is the right consistency class for ephemeral UI sync. The SignalR adapter lives in
/// Presentation so the Onion/architecture rules stay satisfied.
/// </summary>
public interface IBoardBroadcaster
{
    Task TaskUpsertedAsync(Guid boardId, TaskDto task, Guid actorId, CancellationToken ct = default);
    Task TaskDeletedAsync(Guid boardId, Guid taskId, Guid actorId, CancellationToken ct = default);
}
