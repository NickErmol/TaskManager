using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §13.5: File attachments — upload, list, download, delete through the task detail dialog.
[Collection("E2E")]
public class AttachmentsTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Upload_appears_in_list_download_works_and_delete_removes_it()
    {
        // Arrange: fresh user + board + task
        var context = await fixture.Browser.NewContextAsync(new()
        {
            BaseURL = E2eConfig.FrontendUrl,
            AcceptDownloads = true,   // required for RunAndWaitForDownloadAsync
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);

        await Flows.RegisterAsync(page);
        var boardName = $"AttachBoard {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(page, boardName);
        await Flows.OpenBoardAsync(page, boardName);
        await Flows.CreateTaskAsync(page, "Task with file");

        // Open the task dialog
        await Flows.TaskCard(page, "Task with file").ClickAsync();
        var dialog = page.Locator("mat-dialog-container");

        // Create a temp file to upload
        var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"e2e-upload-{Guid.NewGuid():N}.txt");
        await System.IO.File.WriteAllTextAsync(tempFile, "hello e2e attachment");
        try
        {
            // Upload via the file input
            var fileInput = dialog.GetByTestId("attachment-input");
            await fileInput.SetInputFilesAsync(tempFile);

            // Wait for the attachment item to appear in the list
            var attachmentItem = dialog.Locator("[data-testid='attachment-item']");
            await Assertions.Expect(attachmentItem).ToBeVisibleAsync();
            var fileName = System.IO.Path.GetFileName(tempFile);
            await Assertions.Expect(attachmentItem).ToContainTextAsync(fileName);

            // Download: wrap the click in RunAndWaitForDownloadAsync because the component
            // creates a blob URL and calls link.click() — Playwright intercepts that as a download
            // when AcceptDownloads = true.
            var download = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await dialog.Locator("[data-testid='attachment-download']").First.ClickAsync();
            });
            download.SuggestedFilename.Should().Be(fileName);

            // Delete the attachment
            await dialog.Locator("[data-testid='attachment-delete']").First.ClickAsync();
            await Assertions.Expect(dialog.Locator("[data-testid='attachment-item']")).ToHaveCountAsync(0);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
            await context.DisposeAsync();
        }
    }
}
