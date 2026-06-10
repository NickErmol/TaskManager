using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Infrastructure.Http;

/// <summary>
/// Resolves users via Identity's GET /api/users/{id}. Identity validates JWTs on
/// that endpoint, so the call carries a service token minted with the shared
/// JWT_SECRET. Base address comes from IDENTITY_URL.
/// </summary>
public class IdentityUserDirectory(HttpClient http, ServiceTokenProvider tokens) : IUserDirectory
{
    private sealed record UserResponse(Guid Id, string Email, string DisplayName, string? AvatarUrl);

    public async Task<DirectoryUser?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetToken());

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(ct);
        return user is null ? null : new DirectoryUser(user.Email, user.DisplayName);
    }
}
