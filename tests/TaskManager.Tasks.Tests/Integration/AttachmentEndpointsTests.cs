using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Tasks.Application.Attachments;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class AttachmentEndpointsTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task Upload_then_download_round_trips()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        var client = factory.As(owner);

        using var form = new MultipartFormDataContent();
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "doc.pdf");

        var up = await client.PostAsync($"/api/tasks/{task.Id}/attachments", form);
        await up.ShouldBeAsync(HttpStatusCode.Created);
        var updated = await up.Content.ReadFromJsonAsync<TaskDto>();
        updated!.Attachments.Should().ContainSingle().Which.FileName.Should().Be("doc.pdf");

        var attId = updated.Attachments[0].Id;
        var down = await client.GetAsync($"/api/tasks/{task.Id}/attachments/{attId}/content");
        await down.ShouldBeAsync(HttpStatusCode.OK);
        down.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        (await down.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task Upload_rejects_oversize_file()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        using var form = new MultipartFormDataContent();
        var big = new byte[AttachmentPolicy.MaxBytes + 16];
        big[0] = 0x25; big[1] = 0x50; big[2] = 0x44; big[3] = 0x46; // %PDF
        var file = new ByteArrayContent(big);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "huge.pdf");

        var up = await factory.As(owner).PostAsync($"/api/tasks/{task.Id}/attachments", form);
        await up.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_rejects_disallowed_type()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "evil.exe");

        var up = await factory.As(owner).PostAsync($"/api/tasks/{task.Id}/attachments", form);
        await up.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_rejects_content_type_spoof()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("<html></html>"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // lies; bytes are not PNG
        form.Add(file, "file", "fake.png");

        var up = await factory.As(owner).PostAsync($"/api/tasks/{task.Id}/attachments", form);
        await up.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_forbidden_for_viewer()
    {
        var (boardId, owner, _, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "d.pdf");

        var up = await factory.As(viewer).PostAsync($"/api/tasks/{task.Id}/attachments", form);
        await up.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_can_download_but_nonmember_cannot()
    {
        var (boardId, owner, _, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        using var form = new MultipartFormDataContent();
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "d.pdf");
        var up = await factory.As(owner).PostAsync($"/api/tasks/{task.Id}/attachments", form);
        await up.ShouldBeAsync(HttpStatusCode.Created);
        var updated = await up.Content.ReadFromJsonAsync<TaskDto>();
        var attId = updated!.Attachments[0].Id;

        // Viewer (a board member) may download
        var asViewer = await factory.As(viewer).GetAsync($"/api/tasks/{task.Id}/attachments/{attId}/content");
        await asViewer.ShouldBeAsync(HttpStatusCode.OK);

        // A non-member cannot
        var stranger = Guid.NewGuid();
        var asStranger = await factory.As(stranger).GetAsync($"/api/tasks/{task.Id}/attachments/{attId}/content");
        await asStranger.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_removes_attachment()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, owner);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "d.pdf");
        var up = await factory.As(owner).PostAsync($"/api/tasks/{task.Id}/attachments", form);
        var updated = await up.Content.ReadFromJsonAsync<TaskDto>();
        var attId = updated!.Attachments[0].Id;

        var del = await factory.As(owner).DeleteAsync($"/api/tasks/{task.Id}/attachments/{attId}");
        await del.ShouldBeAsync(HttpStatusCode.OK);
        var afterDelete = await del.Content.ReadFromJsonAsync<TaskDto>();
        afterDelete!.Attachments.Should().BeEmpty();
    }
}
