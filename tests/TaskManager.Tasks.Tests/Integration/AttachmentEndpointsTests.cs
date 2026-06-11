using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
}
