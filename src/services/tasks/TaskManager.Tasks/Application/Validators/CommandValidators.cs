using FluentValidation;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Validators;

public class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
    public CreateBoardCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateBoardCommandValidator : AbstractValidator<UpdateBoardCommand>
{
    public UpdateBoardCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class AddBoardMemberCommandValidator : AbstractValidator<AddBoardMemberCommand>
{
    public AddBoardMemberCommandValidator()
        => RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<BoardRole>(r, true, out var role) && role != BoardRole.Owner)
            .WithMessage("Role must be Editor or Viewer");
}

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority)
            .Must(p => Enum.TryParse<TaskPriority>(p, true, out _))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical");
    }
}

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority)
            .Must(p => Enum.TryParse<TaskPriority>(p, true, out _))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical");
    }
}

public class MoveTaskCommandValidator : AbstractValidator<MoveTaskCommand>
{
    public MoveTaskCommandValidator()
    {
        RuleFor(x => x.NewStatus)
            .Must(s => Enum.TryParse<TaskStatus>(s, true, out _))
            .WithMessage("NewStatus must be one of: Todo, InProgress, Review, Done");
        RuleFor(x => x.Position).GreaterThanOrEqualTo(0);
    }
}

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
        => RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
}

public class EditCommentCommandValidator : AbstractValidator<EditCommentCommand>
{
    public EditCommentCommandValidator()
        => RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
}

public class CreateLabelCommandValidator : AbstractValidator<CreateLabelCommand>
{
    public CreateLabelCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("Color must be a valid hex string e.g. #4ade80");
    }
}
