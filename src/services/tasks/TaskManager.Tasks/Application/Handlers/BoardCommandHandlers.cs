using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class CreateBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<CreateBoardCommand, Result<BoardDto>>
{
    public ValueTask<Result<BoardDto>> Handle(CreateBoardCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class UpdateBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateBoardCommand, Result<BoardDto>>
{
    public ValueTask<Result<BoardDto>> Handle(UpdateBoardCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class DeleteBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteBoardCommand, Result>
{
    public ValueTask<Result> Handle(DeleteBoardCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class AddBoardMemberCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddBoardMemberCommand, Result<BoardDto>>
{
    public ValueTask<Result<BoardDto>> Handle(AddBoardMemberCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}

public class RemoveBoardMemberCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<RemoveBoardMemberCommand, Result>
{
    public ValueTask<Result> Handle(RemoveBoardMemberCommand cmd, CancellationToken ct)
        => throw new NotImplementedException();
}
