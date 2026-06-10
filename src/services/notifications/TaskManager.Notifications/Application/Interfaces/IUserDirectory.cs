namespace TaskManager.Notifications.Application.Interfaces;

public record DirectoryUser(string Email, string DisplayName);

/// <summary>Resolves user contact details (backed by the Identity service; events carry no PII).</summary>
public interface IUserDirectory
{
    Task<DirectoryUser?> GetUserAsync(Guid userId, CancellationToken ct = default);
}
