namespace TaskManager.Analytics.Application.Interfaces;

/// <summary>
/// Verifies a caller's board membership by asking Tasks (the membership owner). Keeps
/// Analytics free of a membership table and of Identity coupling (spec §13.4).
/// </summary>
public interface IBoardMembershipChecker
{
    Task<bool> IsMemberAsync(Guid boardId, Guid userId, CancellationToken ct = default);
}
