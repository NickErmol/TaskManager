using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class CreateTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<CreateTaskCommand, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(CreateTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class UpdateTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateTaskCommand, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(UpdateTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class DeleteTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteTaskCommand, Result>
{
    public ValueTask<Result> Handle(DeleteTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class MoveTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<MoveTaskCommand, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(MoveTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class AssignTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<AssignTaskCommand, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(AssignTaskCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}
