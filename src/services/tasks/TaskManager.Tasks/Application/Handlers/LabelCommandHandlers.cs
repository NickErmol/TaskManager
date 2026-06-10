using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class CreateLabelCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
    public ValueTask<Result<LabelDto>> Handle(CreateLabelCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class DeleteLabelCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteLabelCommand, Result>
{
    public ValueTask<Result> Handle(DeleteLabelCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class AddLabelToTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddLabelToTaskCommand, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(AddLabelToTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class RemoveLabelFromTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<RemoveLabelFromTaskCommand, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(RemoveLabelFromTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}
