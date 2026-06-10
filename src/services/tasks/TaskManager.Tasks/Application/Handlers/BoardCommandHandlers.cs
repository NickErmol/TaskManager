using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class CreateBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<CreateBoardCommand, Result<BoardDto>>
{
    public async ValueTask<Result<BoardDto>> Handle(CreateBoardCommand cmd, CancellationToken ct)
    {
        var board = Board.Create(cmd.Name, cmd.UserId, cmd.Description);
        boards.Add(board);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(board));
    }
}

public class UpdateBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateBoardCommand, Result<BoardDto>>
{
    public async ValueTask<Result<BoardDto>> Handle(UpdateBoardCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can update the board");
        board.Update(cmd.Name, cmd.Description);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(board));
    }
}

public class DeleteBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteBoardCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteBoardCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can delete the board");
        boards.Remove(board);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class AddBoardMemberCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddBoardMemberCommand, Result<BoardDto>>
{
    public async ValueTask<Result<BoardDto>> Handle(AddBoardMemberCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can add members");
        if (!Enum.TryParse<BoardRole>(cmd.Role, true, out var role) || role == BoardRole.Owner)
            return Result.Fail("Role must be Editor or Viewer");
        var added = board.AddMember(cmd.MemberId, role);
        if (added.IsFailed) return added.ToResult<BoardDto>();
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(board));
    }
}

public class RemoveBoardMemberCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<RemoveBoardMemberCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveBoardMemberCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can remove members");
        var removed = board.RemoveMember(cmd.MemberId);
        if (removed.IsFailed) return removed;
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
