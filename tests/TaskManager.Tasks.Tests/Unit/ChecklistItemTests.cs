using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class ChecklistItemTests
{
    [Fact]
    public void AddChecklistItem_AppendsWithIncrementingPosition_AndDoesNotBumpUpdatedAt()
    {
        var task = Fake.Task(Guid.NewGuid());
        var before = task.UpdatedAt;

        var first = task.AddChecklistItem("Write tests");
        var second = task.AddChecklistItem("Make them pass");

        task.Checklist.Should().HaveCount(2);
        first.Position.Should().Be(0);
        second.Position.Should().Be(1);
        first.IsDone.Should().BeFalse();
        first.Title.Should().Be("Write tests");
        // The defining invariant: checklist writes must NOT advance the concurrency token.
        task.UpdatedAt.Should().Be(before);
    }

    [Fact]
    public void SetDone_And_Rename_MutateTheItem()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("draft");

        item.SetDone(true);
        item.Rename("final draft");

        item.IsDone.Should().BeTrue();
        item.Title.Should().Be("final draft");
    }

    [Fact]
    public void RemoveChecklistItem_RemovesByIdAndReportsWhetherItRemovedAnything()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("temp");

        task.RemoveChecklistItem(item.Id).Should().BeTrue();
        task.Checklist.Should().BeEmpty();
        task.RemoveChecklistItem(Guid.NewGuid()).Should().BeFalse();
    }
}
