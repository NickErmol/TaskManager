using System.Net;
using TaskManager.Analytics.Application.Interfaces;

namespace TaskManager.Analytics.Infrastructure.Http;

/// <summary>
/// Calls Tasks GET /api/boards/{id} with the caller's X-User-Id (Tasks REST trusts the
/// gateway-style header). 200 ⇒ member; 403/404 ⇒ not a member. Base address from TASKS_URL.
/// </summary>
public class TasksBoardMembershipChecker(HttpClient http) : IBoardMembershipChecker
{
    public async Task<bool> IsMemberAsync(Guid boardId, Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/boards/{boardId}");
        request.Headers.Add("X-User-Id", userId.ToString());
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
