using FluentAssertions;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.ValueObjects;
using Xunit;

namespace TaskManager.Tasks.Tests.Unit;

public class AttachmentTests
{
    private static TaskItem NewTask() =>
        TaskItem.Create(Guid.NewGuid(), "T", Guid.NewGuid(), TaskPriority.Medium);

    [Fact]
    public void AddAttachment_appends_and_returns_it_without_bumping_rowversion()
    {
        var task = NewTask();
        var before = task.RowVersion;

        var att = task.AddAttachment("report.pdf", "application/pdf", 1234, "boards/x/tasks/y/z", Guid.NewGuid());

        task.Attachments.Should().ContainSingle().Which.Should().BeSameAs(att);
        att.FileName.Should().Be("report.pdf");
        att.SizeBytes.Should().Be(1234);
        task.RowVersion.Should().Be(before); // child collection — no parent concurrency bump
    }

    [Fact]
    public void RemoveAttachment_returns_true_when_present_false_when_absent()
    {
        var task = NewTask();
        var att = task.AddAttachment("a.png", "image/png", 10, "k", Guid.NewGuid());

        task.RemoveAttachment(att.Id).Should().BeTrue();
        task.Attachments.Should().BeEmpty();
        task.RemoveAttachment(att.Id).Should().BeFalse();
    }
}
