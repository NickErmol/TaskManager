using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class ConcurrencyTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task PutTask_WithStaleIfMatch_Returns409WithCurrentTaskBody()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var staleEtag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        // Someone else updates first — rotates xmin server-side.
        var first = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}",
            new { Title = "first writer", Description = (string?)null, Priority = "High", DueDate = (DateTimeOffset?)null }, staleEtag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replay with the stale etag → deterministic 409 carrying the CURRENT task.
        var second = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}",
            new { Title = "second writer", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null }, staleEtag);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var current = await second.Content.ReadFromJsonAsync<TaskDto>();
        current!.Title.Should().Be("first writer", "409 body must carry the current task so the SPA can refetch");
        $"\"{current.RowVersion}\"".Should().Be(first.Etag());
    }

    [Fact]
    public async Task TwoSequentialPutsWithSameETag_SecondGets409()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        object Body(string title) => new { Title = title, Description = (string?)null, Priority = "Medium", DueDate = (DateTimeOffset?)null };

        var r1 = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}", Body("writer A"), etag);
        var r2 = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}", Body("writer B"), etag);

        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
