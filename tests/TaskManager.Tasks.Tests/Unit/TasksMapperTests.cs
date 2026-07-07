using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class TasksMapperTests
{
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public void ToDto_ProjectsChecklistOrderedByPosition()
    {
        var task = Fake.Task(Guid.NewGuid());
        var a = task.AddChecklistItem("first");
        var b = task.AddChecklistItem("second");
        b.SetDone(true);

        var dto = Mapper.ToDto(task);

        dto.Checklist.Should().HaveCount(2);
        dto.Checklist[0].Id.Should().Be(a.Id);
        dto.Checklist[0].Title.Should().Be("first");
        dto.Checklist[0].IsDone.Should().BeFalse();
        dto.Checklist[0].Position.Should().Be(0);
        dto.Checklist[1].Id.Should().Be(b.Id);
        dto.Checklist[1].IsDone.Should().BeTrue();
        dto.Checklist[1].Position.Should().Be(1);
    }

    [Fact]
    public void ToDto_includes_attachments()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "T", Guid.NewGuid(), TaskPriority.Medium);
        var att = task.AddAttachment("report.pdf", "application/pdf", 99, "k", Guid.NewGuid());

        var dto = Mapper.ToDto(task);

        dto.Attachments.Should().ContainSingle();
        dto.Attachments[0].Id.Should().Be(att.Id);
        dto.Attachments[0].FileName.Should().Be("report.pdf");
        dto.Attachments[0].SizeBytes.Should().Be(99);
    }
}
