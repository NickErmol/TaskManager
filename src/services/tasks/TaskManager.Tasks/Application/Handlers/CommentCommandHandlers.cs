using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class AddCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<AddCommentCommand, Result<CommentDto>>
{
    public ValueTask<Result<CommentDto>> Handle(AddCommentCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class EditCommentCommandHandler(ITaskRepository tasks, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<EditCommentCommand, Result<CommentDto>>
{
    public ValueTask<Result<CommentDto>> Handle(EditCommentCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class DeleteCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteCommentCommand, Result>
{
    public ValueTask<Result> Handle(DeleteCommentCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}
