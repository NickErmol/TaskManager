using System.Net.Http.Json;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Integration;

public static class HttpHelpers
{
    /// <summary>Client acting as the given user (gateway-injected X-User-Id header, spec §4.3 authorization).</summary>
    public static HttpClient As(this TasksWebAppFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    public static Task<HttpResponseMessage> SendJsonWithIfMatch<T>(
        this HttpClient client, HttpMethod method, string url, T body, string etag)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return client.SendAsync(request);
    }

    public static string Etag(this HttpResponseMessage response)
        => response.Headers.ETag!.Tag; // includes surrounding quotes — pass back to If-Match as-is

    /// <summary>Board with one Owner, one Editor, one Viewer — created through the API.</summary>
    public static async Task<(Guid BoardId, Guid Owner, Guid Editor, Guid Viewer)> SeedBoardAsync(this TasksWebAppFactory factory)
    {
        var owner = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var client = factory.As(owner);

        var created = await client.PostAsJsonAsync("/api/boards", new { Name = Fake.F.Commerce.ProductName(), Description = "seeded" });
        created.EnsureSuccessStatusCode();
        var board = (await created.Content.ReadFromJsonAsync<BoardDto>())!;

        (await client.PostAsJsonAsync($"/api/boards/{board.Id}/members", new { MemberId = editor, Role = "Editor" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/boards/{board.Id}/members", new { MemberId = viewer, Role = "Viewer" })).EnsureSuccessStatusCode();
        return (board.Id, owner, editor, viewer);
    }

    public static async Task<TaskDto> SeedTaskAsync(this TasksWebAppFactory factory, Guid boardId, Guid asUser)
    {
        var response = await factory.As(asUser).PostAsJsonAsync("/api/tasks",
            new { BoardId = boardId, Title = Fake.F.Hacker.Phrase(), Description = (string?)null, Priority = "Medium", DueDate = (DateTimeOffset?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>())!;
    }
}
