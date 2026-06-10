using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class TruncationCapTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task GetTasks_With201Matches_Returns200AndTruncationHeader()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
            for (var i = 0; i < 201; i++)
                db.Tasks.Add(Fake.Task(boardId, owner));
            await db.SaveChangesAsync();
        }

        var response = await factory.As(owner).GetAsync($"/api/tasks?boardId={boardId}");

        response.EnsureSuccessStatusCode();
        response.Headers.Should().ContainKey("X-Result-Truncated");
        response.Headers.GetValues("X-Result-Truncated").Should().ContainSingle(v => v == "true");
        (await response.Content.ReadFromJsonAsync<List<TaskDto>>())!.Should().HaveCount(200);
    }
}
