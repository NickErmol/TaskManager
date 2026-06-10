using TaskManager.Tasks.Application.Validators;

namespace TaskManager.Tasks.Tests.Unit;

public class CommandValidatorTests
{
    [Fact]
    public void CreateBoardCommandValidator_Validate_EmptyName_Fails()
        => new CreateBoardCommandValidator()
            .Validate(new CreateBoardCommand("", null, Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateBoardCommandValidator_Validate_NameTooLong_Fails()
        => new CreateBoardCommandValidator()
            .Validate(new CreateBoardCommand(new string('x', 101), null, Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void AddBoardMemberCommandValidator_Validate_InvalidRole_Fails()
        => new AddBoardMemberCommandValidator()
            .Validate(new AddBoardMemberCommand(Guid.NewGuid(), Guid.NewGuid(), "SuperAdmin", Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void AddBoardMemberCommandValidator_Validate_OwnerRole_Fails()
        => new AddBoardMemberCommandValidator()
            .Validate(new AddBoardMemberCommand(Guid.NewGuid(), Guid.NewGuid(), "Owner", Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateTaskCommandValidator_Validate_EmptyTitle_Fails()
        => new CreateTaskCommandValidator()
            .Validate(new CreateTaskCommand(Guid.NewGuid(), "", null, "Low", null, Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateTaskCommandValidator_Validate_InvalidPriority_Fails()
        => new CreateTaskCommandValidator()
            .Validate(new CreateTaskCommand(Guid.NewGuid(), "t", null, "Urgent", null, Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void MoveTaskCommandValidator_Validate_InvalidStatus_Fails()
        => new MoveTaskCommandValidator()
            .Validate(new MoveTaskCommand(Guid.NewGuid(), "Archived", 0, 0, Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void MoveTaskCommandValidator_Validate_NegativePosition_Fails()
        => new MoveTaskCommandValidator()
            .Validate(new MoveTaskCommand(Guid.NewGuid(), "Done", -1, 0, Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void AddCommentCommandValidator_Validate_EmptyBody_Fails()
        => new AddCommentCommandValidator()
            .Validate(new AddCommentCommand(Guid.NewGuid(), "", Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateLabelCommandValidator_Validate_BadColor_Fails()
        => new CreateLabelCommandValidator()
            .Validate(new CreateLabelCommand(Guid.NewGuid(), "bug", "red", Guid.NewGuid()))
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateLabelCommandValidator_Validate_ValidInput_Passes()
        => new CreateLabelCommandValidator()
            .Validate(new CreateLabelCommand(Guid.NewGuid(), "bug", "#4ade80", Guid.NewGuid()))
            .IsValid.Should().BeTrue();
}
