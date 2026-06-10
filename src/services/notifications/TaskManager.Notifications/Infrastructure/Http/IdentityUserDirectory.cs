using System.Net;
using System.Net.Http.Json;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Infrastructure.Http;

/// <summary>
/// Resolves users via Identity's GET /api/users/{id}. Runs on the §8 private service
/// network where X-User-Id headers are trusted, so the call authenticates as the
/// looked-up user itself. Base address comes from IDENTITY_URL.
/// </summary>
public class IdentityUserDirectory(HttpClient http) : IUserDirectory
{
    private sealed record UserResponse(Guid Id, string Email, string DisplayName, string? AvatarUrl);

    public async Task<DirectoryUser?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}");
        request.Headers.Add("X-User-Id", userId.ToString());

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(ct);
        return user is null ? null : new DirectoryUser(user.Email, user.DisplayName);
    }
}
