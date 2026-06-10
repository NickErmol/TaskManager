using System.Net;
using System.Net.Http.Json;
using MassTransit.Testing;
using TaskManager.Contracts.Events;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class TaskEndpointsTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task PostTasks_AsEditor_CreatesAndPublishesTaskCreatedEvent()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor).PostAsJsonAsync("/api/tasks",
            new { BoardId = boardId, Title = "Ship it", Description = (string?)null, Priority = "High", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = (await response.Content.ReadFromJsonAsync<TaskDto>())!;
        task.Status.Should().Be("Todo");
        (await factory.Harness.Published.Any<TaskCreatedEvent>(x => x.Context.Message.TaskId == task.Id))
            .Should().BeTrue("the outbox must deliver TaskCreatedEvent to the bus");
    }

    [Fact]
    public async Task PostTasks_AsViewer_Returns403()
    {
        var (boardId, _, _, viewer) = await factory.SeedBoardAsync();

        var response = await factory.As(viewer).PostAsJsonAsync("/api/tasks",
            new { BoardId = boardId, Title = "nope", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostTasks_MissingBoard_Returns404()
    {
        var response = await factory.As(Guid.NewGuid()).PostAsJsonAsync("/api/tasks",
            new { BoardId = Guid.NewGuid(), Title = "t", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTask_AsMember_ReturnsBodyAndETag()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var response = await factory.As(owner).GetAsync($"/api/tasks/{task.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTask_Missing_Returns404()
    {
        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/tasks/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTasks_FilterByBoard_ReturnsSeededTask()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var tasks = await factory.As(owner).GetFromJsonAsync<List<TaskDto>>($"/api/tasks?boardId={boardId}");

        tasks!.Should().ContainSingle(t => t.Id == task.Id);
    }

    [Fact]
    public async Task GetTasks_BoardFilterAsNonMember_Returns403()
    {
        var (boardId, _, _, _) = await factory.SeedBoardAsync();

        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/tasks?boardId={boardId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutTask_WithCurrentIfMatch_UpdatesAndRotatesETag()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var get = await client.GetAsync($"/api/tasks/{task.Id}");
        var etag = get.Etag();

        var response = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}",
            new { Title = "updated", Description = (string?)null, Priority = "Critical", DueDate = (DateTimeOffset?)null }, etag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Etag().Should().NotBe(etag, "xmin advances on every successful write");
        (await response.Content.ReadFromJsonAsync<TaskDto>())!.Title.Should().Be("updated");
    }

    [Fact]
    public async Task PutTask_WithoutIfMatch_Returns400()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var response = await factory.As(editor).PutAsJsonAsync($"/api/tasks/{task.Id}",
            new { Title = "x", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Move_PublishesTaskStatusChangedEvent()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        var response = await client.SendJsonWithIfMatch(HttpMethod.Post, $"/api/tasks/{task.Id}/move",
            new { NewStatus = "InProgress", Position = 1 }, etag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.Harness.Published.Any<TaskStatusChangedEvent>(x =>
                x.Context.Message.TaskId == task.Id && x.Context.Message.NewStatus == "InProgress"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Move_ToDone_AlsoPublishesTaskCompletedEvent()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        await client.SendJsonWithIfMatch(HttpMethod.Post, $"/api/tasks/{task.Id}/move", new { NewStatus = "Done", Position = 0 }, etag);

        (await factory.Harness.Published.Any<TaskCompletedEvent>(x => x.Context.Message.TaskId == task.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Assign_PublishesTaskAssignedEvent()
    {
        var (boardId, _, editor, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        var response = await client.SendJsonWithIfMatch(HttpMethod.Post, $"/api/tasks/{task.Id}/assign",
            new { AssigneeId = (Guid?)viewer }, etag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.Harness.Published.Any<TaskAssignedEvent>(x =>
                x.Context.Message.TaskId == task.Id && x.Context.Message.AssignedTo == viewer))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Comments_AddEditDelete_FullLifecycle()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);

        var added = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/comments", new { Body = "first" });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var comment = (await added.Content.ReadFromJsonAsync<CommentDto>())!;
        (await factory.Harness.Published.Any<TaskCommentAddedEvent>(x => x.Context.Message.CommentId == comment.Id))
            .Should().BeTrue();

        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();
        var edited = await client.SendJsonWithIfMatch(HttpMethod.Put,
            $"/api/tasks/{task.Id}/comments/{comment.Id}", new { Body = "edited" }, etag);
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        (await edited.Content.ReadFromJsonAsync<CommentDto>())!.Body.Should().Be("edited");

        var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}/comments/{comment.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TaskLabels_AddAndRemove()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var created = await factory.As(owner).PostAsJsonAsync($"/api/boards/{boardId}/labels", new { Name = "ui", Color = "#00ff00" });
        var label = (await created.Content.ReadFromJsonAsync<LabelDto>())!;
        var client = factory.As(editor);

        var add = await client.PostAsync($"/api/tasks/{task.Id}/labels/{label.Id}", null);
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        (await add.Content.ReadFromJsonAsync<TaskDto>())!.LabelIds.Should().Contain(label.Id);

        var remove = await client.DeleteAsync($"/api/tasks/{task.Id}/labels/{label.Id}");
        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        (await remove.Content.ReadFromJsonAsync<TaskDto>())!.LabelIds.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTask_AsViewer403_AsEditor204()
    {
        var (boardId, _, editor, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        (await factory.As(viewer).DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.As(editor).DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Endpoints_WithoutUserIdHeader_Return401()
    {
        var client = factory.CreateClient(); // no X-User-Id
        (await client.GetAsync("/api/boards")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/tasks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
