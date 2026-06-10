using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class BoardEndpointsTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task PostBoards_CreatesBoardWithOwnerMember()
    {
        var owner = Guid.NewGuid();
        var response = await factory.As(owner).PostAsJsonAsync("/api/boards", new { Name = "B1", Description = "d" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>();
        board!.OwnerId.Should().Be(owner);
        board.Members.Should().ContainSingle(m => m.UserId == owner && m.Role == "Owner");
    }

    [Fact]
    public async Task GetBoards_ReturnsOnlyBoardsWhereUserIsMember()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        await factory.SeedBoardAsync(); // someone else's board

        var boards = await factory.As(owner).GetFromJsonAsync<List<BoardDto>>("/api/boards");

        boards!.Should().ContainSingle(b => b.Id == boardId);
    }

    [Fact]
    public async Task GetBoard_AsNonMember_Returns403()
    {
        var (boardId, _, _, _) = await factory.SeedBoardAsync();

        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/boards/{boardId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBoard_Missing_Returns404()
    {
        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/boards/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBoard_AsMember_ReturnsTasksGroupedByStatus()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        await factory.SeedTaskAsync(boardId, editor);

        var detail = await factory.As(owner).GetFromJsonAsync<BoardDetailDto>($"/api/boards/{boardId}");

        detail!.TasksByStatus.Should().ContainKey("Todo");
        detail.TasksByStatus["Todo"].Should().HaveCount(1);
    }

    [Fact]
    public async Task PutBoard_AsEditor_Returns403()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor).PutAsJsonAsync($"/api/boards/{boardId}", new { Name = "x", Description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutBoard_AsOwner_UpdatesName()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        var response = await factory.As(owner).PutAsJsonAsync($"/api/boards/{boardId}", new { Name = "renamed", Description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BoardDto>())!.Name.Should().Be("renamed");
    }

    [Fact]
    public async Task DeleteBoard_AsEditor_Returns403()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor).DeleteAsync($"/api/boards/{boardId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteBoard_AsOwner_Returns204ThenGet404()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        var del = await factory.As(owner).DeleteAsync($"/api/boards/{boardId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await factory.As(owner).GetAsync($"/api/boards/{boardId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_AsNonOwner_Returns403()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor)
            .PostAsJsonAsync($"/api/boards/{boardId}/members", new { MemberId = Guid.NewGuid(), Role = "Viewer" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveMember_AsOwner_Returns204()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(owner).DeleteAsync($"/api/boards/{boardId}/members/{editor}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Labels_CreateListDelete_OwnerOnlyForWrite()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();

        var forbidden = await factory.As(editor)
            .PostAsJsonAsync($"/api/boards/{boardId}/labels", new { Name = "bug", Color = "#ff0000" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var created = await factory.As(owner)
            .PostAsJsonAsync($"/api/boards/{boardId}/labels", new { Name = "bug", Color = "#ff0000" });
        await created.ShouldBeAsync(HttpStatusCode.OK);
        var label = (await created.Content.ReadFromJsonAsync<LabelDto>())!;

        var list = await factory.As(editor).GetFromJsonAsync<List<LabelDto>>($"/api/boards/{boardId}/labels");
        list!.Should().ContainSingle(l => l.Id == label.Id);

        var deleted = await factory.As(owner).DeleteAsync($"/api/boards/{boardId}/labels/{label.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
