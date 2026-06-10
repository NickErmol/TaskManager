using FluentValidation;
using TaskManager.Tasks.Application.Commands;

namespace TaskManager.Tasks.Application.Validators;

public class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand> { }
public class UpdateBoardCommandValidator : AbstractValidator<UpdateBoardCommand> { }
public class AddBoardMemberCommandValidator : AbstractValidator<AddBoardMemberCommand> { }
public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand> { }
public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand> { }
public class MoveTaskCommandValidator : AbstractValidator<MoveTaskCommand> { }
public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand> { }
public class EditCommentCommandValidator : AbstractValidator<EditCommentCommand> { }
public class CreateLabelCommandValidator : AbstractValidator<CreateLabelCommand> { }
