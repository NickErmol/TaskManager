# Tasks Service (Step 3) Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish `TaskManager.Tasks` — Application, Infrastructure, Presentation layers plus the full Step 3a test suite — per spec §4.3/§5/§11 and `docs/superpowers/specs/2026-06-10-tasks-service-step3-design.md`.

**Architecture:** Onion (Domain → Application ← Infrastructure/Presentation), CQRS via martinothamar/Mediator, `Result<T>` (FluentResults) for all expected failures, MassTransit EF Core **outbox** for the 6 integration events. Optimistic concurrency uses the already-committed `uint RowVersion` (Postgres `xmin`) — the ETag/If-Match value is the uint as a quoted string (the spec's "base64 byte[]" wording predates the xmin adaptation).

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Mediator 3.0-preview, FluentResults, FluentValidation, MassTransit 8 (RabbitMQ + EF outbox), Serilog. Tests: xUnit, FluentAssertions, NSubstitute, Bogus, NetArchTest.Rules, Testcontainers (PostgreSql + RabbitMq), MassTransit test harness.

**Environment notes (read first):**
- Run everything from repo root `D:\work\Task Manager`. Shell is PowerShell.
- **No Docker on this machine.** Integration tests compile locally but only run in CI. Local verification = build + unit + architecture tests: use `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName!~Integration"`.
- The Tasks service aliases `TaskStatus` in `src/services/tasks/TaskManager.Tasks/GlobalUsings.cs` (conflicts with `System.Threading.Tasks.TaskStatus`). The test project needs the same alias (created in Task 3).
- Conventional Commits. Branch: `feature/tasks-service` (already checked out).
- Step 3a = Tasks 1–5 (tests red). Step 3b = Tasks 6–13 (make green). 3a includes *skeleton* handler classes (`throw new NotImplementedException()`) so the suite **compiles and fails red** — spec §5 requires "tests must compile and fail".

---

### Task 1: Domain additions needed by the Application layer

The committed Domain layer lacks a few members the handlers need. Pure domain code, no packages beyond FluentResults (already used by `Board`).

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Domain/Entities/Board.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Domain/Interfaces/IBoardRepository.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Domain/Interfaces/ITaskRepository.cs`
- Create: `src/services/tasks/TaskManager.Tasks/Domain/Exceptions/ConcurrencyConflictException.cs`

- [ ] **Step 1.1: Add label behaviors to `Board`**

In `Board.cs`, after the `GetRole` method, add:

```csharp
    public Result<Label> AddLabel(string name, string colorHex)
    {
        if (_labels.Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return Result.Fail("conflict: a label with this name already exists");
        var label = Label.Create(Id, name, colorHex);
        if (label.IsFailed) return label;
        _labels.Add(label.Value);
        return label;
    }

    public Result RemoveLabel(Guid labelId)
    {
        var label = _labels.FirstOrDefault(l => l.Id == labelId);
        if (label is null) return Result.Fail("not found: label");
        _labels.Remove(label);
        return Result.Ok();
    }
```

- [ ] **Step 1.2: Extend `IBoardRepository`**

Add to the interface body in `IBoardRepository.cs`:

```csharp
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the member's role on the board, or null when not a member (or board missing).</summary>
    Task<BoardRole?> GetMemberRoleAsync(Guid boardId, Guid userId, CancellationToken ct = default);
```

(Ensure `using TaskManager.Tasks.Domain.ValueObjects;` is present at the top of the file.)

- [ ] **Step 1.3: Extend `TaskFilterParams` and `ITaskRepository`**

In `ITaskRepository.cs`, replace the `TaskFilterParams` record with:

```csharp
public record TaskFilterParams(
    Guid? BoardId = null,
    Guid? AssignedTo = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null,
    DateTimeOffset? DueBefore = null,
    int Limit = 200,
    Guid? MemberUserId = null); // restricts to boards where this user is a member (used when BoardId is absent)
```

and add to the interface:

```csharp
    /// <summary>Assigned, non-Done tasks with a due date inside (now, now+window]. Used by the deadline scanner.</summary>
    Task<List<TaskItem>> GetDueWithinAsync(TimeSpan window, CancellationToken ct = default);
```

- [ ] **Step 1.4: Create `ConcurrencyConflictException`**

Create `src/services/tasks/TaskManager.Tasks/Domain/Exceptions/ConcurrencyConflictException.cs`:

```csharp
namespace TaskManager.Tasks.Domain.Exceptions;

/// <summary>
/// Thrown by the Infrastructure unit of work when EF Core detects an optimistic-concurrency
/// conflict (xmin mismatch). Application handlers catch this and map it to
/// <c>Result.Fail("conflict: ...")</c>, which Presentation turns into HTTP 409.
/// Lives in Domain so Application never references EF Core.
/// </summary>
public class ConcurrencyConflictException(string message, Exception? inner = null)
    : Exception(message, inner);
```

- [ ] **Step 1.5: Build and commit**

Run: `dotnet build SmartTaskManager.sln --no-restore`
Expected: Build succeeded (NU1902/NU1903 Scriban warnings are known noise).

```powershell
git add src/services/tasks
git commit -m "feat(tasks): extend Domain with label behaviors, repo members, concurrency exception"
```

---

### Task 2: Application contracts + skeleton handlers (3a scaffolding)

DTOs, command/query records, mapper, pipeline behaviors, **empty** validators, and handler skeletons that `throw new NotImplementedException()`. This is what lets the 3a test suite compile and run red. Behaviors and mapper are mechanical plumbing, included complete — they don't turn any handler test green.

**Files (all under `src/services/tasks/TaskManager.Tasks/Application/`):**
- Create: `DTOs/TaskDtos.cs`
- Create: `Mappers/TasksMapper.cs`
- Create: `Behaviors/ValidationBehavior.cs`
- Create: `Behaviors/LoggingBehavior.cs`
- Create: `Commands/BoardCommands.cs`, `Commands/TaskCommands.cs`, `Commands/CommentCommands.cs`, `Commands/LabelCommands.cs`
- Create: `Queries/TaskQueries.cs`
- Create: `Handlers/BoardCommandHandlers.cs`, `Handlers/TaskCommandHandlers.cs`, `Handlers/CommentCommandHandlers.cs`, `Handlers/LabelCommandHandlers.cs`, `Handlers/QueryHandlers.cs`
- Create: `Validators/CommandValidators.cs`
- Create: `Services/DeadlineScanner.cs`

- [ ] **Step 2.1: Create `DTOs/TaskDtos.cs`**

```csharp
namespace TaskManager.Tasks.Application.DTOs;

public record BoardMemberDto(Guid UserId, string Role, DateTimeOffset JoinedAt);

public record LabelDto(Guid Id, Guid BoardId, string Name, string Color);

public record CommentDto(Guid Id, Guid TaskId, Guid AuthorId, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

public record TaskDto(
    Guid Id, Guid BoardId, string Title, string? Description,
    string Status, string Priority, Guid CreatedBy, Guid? AssignedTo,
    DateTimeOffset? DueDate, int Position, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    uint RowVersion,
    IReadOnlyList<Guid> LabelIds,
    IReadOnlyList<CommentDto> Comments);

public record BoardDto(
    Guid Id, string Name, string? Description, Guid OwnerId, DateTimeOffset CreatedAt,
    IReadOnlyList<BoardMemberDto> Members);

public record BoardDetailDto(
    Guid Id, string Name, string? Description, Guid OwnerId, DateTimeOffset CreatedAt,
    IReadOnlyList<BoardMemberDto> Members,
    IReadOnlyList<LabelDto> Labels,
    IReadOnlyDictionary<string, IReadOnlyList<TaskDto>> TasksByStatus);

/// <summary>GET /api/tasks result. Truncated=true → endpoint sets X-Result-Truncated header (spec §4.3 pagination policy).</summary>
public record TasksPage(IReadOnlyList<TaskDto> Tasks, bool Truncated);
```

- [ ] **Step 2.2: Create `Mappers/TasksMapper.cs`**

Hand-written (registered as singleton). Every mapping here needs custom expressions (enum→string, `Color`→string, label-id projection), so a Mapperly partial would be all hand-written bodies anyway — Mapperly stays available for future flat maps.

```csharp
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Application.Mappers;

public class TasksMapper
{
    public BoardMemberDto ToDto(BoardMember m) => new(m.UserId, m.Role.ToString(), m.JoinedAt);

    public LabelDto ToDto(Label l) => new(l.Id, l.BoardId, l.Name, l.Color.Value);

    public CommentDto ToDto(TaskComment c) => new(c.Id, c.TaskId, c.AuthorId, c.Body, c.CreatedAt, c.EditedAt);

    public TaskDto ToDto(TaskItem t) => new(
        t.Id, t.BoardId, t.Title, t.Description,
        t.Status.ToString(), t.Priority.ToString(), t.CreatedBy, t.AssignedTo,
        t.DueDate, t.Position, t.CreatedAt, t.UpdatedAt, t.RowVersion,
        t.Labels.Select(l => l.LabelId).ToList(),
        t.Comments.OrderBy(c => c.CreatedAt).Select(ToDto).ToList());

    public BoardDto ToDto(Board b) => new(
        b.Id, b.Name, b.Description, b.OwnerId, b.CreatedAt,
        b.Members.Select(ToDto).ToList());

    public BoardDetailDto ToDetailDto(Board b) => new(
        b.Id, b.Name, b.Description, b.OwnerId, b.CreatedAt,
        b.Members.Select(ToDto).ToList(),
        b.Labels.Select(ToDto).ToList(),
        b.Tasks.GroupBy(t => t.Status.ToString())
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TaskDto>)g.OrderBy(t => t.Position).Select(ToDto).ToList()));
}
```

- [ ] **Step 2.3: Create the pipeline behaviors**

Copy the Identity behaviors verbatim with only the namespace changed:

```powershell
Copy-Item src/services/identity/TaskManager.Identity/Application/Behaviors/ValidationBehavior.cs src/services/tasks/TaskManager.Tasks/Application/Behaviors/ValidationBehavior.cs
Copy-Item src/services/identity/TaskManager.Identity/Application/Behaviors/LoggingBehavior.cs src/services/tasks/TaskManager.Tasks/Application/Behaviors/LoggingBehavior.cs
```

Then in both copied files change `namespace TaskManager.Identity.Application.Behaviors;` to `namespace TaskManager.Tasks.Application.Behaviors;` (single Edit per file; no other changes).

- [ ] **Step 2.4: Create command records**

`Commands/BoardCommands.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record CreateBoardCommand(string Name, string? Description, Guid UserId) : IRequest<Result<BoardDto>>;
public record UpdateBoardCommand(Guid BoardId, string Name, string? Description, Guid UserId) : IRequest<Result<BoardDto>>;
public record DeleteBoardCommand(Guid BoardId, Guid UserId) : IRequest<Result>;
public record AddBoardMemberCommand(Guid BoardId, Guid MemberId, string Role, Guid UserId) : IRequest<Result<BoardDto>>;
public record RemoveBoardMemberCommand(Guid BoardId, Guid MemberId, Guid UserId) : IRequest<Result>;
```

`Commands/TaskCommands.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record CreateTaskCommand(Guid BoardId, string Title, string? Description, string Priority, DateTimeOffset? DueDate, Guid UserId) : IRequest<Result<TaskDto>>;
public record UpdateTaskCommand(Guid TaskId, string Title, string? Description, string Priority, DateTimeOffset? DueDate, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<TaskDto>>;
public record DeleteTaskCommand(Guid TaskId, Guid UserId) : IRequest<Result>;
public record MoveTaskCommand(Guid TaskId, string NewStatus, int Position, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<TaskDto>>;
public record AssignTaskCommand(Guid TaskId, Guid? AssigneeId, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<TaskDto>>;
```

`Commands/CommentCommands.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record AddCommentCommand(Guid TaskId, string Body, Guid UserId) : IRequest<Result<CommentDto>>;
public record EditCommentCommand(Guid TaskId, Guid CommentId, string Body, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<CommentDto>>;
public record DeleteCommentCommand(Guid TaskId, Guid CommentId, Guid UserId) : IRequest<Result>;
```

`Commands/LabelCommands.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record CreateLabelCommand(Guid BoardId, string Name, string Color, Guid UserId) : IRequest<Result<LabelDto>>;
public record DeleteLabelCommand(Guid BoardId, Guid LabelId, Guid UserId) : IRequest<Result>;
public record AddLabelToTaskCommand(Guid TaskId, Guid LabelId, Guid UserId) : IRequest<Result<TaskDto>>;
public record RemoveLabelFromTaskCommand(Guid TaskId, Guid LabelId, Guid UserId) : IRequest<Result<TaskDto>>;
```

- [ ] **Step 2.5: Create `Queries/TaskQueries.cs`**

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Queries;

public record GetBoardsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<BoardDto>>>;
public record GetBoardQuery(Guid BoardId, Guid UserId) : IRequest<Result<BoardDetailDto>>;
public record GetTaskQuery(Guid TaskId, Guid UserId) : IRequest<Result<TaskDto>>;
public record GetTasksQuery(Guid? BoardId, Guid? AssignedTo, string? Status, string? Priority, DateTimeOffset? DueBefore, Guid UserId) : IRequest<Result<TasksPage>>;
```

- [ ] **Step 2.6: Create skeleton handlers**

All five handler files follow the same skeleton shape: constructor-injected dependencies (final ones — so unit tests written in Task 4 construct them with the right arguments), `Handle` throws. Full skeleton contents:

`Handlers/BoardCommandHandlers.cs`:

```csharp
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
```

`Handlers/TaskCommandHandlers.cs`:

```csharp
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
```

`Handlers/CommentCommandHandlers.cs`:

```csharp
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
```

`Handlers/LabelCommandHandlers.cs`:

```csharp
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
```

`Handlers/QueryHandlers.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class GetBoardsQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardsQuery, Result<IReadOnlyList<BoardDto>>>
{
    public ValueTask<Result<IReadOnlyList<BoardDto>>> Handle(GetBoardsQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}

public class GetBoardQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardQuery, Result<BoardDetailDto>>
{
    public ValueTask<Result<BoardDetailDto>> Handle(GetBoardQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}

public class GetTaskQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTaskQuery, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(GetTaskQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}

public class GetTasksQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTasksQuery, Result<TasksPage>>
{
    public ValueTask<Result<TasksPage>> Handle(GetTasksQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2.7: Create empty validators — `Validators/CommandValidators.cs`**

Empty rule sets: validator unit tests (Task 4) go red, Task 6 fills the rules.

```csharp
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
```

- [ ] **Step 2.8: Create `Services/DeadlineScanner.cs` (skeleton)**

```csharp
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Services;

/// <summary>
/// Publishes DeadlineApproachingEvent for assigned, non-Done tasks due within 24 h.
/// Invoked hourly by the Presentation-layer DeadlineWorker hosted service.
/// </summary>
public class DeadlineScanner(ITaskRepository tasks, IEventPublisher publisher, IUnitOfWork uow)
{
    public Task ScanAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2.9: Build and commit**

Run: `dotnet build SmartTaskManager.sln --no-restore`
Expected: Build succeeded.

```powershell
git add src/services/tasks
git commit -m "feat(tasks): add Application contracts and skeleton handlers for Step 3a"
```

---

### Task 3: Test scaffolding + architecture tests

**Files:**
- Create: `tests/TaskManager.Tasks.Tests/GlobalUsings.cs`
- Create: `tests/TaskManager.Tasks.Tests/TestData/Fake.cs`
- Create: `tests/TaskManager.Tasks.Tests/Architecture/OnionDependencyRuleTests.cs`

- [ ] **Step 3.1: Create `GlobalUsings.cs`**

```csharp
global using NSubstitute;
global using TaskManager.Tasks.Application.Commands;
global using TaskManager.Tasks.Application.DTOs;
global using TaskManager.Tasks.Application.Handlers;
global using TaskManager.Tasks.Application.Mappers;
global using TaskManager.Tasks.Application.Queries;
global using TaskManager.Tasks.Domain.Entities;
global using TaskManager.Tasks.Domain.Interfaces;
global using TaskManager.Tasks.Domain.ValueObjects;
// Same alias the production project uses — TaskStatus otherwise collides with System.Threading.Tasks.TaskStatus.
global using TaskStatus = TaskManager.Tasks.Domain.ValueObjects.TaskStatus;
```

- [ ] **Step 3.2: Create `TestData/Fake.cs`** (Bogus builders; spec §5 says avoid hardcoded fixtures)

```csharp
using Bogus;

namespace TaskManager.Tasks.Tests.TestData;

public static class Fake
{
    public static readonly Faker F = new();

    public static Board Board(Guid? ownerId = null)
        => Domain.Entities.Board.Create(F.Commerce.ProductName(), ownerId ?? Guid.NewGuid(), F.Lorem.Sentence());

    public static TaskItem Task(Guid boardId, Guid? createdBy = null,
        TaskPriority priority = TaskPriority.Medium, DateTimeOffset? dueDate = null)
        => TaskItem.Create(boardId, F.Hacker.Phrase(), createdBy ?? Guid.NewGuid(), priority, dueDate, F.Lorem.Sentence());
}
```

(Method names shadow the entity type names inside this file, hence the fully qualified `Domain.Entities.Board.Create` call. `global using TaskManager.Tasks.Domain.Entities;` from GlobalUsings covers `TaskItem`.)

- [ ] **Step 3.3: Create `Architecture/OnionDependencyRuleTests.cs`**

Mirrors the Identity fixture. The Tasks service has no AppUser-style exception, and additionally pins "Domain/Application reference no EF Core / MassTransit" since all layers share one assembly.

```csharp
using NetArchTest.Rules;

namespace TaskManager.Tasks.Tests.Architecture;

public class OnionDependencyRuleTests
{
    private static readonly System.Reflection.Assembly Asm = typeof(TaskItem).Assembly;

    [Fact]
    public void Domain_does_not_reference_outer_layers_or_infrastructure_packages()
    {
        var forbidden = new[]
        {
            "TaskManager.Tasks.Application",
            "TaskManager.Tasks.Infrastructure",
            "TaskManager.Tasks.Presentation",
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Tasks.Domain")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_Presentation()
    {
        var forbidden = new[]
        {
            "TaskManager.Tasks.Infrastructure",
            "TaskManager.Tasks.Presentation",
            "Microsoft.EntityFrameworkCore",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Tasks.Application")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    private static string BuildMessage(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
```

- [ ] **Step 3.4: Run architecture tests and commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Architecture"`
Expected: PASS (2 tests) — these are guards; they hold green throughout.

```powershell
git add tests/TaskManager.Tasks.Tests
git commit -m "test(tasks): add architecture fixture and test scaffolding (Step 3a)"
```

---

### Task 4: Unit tests (write, run, expect red)

**Files (all under `tests/TaskManager.Tasks.Tests/Unit/`):**
- Create: `BoardCommandHandlerTests.cs`
- Create: `TaskCommandHandlerTests.cs`
- Create: `CommentCommandHandlerTests.cs`
- Create: `LabelCommandHandlerTests.cs`
- Create: `QueryHandlerTests.cs`
- Create: `CommandValidatorTests.cs`
- Create: `DeadlineScannerTests.cs`

Conventions: naming `{Class}_{Method}_{Scenario}_{ExpectedResult}` (spec §5). `Result` failure categories are detected by message prefix (`not found:`, `forbidden:`, `conflict:`) matching `ResultExtensions.MapFailure` (Task 12) — same convention as the Identity service.

- [ ] **Step 4.1: Create `BoardCommandHandlerTests.cs`**

```csharp
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class BoardCommandHandlerTests
{
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public async Task CreateBoardCommandHandler_Handle_WithValidInput_ReturnsDtoWithOwnerMember()
    {
        var userId = Guid.NewGuid();
        var handler = new CreateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateBoardCommand("Sprint board", "desc", userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sprint board");
        result.Value.OwnerId.Should().Be(userId);
        result.Value.Members.Should().ContainSingle(m => m.UserId == userId && m.Role == "Owner");
        _boards.Received(1).Add(Arg.Any<Board>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateBoardCommandHandler_Handle_WhenBoardMissing_ReturnsNotFound()
    {
        _boards.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Board?)null);
        var handler = new UpdateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new UpdateBoardCommand(Guid.NewGuid(), "n", null, Guid.NewGuid()), default);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task UpdateBoardCommandHandler_Handle_WhenNotOwner_ReturnsForbidden()
    {
        var board = Fake.Board();
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new UpdateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new UpdateBoardCommand(board.Id, "new name", null, editor), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task UpdateBoardCommandHandler_Handle_WhenOwner_UpdatesAndSaves()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new UpdateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new UpdateBoardCommand(board.Id, "renamed", "d2", owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("renamed");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBoardCommandHandler_Handle_WhenNotOwner_ReturnsForbidden()
    {
        var board = Fake.Board();
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new DeleteBoardCommandHandler(_boards, _uow);

        var result = await handler.Handle(new DeleteBoardCommand(board.Id, editor), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
        _boards.DidNotReceive().Remove(Arg.Any<Board>());
    }

    [Fact]
    public async Task DeleteBoardCommandHandler_Handle_WhenOwner_RemovesBoard()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new DeleteBoardCommandHandler(_boards, _uow);

        var result = await handler.Handle(new DeleteBoardCommand(board.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        _boards.Received(1).Remove(board);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddBoardMemberCommandHandler_Handle_WhenOwner_AddsMember()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new AddBoardMemberCommandHandler(_boards, _uow, Mapper);
        var newMember = Guid.NewGuid();

        var result = await handler.Handle(new AddBoardMemberCommand(board.Id, newMember, "Editor", owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Members.Should().Contain(m => m.UserId == newMember && m.Role == "Editor");
    }

    [Fact]
    public async Task AddBoardMemberCommandHandler_Handle_WhenDuplicate_ReturnsConflict()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var member = Guid.NewGuid();
        board.AddMember(member, BoardRole.Viewer);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new AddBoardMemberCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new AddBoardMemberCommand(board.Id, member, "Editor", owner), default);

        result.Errors[0].Message.Should().StartWith("conflict");
    }

    [Fact]
    public async Task RemoveBoardMemberCommandHandler_Handle_RemovingOwner_ReturnsForbidden()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new RemoveBoardMemberCommandHandler(_boards, _uow);

        var result = await handler.Handle(new RemoveBoardMemberCommand(board.Id, owner, owner), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }
}
```

- [ ] **Step 4.2: Create `TaskCommandHandlerTests.cs`**

```csharp
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class TaskCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private static readonly TasksMapper Mapper = new();

    private void SetRole(Guid boardId, Guid userId, BoardRole? role)
        => _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(role);

    [Fact]
    public async Task CreateTaskCommandHandler_Handle_AsEditor_CreatesAndPublishesTaskCreatedEvent()
    {
        var boardId = Guid.NewGuid();
        var editor = Guid.NewGuid();
        _boards.ExistsAsync(boardId, Arg.Any<CancellationToken>()).Returns(true);
        SetRole(boardId, editor, BoardRole.Editor);
        var handler = new CreateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new CreateTaskCommand(boardId, "Ship it", null, "High", null, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Todo");
        result.Value.Priority.Should().Be("High");
        _tasks.Received(1).Add(Arg.Any<TaskItem>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskCreatedEvent>(e => e.BoardId == boardId && e.Title == "Ship it" && e.CreatedBy == editor),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTaskCommandHandler_Handle_AsViewer_ReturnsForbidden()
    {
        var boardId = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        _boards.ExistsAsync(boardId, Arg.Any<CancellationToken>()).Returns(true);
        SetRole(boardId, viewer, BoardRole.Viewer);
        var handler = new CreateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new CreateTaskCommand(boardId, "t", null, "Low", null, viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task CreateTaskCommandHandler_Handle_WhenBoardMissing_ReturnsNotFound()
    {
        _boards.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = new CreateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new CreateTaskCommand(Guid.NewGuid(), "t", null, "Low", null, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task UpdateTaskCommandHandler_Handle_WithMatchingRowVersion_Updates()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateTaskCommand(task.Id, "new title", "d", "Critical", null, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("new title");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTaskCommandHandler_Handle_WithStaleRowVersion_ReturnsConflict()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateTaskCommand(task.Id, "t", null, "Low", null, task.RowVersion + 1, editor), default);

        result.Errors[0].Message.Should().StartWith("conflict");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveTaskCommandHandler_Handle_PublishesTaskStatusChangedEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new MoveTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new MoveTaskCommand(task.Id, "InProgress", 3, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("InProgress");
        result.Value.Position.Should().Be(3);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskStatusChangedEvent>(e => e.OldStatus == "Todo" && e.NewStatus == "InProgress" && e.ChangedBy == editor),
            Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TaskCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveTaskCommandHandler_Handle_MoveToDone_AlsoPublishesTaskCompletedEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new MoveTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new MoveTaskCommand(task.Id, "Done", 0, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskCompletedEvent>(e => e.TaskId == task.Id && e.CompletedBy == editor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignTaskCommandHandler_Handle_PublishesTaskAssignedEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AssignTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AssignTaskCommand(task.Id, assignee, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedTo.Should().Be(assignee);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskAssignedEvent>(e => e.AssignedTo == assignee && e.AssignedBy == editor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignTaskCommandHandler_Handle_Unassign_DoesNotPublish()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AssignTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AssignTaskCommand(task.Id, null, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TaskAssignedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTaskCommandHandler_Handle_AsViewer_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var viewer = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, viewer, BoardRole.Viewer);
        var handler = new DeleteTaskCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteTaskCommand(task.Id, viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
        _tasks.DidNotReceive().Remove(Arg.Any<TaskItem>());
    }

    [Fact]
    public async Task DeleteTaskCommandHandler_Handle_AsEditor_Removes()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new DeleteTaskCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteTaskCommand(task.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        _tasks.Received(1).Remove(task);
    }
}
```


- [ ] **Step 4.3: Create `CommentCommandHandlerTests.cs`**

```csharp
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class CommentCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private static readonly TasksMapper Mapper = new();

    private void SetRole(Guid boardId, Guid userId, BoardRole? role)
        => _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(role);

    [Fact]
    public async Task AddCommentCommandHandler_Handle_AsEditor_AddsAndPublishesEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AddCommentCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AddCommentCommand(task.Id, "looks good", editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Be("looks good");
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskCommentAddedEvent>(e => e.TaskId == task.Id && e.AuthorId == editor && e.Body == "looks good"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCommentCommandHandler_Handle_AsViewer_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var viewer = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, viewer, BoardRole.Viewer);
        var handler = new AddCommentCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AddCommentCommand(task.Id, "hi", viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task EditCommentCommandHandler_Handle_ByAuthor_Edits()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "v1");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        var handler = new EditCommentCommandHandler(_tasks, _uow, Mapper);

        var result = await handler.Handle(new EditCommentCommand(task.Id, comment.Id, "v2", task.RowVersion, author), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Be("v2");
        result.Value.EditedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EditCommentCommandHandler_Handle_ByOtherUser_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var comment = task.AddComment(Guid.NewGuid(), "v1");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        var handler = new EditCommentCommandHandler(_tasks, _uow, Mapper);

        var result = await handler.Handle(new EditCommentCommand(task.Id, comment.Id, "v2", task.RowVersion, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task DeleteCommentCommandHandler_Handle_ByAuthor_Deletes()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "bye");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, author, BoardRole.Editor);
        var handler = new DeleteCommentCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteCommentCommand(task.Id, comment.Id, author), default);

        result.IsSuccess.Should().BeTrue();
        task.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCommentCommandHandler_Handle_MissingComment_ReturnsNotFound()
    {
        var task = Fake.Task(Guid.NewGuid());
        var user = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, user, BoardRole.Owner);
        var handler = new DeleteCommentCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteCommentCommand(task.Id, Guid.NewGuid(), user), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }
}
```

- [ ] **Step 4.4: Create `LabelCommandHandlerTests.cs`**

```csharp
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class LabelCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public async Task CreateLabelCommandHandler_Handle_AsOwner_CreatesLabel()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new CreateLabelCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateLabelCommand(board.Id, "bug", "#ff0000", owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Color.Should().Be("#ff0000");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLabelCommandHandler_Handle_AsEditor_ReturnsForbidden()
    {
        var board = Fake.Board();
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new CreateLabelCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateLabelCommand(board.Id, "bug", "#ff0000", editor), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task CreateLabelCommandHandler_Handle_WithInvalidColor_Fails()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new CreateLabelCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateLabelCommand(board.Id, "bug", "red", owner), default);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteLabelCommandHandler_Handle_AsOwner_Deletes()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var label = board.AddLabel("bug", "#ff0000").Value;
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new DeleteLabelCommandHandler(_boards, _uow);

        var result = await handler.Handle(new DeleteLabelCommand(board.Id, label.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        board.Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task AddLabelToTaskCommandHandler_Handle_AsEditor_AddsLabel()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        var label = board.AddLabel("bug", "#ff0000").Value;
        var task = Fake.Task(board.Id);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        _boards.GetMemberRoleAsync(board.Id, editor, Arg.Any<CancellationToken>()).Returns(BoardRole.Editor);
        var handler = new AddLabelToTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddLabelToTaskCommand(task.Id, label.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.LabelIds.Should().Contain(label.Id);
    }

    [Fact]
    public async Task AddLabelToTaskCommandHandler_Handle_LabelFromOtherBoard_ReturnsNotFound()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var task = Fake.Task(board.Id);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        _boards.GetMemberRoleAsync(board.Id, owner, Arg.Any<CancellationToken>()).Returns(BoardRole.Owner);
        var handler = new AddLabelToTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddLabelToTaskCommand(task.Id, Guid.NewGuid(), owner), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task RemoveLabelFromTaskCommandHandler_Handle_RemovesLabel()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var label = board.AddLabel("bug", "#ff0000").Value;
        var task = Fake.Task(board.Id);
        task.AddLabel(label.Id);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(board.Id, owner, Arg.Any<CancellationToken>()).Returns(BoardRole.Owner);
        var handler = new RemoveLabelFromTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new RemoveLabelFromTaskCommand(task.Id, label.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.LabelIds.Should().BeEmpty();
    }
}
```

- [ ] **Step 4.5: Create `QueryHandlerTests.cs`**

```csharp
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class QueryHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public async Task GetBoardsQueryHandler_Handle_ReturnsMemberBoards()
    {
        var userId = Guid.NewGuid();
        _boards.GetByMemberAsync(userId, Arg.Any<CancellationToken>()).Returns([Fake.Board(userId), Fake.Board(userId)]);
        var handler = new GetBoardsQueryHandler(_boards, Mapper);

        var result = await handler.Handle(new GetBoardsQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBoardQueryHandler_Handle_AsNonMember_ReturnsForbidden()
    {
        var board = Fake.Board();
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new GetBoardQueryHandler(_boards, Mapper);

        var result = await handler.Handle(new GetBoardQuery(board.Id, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task GetBoardQueryHandler_Handle_AsMember_ReturnsDetailShape()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new GetBoardQueryHandler(_boards, Mapper);

        var result = await handler.Handle(new GetBoardQuery(board.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TasksByStatus.Should().BeEmpty(); // no tasks seeded — grouping covered by integration tests
        result.Value.Members.Should().ContainSingle(m => m.Role == "Owner");
    }

    [Fact]
    public async Task GetTaskQueryHandler_Handle_AsNonMember_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var handler = new GetTaskQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTaskQuery(task.Id, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task GetTaskQueryHandler_Handle_MissingTask_ReturnsNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);
        var handler = new GetTaskQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTaskQuery(Guid.NewGuid(), Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_PassesThroughTruncationFlag()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(BoardRole.Viewer);
        _tasks.QueryAsync(Arg.Any<TaskFilterParams>(), Arg.Any<CancellationToken>())
            .Returns(([Fake.Task(boardId)], true));
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(boardId, null, null, null, null, userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Truncated.Should().BeTrue();
        result.Value.Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_BoardFilterAsNonMember_ReturnsForbidden()
    {
        var boardId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(boardId, null, null, null, null, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_NoBoardFilter_RestrictsToMemberBoards()
    {
        var userId = Guid.NewGuid();
        _tasks.QueryAsync(Arg.Any<TaskFilterParams>(), Arg.Any<CancellationToken>())
            .Returns((new List<TaskItem>(), false));
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(null, null, null, null, null, userId), default);

        result.IsSuccess.Should().BeTrue();
        await _tasks.Received(1).QueryAsync(
            Arg.Is<TaskFilterParams>(f => f.MemberUserId == userId && f.BoardId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_InvalidStatusFilter_Fails()
    {
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(null, null, "NotAStatus", null, null, Guid.NewGuid()), default);

        result.IsFailed.Should().BeTrue();
    }
}
```

- [ ] **Step 4.6: Create `CommandValidatorTests.cs`**

```csharp
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
```

- [ ] **Step 4.7: Create `DeadlineScannerTests.cs`**

```csharp
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Services;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class DeadlineScannerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task DeadlineScanner_ScanAsync_PublishesEventPerDueTask()
    {
        var boardId = Guid.NewGuid();
        var due1 = Fake.Task(boardId, dueDate: DateTimeOffset.UtcNow.AddHours(5));
        due1.Assign(Guid.NewGuid());
        var due2 = Fake.Task(boardId, dueDate: DateTimeOffset.UtcNow.AddHours(23));
        due2.Assign(Guid.NewGuid());
        _tasks.GetDueWithinAsync(TimeSpan.FromHours(24), Arg.Any<CancellationToken>()).Returns([due1, due2]);
        var scanner = new DeadlineScanner(_tasks, _publisher, _uow);

        await scanner.ScanAsync();

        await _publisher.Received(1).PublishAsync(
            Arg.Is<DeadlineApproachingEvent>(e => e.TaskId == due1.Id && e.AssignedTo == due1.AssignedTo!.Value),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<DeadlineApproachingEvent>(e => e.TaskId == due2.Id),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadlineScanner_ScanAsync_NoDueTasks_DoesNothing()
    {
        _tasks.GetDueWithinAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns([]);
        var scanner = new DeadlineScanner(_tasks, _publisher, _uow);

        await scanner.ScanAsync();

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<DeadlineApproachingEvent>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 4.8: Run the unit suite — expect RED — and commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Unit"`
Expected: FAIL. Handler tests throw `NotImplementedException`; validator tests fail on empty rule sets. Architecture tests stay green. **Do not fix anything** — red is the deliverable of Step 3a.

```powershell
git add tests/TaskManager.Tasks.Tests
git commit -m "test(tasks): add unit test suite for Step 3a (red)"
```


---

### Task 5: Integration tests (compile-verified locally, run in CI)

Covers all 22 endpoints + the event, concurrency, and truncation scenarios from spec §11 Step 3a. They reference `Program`, which exists (template); Task 12 replaces it. Locally these only need to **compile** — Docker is unavailable.

**Files:**
- Modify: `tests/TaskManager.Tasks.Tests/TaskManager.Tasks.Tests.csproj`
- Modify: `src/services/tasks/TaskManager.Tasks/Program.cs`
- Create: `tests/TaskManager.Tasks.Tests/Integration/TasksWebAppFactory.cs`
- Create: `tests/TaskManager.Tasks.Tests/Integration/HttpHelpers.cs`
- Create: `tests/TaskManager.Tasks.Tests/Integration/BoardEndpointsTests.cs`
- Create: `tests/TaskManager.Tasks.Tests/Integration/TaskEndpointsTests.cs`
- Create: `tests/TaskManager.Tasks.Tests/Integration/ConcurrencyTests.cs`
- Create: `tests/TaskManager.Tasks.Tests/Integration/TruncationCapTests.cs`

- [ ] **Step 5.1: Add the MassTransit test-harness package to the test project**

In `TaskManager.Tasks.Tests.csproj`, add to the `PackageReference` ItemGroup:

```xml
    <PackageReference Include="MassTransit" Version="8.3.4" />
```

(`AddMassTransitTestHarness` / `ITestHarness` live in the core MassTransit package.)

- [ ] **Step 5.2: Make `Program` visible to `WebApplicationFactory`**

Append this line to the end of `src/services/tasks/TaskManager.Tasks/Program.cs` (whatever its current template content is):

```csharp
public partial class Program;
```

- [ ] **Step 5.3: Create `Integration/TasksWebAppFactory.cs`**

```csharp
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace TaskManager.Tasks.Tests.Integration;

/// <summary>
/// Boots the Tasks service against real Postgres + RabbitMQ containers. The MassTransit test
/// harness wraps the bus so outbox-delivered events can be asserted via <see cref="Harness"/>.
/// OUTBOX_QUERY_DELAY_SECONDS=1 keeps outbox drain fast enough for test timeouts.
/// </summary>
public class TasksWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("tasks_db_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine")
        .Build();

    public ITestHarness Harness
    {
        get
        {
            var harness = Services.GetRequiredService<ITestHarness>();
            harness.TestTimeout = TimeSpan.FromSeconds(15);
            return harness;
        }
    }

    public async Task InitializeAsync()
        => await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("TASKS_DB_CONNECTION", _postgres.GetConnectionString());
        builder.UseSetting("RABBITMQ_URL", _rabbit.GetConnectionString());
        builder.UseSetting("OUTBOX_QUERY_DELAY_SECONDS", "1");
        // Wraps the bus already registered by AddTasksInfrastructure; transport becomes
        // the in-memory test transport, which is what Harness.Published observes.
        builder.ConfigureServices(services => services.AddMassTransitTestHarness());
    }
}

[CollectionDefinition("tasks-api")]
public class TasksApiCollection : ICollectionFixture<TasksWebAppFactory>;
```

- [ ] **Step 5.4: Create `Integration/HttpHelpers.cs`**

```csharp
using System.Net.Http.Json;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Integration;

public static class HttpHelpers
{
    /// <summary>Client acting as the given user (gateway-injected X-User-Id header, spec §4.3 authorization).</summary>
    public static HttpClient As(this TasksWebAppFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    public static Task<HttpResponseMessage> SendJsonWithIfMatch<T>(
        this HttpClient client, HttpMethod method, string url, T body, string etag)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return client.SendAsync(request);
    }

    public static string Etag(this HttpResponseMessage response)
        => response.Headers.ETag!.Tag; // includes surrounding quotes — pass back to If-Match as-is

    /// <summary>Board with one Owner, one Editor, one Viewer — created through the API.</summary>
    public static async Task<(Guid BoardId, Guid Owner, Guid Editor, Guid Viewer)> SeedBoardAsync(this TasksWebAppFactory factory)
    {
        var owner = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var client = factory.As(owner);

        var created = await client.PostAsJsonAsync("/api/boards", new { Name = Fake.F.Commerce.ProductName(), Description = "seeded" });
        created.EnsureSuccessStatusCode();
        var board = (await created.Content.ReadFromJsonAsync<BoardDto>())!;

        (await client.PostAsJsonAsync($"/api/boards/{board.Id}/members", new { MemberId = editor, Role = "Editor" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/boards/{board.Id}/members", new { MemberId = viewer, Role = "Viewer" })).EnsureSuccessStatusCode();
        return (board.Id, owner, editor, viewer);
    }

    public static async Task<TaskDto> SeedTaskAsync(this TasksWebAppFactory factory, Guid boardId, Guid asUser)
    {
        var response = await factory.As(asUser).PostAsJsonAsync("/api/tasks",
            new { BoardId = boardId, Title = Fake.F.Hacker.Phrase(), Description = (string?)null, Priority = "Medium", DueDate = (DateTimeOffset?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>())!;
    }
}
```

- [ ] **Step 5.5: Create `Integration/BoardEndpointsTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class BoardEndpointsTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task PostBoards_CreatesBoardWithOwnerMember()
    {
        var owner = Guid.NewGuid();
        var response = await factory.As(owner).PostAsJsonAsync("/api/boards", new { Name = "B1", Description = "d" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>();
        board!.OwnerId.Should().Be(owner);
        board.Members.Should().ContainSingle(m => m.UserId == owner && m.Role == "Owner");
    }

    [Fact]
    public async Task GetBoards_ReturnsOnlyBoardsWhereUserIsMember()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();
        await factory.SeedBoardAsync(); // someone else's board

        var boards = await factory.As(owner).GetFromJsonAsync<List<BoardDto>>("/api/boards");

        boards!.Should().ContainSingle(b => b.Id == boardId);
    }

    [Fact]
    public async Task GetBoard_AsNonMember_Returns403()
    {
        var (boardId, _, _, _) = await factory.SeedBoardAsync();

        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/boards/{boardId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBoard_Missing_Returns404()
    {
        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/boards/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBoard_AsMember_ReturnsTasksGroupedByStatus()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        await factory.SeedTaskAsync(boardId, editor);

        var detail = await factory.As(owner).GetFromJsonAsync<BoardDetailDto>($"/api/boards/{boardId}");

        detail!.TasksByStatus.Should().ContainKey("Todo");
        detail.TasksByStatus["Todo"].Should().HaveCount(1);
    }

    [Fact]
    public async Task PutBoard_AsEditor_Returns403()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor).PutAsJsonAsync($"/api/boards/{boardId}", new { Name = "x", Description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutBoard_AsOwner_UpdatesName()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        var response = await factory.As(owner).PutAsJsonAsync($"/api/boards/{boardId}", new { Name = "renamed", Description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BoardDto>())!.Name.Should().Be("renamed");
    }

    [Fact]
    public async Task DeleteBoard_AsEditor_Returns403()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor).DeleteAsync($"/api/boards/{boardId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteBoard_AsOwner_Returns204ThenGet404()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        var del = await factory.As(owner).DeleteAsync($"/api/boards/{boardId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await factory.As(owner).GetAsync($"/api/boards/{boardId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_AsNonOwner_Returns403()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor)
            .PostAsJsonAsync($"/api/boards/{boardId}/members", new { MemberId = Guid.NewGuid(), Role = "Viewer" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveMember_AsOwner_Returns204()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(owner).DeleteAsync($"/api/boards/{boardId}/members/{editor}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Labels_CreateListDelete_OwnerOnlyForWrite()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();

        var forbidden = await factory.As(editor)
            .PostAsJsonAsync($"/api/boards/{boardId}/labels", new { Name = "bug", Color = "#ff0000" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var created = await factory.As(owner)
            .PostAsJsonAsync($"/api/boards/{boardId}/labels", new { Name = "bug", Color = "#ff0000" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var label = (await created.Content.ReadFromJsonAsync<LabelDto>())!;

        var list = await factory.As(editor).GetFromJsonAsync<List<LabelDto>>($"/api/boards/{boardId}/labels");
        list!.Should().ContainSingle(l => l.Id == label.Id);

        var deleted = await factory.As(owner).DeleteAsync($"/api/boards/{boardId}/labels/{label.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
```

- [ ] **Step 5.6: Create `Integration/TaskEndpointsTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using MassTransit.Testing;
using TaskManager.Contracts.Events;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class TaskEndpointsTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task PostTasks_AsEditor_CreatesAndPublishesTaskCreatedEvent()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();

        var response = await factory.As(editor).PostAsJsonAsync("/api/tasks",
            new { BoardId = boardId, Title = "Ship it", Description = (string?)null, Priority = "High", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = (await response.Content.ReadFromJsonAsync<TaskDto>())!;
        task.Status.Should().Be("Todo");
        (await factory.Harness.Published.Any<TaskCreatedEvent>(x => x.Context.Message.TaskId == task.Id))
            .Should().BeTrue("the outbox must deliver TaskCreatedEvent to the bus");
    }

    [Fact]
    public async Task PostTasks_AsViewer_Returns403()
    {
        var (boardId, _, _, viewer) = await factory.SeedBoardAsync();

        var response = await factory.As(viewer).PostAsJsonAsync("/api/tasks",
            new { BoardId = boardId, Title = "nope", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostTasks_MissingBoard_Returns404()
    {
        var response = await factory.As(Guid.NewGuid()).PostAsJsonAsync("/api/tasks",
            new { BoardId = Guid.NewGuid(), Title = "t", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTask_AsMember_ReturnsBodyAndETag()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var response = await factory.As(owner).GetAsync($"/api/tasks/{task.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTask_Missing_Returns404()
    {
        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/tasks/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTasks_FilterByBoard_ReturnsSeededTask()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var tasks = await factory.As(owner).GetFromJsonAsync<List<TaskDto>>($"/api/tasks?boardId={boardId}");

        tasks!.Should().ContainSingle(t => t.Id == task.Id);
    }

    [Fact]
    public async Task GetTasks_BoardFilterAsNonMember_Returns403()
    {
        var (boardId, _, _, _) = await factory.SeedBoardAsync();

        var response = await factory.As(Guid.NewGuid()).GetAsync($"/api/tasks?boardId={boardId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutTask_WithCurrentIfMatch_UpdatesAndRotatesETag()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var get = await client.GetAsync($"/api/tasks/{task.Id}");
        var etag = get.Etag();

        var response = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}",
            new { Title = "updated", Description = (string?)null, Priority = "Critical", DueDate = (DateTimeOffset?)null }, etag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Etag().Should().NotBe(etag, "xmin advances on every successful write");
        (await response.Content.ReadFromJsonAsync<TaskDto>())!.Title.Should().Be("updated");
    }

    [Fact]
    public async Task PutTask_WithoutIfMatch_Returns400()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var response = await factory.As(editor).PutAsJsonAsync($"/api/tasks/{task.Id}",
            new { Title = "x", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Move_PublishesTaskStatusChangedEvent()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        var response = await client.SendJsonWithIfMatch(HttpMethod.Post, $"/api/tasks/{task.Id}/move",
            new { NewStatus = "InProgress", Position = 1 }, etag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.Harness.Published.Any<TaskStatusChangedEvent>(x =>
                x.Context.Message.TaskId == task.Id && x.Context.Message.NewStatus == "InProgress"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Move_ToDone_AlsoPublishesTaskCompletedEvent()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        await client.SendJsonWithIfMatch(HttpMethod.Post, $"/api/tasks/{task.Id}/move", new { NewStatus = "Done", Position = 0 }, etag);

        (await factory.Harness.Published.Any<TaskCompletedEvent>(x => x.Context.Message.TaskId == task.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Assign_PublishesTaskAssignedEvent()
    {
        var (boardId, _, editor, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        var response = await client.SendJsonWithIfMatch(HttpMethod.Post, $"/api/tasks/{task.Id}/assign",
            new { AssigneeId = (Guid?)viewer }, etag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.Harness.Published.Any<TaskAssignedEvent>(x =>
                x.Context.Message.TaskId == task.Id && x.Context.Message.AssignedTo == viewer))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Comments_AddEditDelete_FullLifecycle()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);

        var added = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/comments", new { Body = "first" });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var comment = (await added.Content.ReadFromJsonAsync<CommentDto>())!;
        (await factory.Harness.Published.Any<TaskCommentAddedEvent>(x => x.Context.Message.CommentId == comment.Id))
            .Should().BeTrue();

        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();
        var edited = await client.SendJsonWithIfMatch(HttpMethod.Put,
            $"/api/tasks/{task.Id}/comments/{comment.Id}", new { Body = "edited" }, etag);
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        (await edited.Content.ReadFromJsonAsync<CommentDto>())!.Body.Should().Be("edited");

        var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}/comments/{comment.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TaskLabels_AddAndRemove()
    {
        var (boardId, owner, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var created = await factory.As(owner).PostAsJsonAsync($"/api/boards/{boardId}/labels", new { Name = "ui", Color = "#00ff00" });
        var label = (await created.Content.ReadFromJsonAsync<LabelDto>())!;
        var client = factory.As(editor);

        var add = await client.PostAsync($"/api/tasks/{task.Id}/labels/{label.Id}", null);
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        (await add.Content.ReadFromJsonAsync<TaskDto>())!.LabelIds.Should().Contain(label.Id);

        var remove = await client.DeleteAsync($"/api/tasks/{task.Id}/labels/{label.Id}");
        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        (await remove.Content.ReadFromJsonAsync<TaskDto>())!.LabelIds.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTask_AsViewer403_AsEditor204()
    {
        var (boardId, _, editor, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        (await factory.As(viewer).DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.As(editor).DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Endpoints_WithoutUserIdHeader_Return401()
    {
        var client = factory.CreateClient(); // no X-User-Id
        (await client.GetAsync("/api/boards")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/tasks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 5.7: Create `Integration/ConcurrencyTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class ConcurrencyTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task PutTask_WithStaleIfMatch_Returns409WithCurrentTaskBody()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var staleEtag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        // Someone else updates first — rotates xmin server-side.
        var first = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}",
            new { Title = "first writer", Description = (string?)null, Priority = "High", DueDate = (DateTimeOffset?)null }, staleEtag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replay with the stale etag → deterministic 409 carrying the CURRENT task.
        var second = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}",
            new { Title = "second writer", Description = (string?)null, Priority = "Low", DueDate = (DateTimeOffset?)null }, staleEtag);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var current = await second.Content.ReadFromJsonAsync<TaskDto>();
        current!.Title.Should().Be("first writer", "409 body must carry the current task so the SPA can refetch");
        $"\"{current.RowVersion}\"".Should().Be(first.Etag());
    }

    [Fact]
    public async Task TwoSequentialPutsWithSameETag_SecondGets409()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etag = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        object Body(string title) => new { Title = title, Description = (string?)null, Priority = "Medium", DueDate = (DateTimeOffset?)null };

        var r1 = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}", Body("writer A"), etag);
        var r2 = await client.SendJsonWithIfMatch(HttpMethod.Put, $"/api/tasks/{task.Id}", Body("writer B"), etag);

        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

- [ ] **Step 5.8: Create `Integration/TruncationCapTests.cs`**

Seeds 201 tasks directly through the DbContext (API would need 201 requests).

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class TruncationCapTests(TasksWebAppFactory factory)
{
    [Fact]
    public async Task GetTasks_With201Matches_Returns200AndTruncationHeader()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
            for (var i = 0; i < 201; i++)
                db.Tasks.Add(Fake.Task(boardId, owner));
            await db.SaveChangesAsync();
        }

        var response = await factory.As(owner).GetAsync($"/api/tasks?boardId={boardId}");

        response.EnsureSuccessStatusCode();
        response.Headers.Should().ContainKey("X-Result-Truncated");
        response.Headers.GetValues("X-Result-Truncated").Should().ContainSingle(v => v == "true");
        (await response.Content.ReadFromJsonAsync<List<TaskDto>>())!.Should().HaveCount(200);
    }
}
```

Note: this file references `TaskManager.Tasks.Infrastructure.Persistence.TasksDbContext`, which doesn't exist until Task 11 — so from this commit until Task 11 the **test project does not compile**, which is the expected 3a red state for integration tests. The CI test job will fail on this branch until 3b completes; that is acceptable and mirrors "tests red" (do not merge before green).

- [ ] **Step 5.9: Verify the production project still builds, then commit 3a**

Run: `dotnet build src/services/tasks/TaskManager.Tasks --no-restore`
Expected: Build succeeded (the *test* project is expected red — don't build it here).

```powershell
git add tests/TaskManager.Tasks.Tests src/services/tasks/TaskManager.Tasks/Program.cs
git commit -m "test(tasks): add integration test suite for Step 3a (red until 3b)"
```


---

### Task 6: Implement validators (3b begins)

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Validators/CommandValidators.cs`

- [ ] **Step 6.1: Fill in the validation rules**

Replace the entire file body (keep the usings/namespace) with:

```csharp
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
```

(`TaskStatus` resolves via the project-wide alias in `GlobalUsings.cs`.)

- [ ] **Step 6.2: Run validator tests — green — and commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~CommandValidatorTests"`
Expected: PASS (12 tests). *If the test project still fails to compile because of the `TasksDbContext` reference from Task 5, that's expected — in that case verify with `dotnet build src/services/tasks/TaskManager.Tasks --no-restore` and defer test runs to Task 11+. Same caveat applies to Tasks 7–10 test runs.*

```powershell
git add src/services/tasks
git commit -m "feat(tasks): implement command validators"
```

> **Compile-order note for Tasks 6–10:** the integration tests reference Infrastructure types that arrive in Task 11, so `dotnet test` may not compile until then. If it doesn't, the per-task "run tests" steps become "run `dotnet build src/services/tasks/TaskManager.Tasks`", and the full unit suite is verified at Step 11.6. An alternative the executor may use: temporarily exclude `Integration/**` via `<Compile Remove="Integration/**/*.cs" />` in the test csproj, run unit tests per task, and remove the exclusion in Task 11. Prefer the temporary-exclusion route — it keeps the TDD feedback loop per task — and make sure the exclusion is **gone** before the 3b commit at the end of Task 11.

---

### Task 7: Implement board command + query handlers

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/BoardCommandHandlers.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/QueryHandlers.cs`

- [ ] **Step 7.1: Replace `BoardCommandHandlers.cs` with the implementation**

```csharp
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
```

- [ ] **Step 7.2: Replace `QueryHandlers.cs` with the implementation**

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class GetBoardsQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardsQuery, Result<IReadOnlyList<BoardDto>>>
{
    public async ValueTask<Result<IReadOnlyList<BoardDto>>> Handle(GetBoardsQuery query, CancellationToken ct)
    {
        var list = await boards.GetByMemberAsync(query.UserId, ct);
        return Result.Ok<IReadOnlyList<BoardDto>>(list.Select(mapper.ToDto).ToList());
    }
}

public class GetBoardQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardQuery, Result<BoardDetailDto>>
{
    public async ValueTask<Result<BoardDetailDto>> Handle(GetBoardQuery query, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(query.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(query.UserId) is null)
            return Result.Fail("forbidden: not a board member");
        return Result.Ok(mapper.ToDetailDto(board));
    }
}

public class GetTaskQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTaskQuery, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(GetTaskQuery query, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(query.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (await boards.GetMemberRoleAsync(task.BoardId, query.UserId, ct) is null)
            return Result.Fail("forbidden: not a board member");
        return Result.Ok(mapper.ToDto(task));
    }
}

public class GetTasksQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTasksQuery, Result<TasksPage>>
{
    public async ValueTask<Result<TasksPage>> Handle(GetTasksQuery query, CancellationToken ct)
    {
        TaskStatus? status = null;
        if (query.Status is not null)
        {
            if (!Enum.TryParse<TaskStatus>(query.Status, true, out var s))
                return Result.Fail("Status filter must be one of: Todo, InProgress, Review, Done");
            status = s;
        }

        TaskPriority? priority = null;
        if (query.Priority is not null)
        {
            if (!Enum.TryParse<TaskPriority>(query.Priority, true, out var p))
                return Result.Fail("Priority filter must be one of: Low, Medium, High, Critical");
            priority = p;
        }

        if (query.BoardId is not null
            && await boards.GetMemberRoleAsync(query.BoardId.Value, query.UserId, ct) is null)
            return Result.Fail("forbidden: not a board member");

        var filter = new TaskFilterParams(
            BoardId: query.BoardId,
            AssignedTo: query.AssignedTo,
            Status: status,
            Priority: priority,
            DueBefore: query.DueBefore,
            MemberUserId: query.BoardId is null ? query.UserId : null);

        var (items, truncated) = await tasks.QueryAsync(filter, ct);
        return Result.Ok(new TasksPage(items.Select(mapper.ToDto).ToList(), truncated));
    }
}
```

- [ ] **Step 7.3: Run, verify green, commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~BoardCommandHandlerTests|FullyQualifiedName~QueryHandlerTests"`
Expected: PASS (18 tests) — see the Task 6 compile-order note if the test project can't compile yet.

```powershell
git add src/services/tasks
git commit -m "feat(tasks): implement board command and query handlers"
```

---

### Task 8: Implement task command handlers

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/TaskCommandHandlers.cs`

- [ ] **Step 8.1: Replace `TaskCommandHandlers.cs` with the implementation**

Note the ordering inside each handler: **publish before `SaveChangesAsync`** — the MassTransit EF outbox stores publishes in the same transaction the save commits.

```csharp
using FluentResults;
using Mediator;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Exceptions;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

internal static class TaskAccess
{
    public const string Conflict = "conflict: task was modified by another request";
    public const string EditorRequired = "forbidden: requires Owner or Editor role on the board";

    public static bool CanEdit(BoardRole? role) => role is BoardRole.Owner or BoardRole.Editor;
}

public class CreateTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<CreateTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        if (!await boards.ExistsAsync(cmd.BoardId, ct)) return Result.Fail("not found: board");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(cmd.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (!Enum.TryParse<TaskPriority>(cmd.Priority, true, out var priority))
            return Result.Fail("Priority must be one of: Low, Medium, High, Critical");

        var task = TaskItem.Create(cmd.BoardId, cmd.Title, cmd.UserId, priority, cmd.DueDate, cmd.Description);
        tasks.Add(task);
        await publisher.PublishAsync(new TaskCreatedEvent(task.Id, task.BoardId, task.Title, task.CreatedBy, task.CreatedAt), ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class UpdateTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(UpdateTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);
        if (!Enum.TryParse<TaskPriority>(cmd.Priority, true, out var priority))
            return Result.Fail("Priority must be one of: Low, Medium, High, Critical");

        task.Update(cmd.Title, cmd.Description, priority, cmd.DueDate);
        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(task));
    }
}

public class DeleteTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteTaskCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        tasks.Remove(task);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class MoveTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<MoveTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(MoveTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);
        if (!Enum.TryParse<TaskStatus>(cmd.NewStatus, true, out var newStatus))
            return Result.Fail("NewStatus must be one of: Todo, InProgress, Review, Done");

        var oldStatus = task.Status;
        task.Move(newStatus, cmd.Position);

        await publisher.PublishAsync(new TaskStatusChangedEvent(
            task.Id, task.BoardId, task.Title, oldStatus.ToString(), newStatus.ToString(), cmd.UserId), ct);
        if (newStatus == TaskStatus.Done && oldStatus != TaskStatus.Done)
            await publisher.PublishAsync(new TaskCompletedEvent(
                task.Id, task.BoardId, task.Title, cmd.UserId, DateTimeOffset.UtcNow), ct);

        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(task));
    }
}

public class AssignTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<AssignTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(AssignTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);

        task.Assign(cmd.AssigneeId);
        if (cmd.AssigneeId is not null)
            await publisher.PublishAsync(new TaskAssignedEvent(
                task.Id, task.BoardId, task.Title, cmd.AssigneeId.Value, cmd.UserId, task.DueDate), ct);

        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(task));
    }
}
```

- [ ] **Step 8.2: Run, verify green, commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~TaskCommandHandlerTests"`
Expected: PASS (12 tests) — see the Task 6 compile-order note.

```powershell
git add src/services/tasks
git commit -m "feat(tasks): implement task command handlers with events and concurrency checks"
```

---

### Task 9: Implement comment + label handlers

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/CommentCommandHandlers.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/LabelCommandHandlers.cs`

- [ ] **Step 9.1: Replace `CommentCommandHandlers.cs`**

```csharp
using FluentResults;
using Mediator;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Exceptions;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class AddCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<AddCommentCommand, Result<CommentDto>>
{
    public async ValueTask<Result<CommentDto>> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        var comment = task.AddComment(cmd.UserId, cmd.Body);
        await publisher.PublishAsync(new TaskCommentAddedEvent(task.Id, task.BoardId, comment.Id, cmd.UserId, cmd.Body), ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(comment));
    }
}

public class EditCommentCommandHandler(ITaskRepository tasks, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<EditCommentCommand, Result<CommentDto>>
{
    public async ValueTask<Result<CommentDto>> Handle(EditCommentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        var comment = task.Comments.FirstOrDefault(c => c.Id == cmd.CommentId);
        if (comment is null) return Result.Fail("not found: comment");
        if (comment.AuthorId != cmd.UserId)
            return Result.Fail("forbidden: only the comment author can edit it");
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);

        comment.Edit(cmd.Body);
        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(comment));
    }
}

public class DeleteCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteCommentCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCommentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        var comment = task.Comments.FirstOrDefault(c => c.Id == cmd.CommentId);
        if (comment is null) return Result.Fail("not found: comment");

        var role = await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct);
        if (comment.AuthorId != cmd.UserId && role != BoardRole.Owner)
            return Result.Fail("forbidden: only the author or the board owner can delete a comment");

        task.RemoveComment(cmd.CommentId);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
```

- [ ] **Step 9.2: Replace `LabelCommandHandlers.cs`**

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class CreateLabelCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
    public async ValueTask<Result<LabelDto>> Handle(CreateLabelCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can create labels");
        var label = board.AddLabel(cmd.Name, cmd.Color);
        if (label.IsFailed) return label.ToResult<LabelDto>();
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(label.Value));
    }
}

public class DeleteLabelCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteLabelCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteLabelCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can delete labels");
        var removed = board.RemoveLabel(cmd.LabelId);
        if (removed.IsFailed) return removed;
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class AddLabelToTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddLabelToTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(AddLabelToTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        var board = await boards.GetByIdAsync(task.BoardId, ct);
        if (board is null || board.Labels.All(l => l.Id != cmd.LabelId))
            return Result.Fail("not found: label on this board");

        task.AddLabel(cmd.LabelId);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class RemoveLabelFromTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<RemoveLabelFromTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(RemoveLabelFromTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        task.RemoveLabel(cmd.LabelId);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}
```

- [ ] **Step 9.3: Run, verify green, commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~CommentCommandHandlerTests|FullyQualifiedName~LabelCommandHandlerTests"`
Expected: PASS (13 tests) — see the Task 6 compile-order note.

```powershell
git add src/services/tasks
git commit -m "feat(tasks): implement comment and label handlers"
```

---

### Task 10: Implement `DeadlineScanner`

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Services/DeadlineScanner.cs`

- [ ] **Step 10.1: Replace the `ScanAsync` body**

```csharp
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Services;

/// <summary>
/// Publishes DeadlineApproachingEvent for assigned, non-Done tasks due within 24 h.
/// Repository does the filtering; invoked hourly by the Presentation-layer DeadlineWorker.
/// </summary>
public class DeadlineScanner(ITaskRepository tasks, IEventPublisher publisher, IUnitOfWork uow)
{
    public async Task ScanAsync(CancellationToken ct = default)
    {
        var due = await tasks.GetDueWithinAsync(TimeSpan.FromHours(24), ct);
        if (due.Count == 0) return;

        foreach (var task in due)
            await publisher.PublishAsync(new DeadlineApproachingEvent(
                task.Id, task.BoardId, task.Title, task.AssignedTo!.Value, task.DueDate!.Value), ct);

        // Outbox: the publishes above are persisted by this save.
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 10.2: Run the full unit + architecture suite — all green — and commit**

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName!~Integration"`
Expected: PASS, 0 failures (≈60 tests) — see the Task 6 compile-order note; if exclusion was used, this is where unit-green is mandatory before moving on.

```powershell
git add src/services/tasks
git commit -m "feat(tasks): implement deadline scanner"
```


---

### Task 11: Infrastructure layer (DbContext, configs, repositories, messaging, migration)

**Files (under `src/services/tasks/TaskManager.Tasks/Infrastructure/`):**
- Create: `Persistence/TasksDbContext.cs`
- Create: `Persistence/TasksDbContextDesignTimeFactory.cs`
- Create: `Persistence/Configurations/BoardConfiguration.cs`
- Create: `Persistence/Configurations/BoardMemberConfiguration.cs`
- Create: `Persistence/Configurations/TaskItemConfiguration.cs`
- Create: `Persistence/Configurations/TaskCommentConfiguration.cs`
- Create: `Persistence/Configurations/TaskLabelConfiguration.cs`
- Create: `Persistence/Configurations/LabelConfiguration.cs`
- Create: `Persistence/Repositories/BoardRepository.cs`
- Create: `Persistence/Repositories/TaskRepository.cs`
- Create: `Messaging/MassTransitEventPublisher.cs`
- Create: `DependencyInjection.cs`
- Generated: `Persistence/Migrations/*` (via `dotnet ef`)

- [ ] **Step 11.1: Create `Persistence/TasksDbContext.cs`**

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Exceptions;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Persistence;

public class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TasksDbContext).Assembly);
        // MassTransit EF Core outbox tables (spec §4.3 reliable publishing)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await base.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Surface to Application without an EF Core dependency there.
            throw new ConcurrencyConflictException("task was modified by another request", ex);
        }
    }
}
```

- [ ] **Step 11.2: Create the entity configurations**

`Persistence/Configurations/BoardConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("boards");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(500);

        builder.HasMany(b => b.Members).WithOne().HasForeignKey(m => m.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Tasks).WithOne().HasForeignKey(t => t.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Labels).WithOne().HasForeignKey(l => l.BoardId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(b => b.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(b => b.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

`Persistence/Configurations/BoardMemberConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class BoardMemberConfiguration : IEntityTypeConfiguration<BoardMember>
{
    public void Configure(EntityTypeBuilder<BoardMember> builder)
    {
        builder.ToTable("board_members");
        builder.HasKey(m => new { m.BoardId, m.UserId });
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(m => m.UserId);
    }
}
```

`Persistence/Configurations/TaskItemConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

        // Npgsql maps a uint IsRowVersion property to the system xmin column.
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasMany(t => t.Comments).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Labels).WithOne().HasForeignKey(l => l.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => t.BoardId);
        builder.HasIndex(t => t.AssignedTo);
        builder.HasIndex(t => t.DueDate);
        builder.HasIndex(t => t.UpdatedAt);
    }
}
```

`Persistence/Configurations/TaskCommentConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("task_comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Body).HasMaxLength(2000).IsRequired();
        builder.HasIndex(c => c.TaskId);
    }
}
```

`Persistence/Configurations/TaskLabelConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class TaskLabelConfiguration : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        builder.ToTable("task_labels");
        builder.HasKey(tl => new { tl.TaskId, tl.LabelId });
    }
}
```

`Persistence/Configurations/LabelConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(50).IsRequired();

        // Color persists as a single column labels.color (spec §4.3 owned entity)
        builder.OwnsOne(l => l.Color, color =>
        {
            color.Property(c => c.Value).HasColumnName("color").HasMaxLength(7).IsRequired();
        });
    }
}
```

- [ ] **Step 11.3: Create the repositories**

`Persistence/Repositories/BoardRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Infrastructure.Persistence.Repositories;

public class BoardRepository(TasksDbContext db) : IBoardRepository
{
    public Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Boards
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Labels)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Board>> GetByMemberAsync(Guid userId, CancellationToken ct = default)
        => db.Boards
            .Include(b => b.Members)
            .Where(b => b.Members.Any(m => m.UserId == userId))
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => db.Boards.AnyAsync(b => b.Id == id, ct);

    public Task<BoardRole?> GetMemberRoleAsync(Guid boardId, Guid userId, CancellationToken ct = default)
        => db.Set<BoardMember>()
            .Where(m => m.BoardId == boardId && m.UserId == userId)
            .Select(m => (BoardRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    public void Add(Board board) => db.Boards.Add(board);
    public void Remove(Board board) => db.Boards.Remove(board);
}
```

`Persistence/Repositories/TaskRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Persistence.Repositories;

public class TaskRepository(TasksDbContext db) : ITaskRepository
{
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Tasks
            .Include(t => t.Comments)
            .Include(t => t.Labels)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<(List<TaskItem> Results, bool Truncated)> QueryAsync(TaskFilterParams filter, CancellationToken ct = default)
    {
        var query = db.Tasks.Include(t => t.Labels).AsQueryable();

        if (filter.BoardId is not null)
            query = query.Where(t => t.BoardId == filter.BoardId);
        if (filter.MemberUserId is not null)
            query = query.Where(t => db.Set<BoardMember>()
                .Any(m => m.BoardId == t.BoardId && m.UserId == filter.MemberUserId));
        if (filter.AssignedTo is not null)
            query = query.Where(t => t.AssignedTo == filter.AssignedTo);
        if (filter.Status is not null)
            query = query.Where(t => t.Status == filter.Status);
        if (filter.Priority is not null)
            query = query.Where(t => t.Priority == filter.Priority);
        if (filter.DueBefore is not null)
            query = query.Where(t => t.DueDate != null && t.DueDate <= filter.DueBefore);

        // Fetch cap+1 to detect truncation without a COUNT round-trip (spec §4.3 pagination policy).
        var page = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Take(filter.Limit + 1)
            .ToListAsync(ct);

        var truncated = page.Count > filter.Limit;
        if (truncated) page.RemoveAt(page.Count - 1);
        return (page, truncated);
    }

    public Task<List<TaskItem>> GetDueWithinAsync(TimeSpan window, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Add(window);
        return db.Tasks
            .Where(t => t.AssignedTo != null
                        && t.Status != TaskStatus.Done
                        && t.DueDate != null && t.DueDate > now && t.DueDate <= cutoff)
            .ToListAsync(ct);
    }

    public void Add(TaskItem task) => db.Tasks.Add(task);
    public void Remove(TaskItem task) => db.Tasks.Remove(task);
}
```

- [ ] **Step 11.4: Create `Messaging/MassTransitEventPublisher.cs`**

```csharp
using MassTransit;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Messaging;

/// <summary>
/// With the EF Core bus outbox enabled, IPublishEndpoint writes to the outbox table; the
/// delivery service forwards to RabbitMQ after the owning transaction commits.
/// </summary>
public class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        => publishEndpoint.Publish(@event, ct);
}
```

- [ ] **Step 11.5: Create `DependencyInjection.cs`**

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Infrastructure.Messaging;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Infrastructure.Persistence.Repositories;

namespace TaskManager.Tasks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTasksInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var connection = config["ConnectionStrings:TasksDb"]
                         ?? config["TASKS_DB_CONNECTION"]
                         ?? throw new InvalidOperationException("TASKS_DB_CONNECTION is not configured");

        services.AddDbContext<TasksDbContext>(opt =>
            opt.UseNpgsql(connection, npg => npg.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TasksDbContext>());
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        var rabbitUrl = config["RABBITMQ_URL"] ?? "rabbitmq://guest:guest@localhost:5672";
        var outboxQueryDelay = TimeSpan.FromSeconds(config.GetValue("OUTBOX_QUERY_DELAY_SECONDS", 10));

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<TasksDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = outboxQueryDelay;
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitUrl));
                ConfigureTopology(cfg);
            });
        });

        return services;
    }

    /// <summary>Spec §4.3: topic exchange "task-manager", routing key per event type.</summary>
    private static void ConfigureTopology(IRabbitMqBusFactoryConfigurator cfg)
    {
        MapEvent<TaskCreatedEvent>(cfg, "task.created");
        MapEvent<TaskAssignedEvent>(cfg, "task.assigned");
        MapEvent<TaskStatusChangedEvent>(cfg, "task.status-changed");
        MapEvent<TaskCompletedEvent>(cfg, "task.completed");
        MapEvent<TaskCommentAddedEvent>(cfg, "task.comment-added");
        MapEvent<DeadlineApproachingEvent>(cfg, "task.deadline-approaching");

        static void MapEvent<T>(IRabbitMqBusFactoryConfigurator cfg, string routingKey) where T : class
        {
            cfg.Message<T>(m => m.SetEntityName("task-manager"));
            cfg.Publish<T>(p => p.ExchangeType = ExchangeType.Topic);
            cfg.Send<T>(s => s.UseRoutingKeyFormatter(_ => routingKey));
        }
    }
}
```

(If `RabbitMQ.Client` / `ExchangeType` doesn't resolve, use the string literal `"topic"` instead and drop the using.)

- [ ] **Step 11.6: Create the design-time factory and generate the migration**

`Persistence/TasksDbContextDesignTimeFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Tasks.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` at design time — never at runtime.</summary>
public class TasksDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TasksDbContext>
{
    public TasksDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=tasks_db;Username=postgres;Password=postgres",
                npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;
        return new TasksDbContext(options);
    }
}
```

Then:

Run: `dotnet build src/services/tasks/TaskManager.Tasks --no-restore` → Build succeeded.
Run: `dotnet tool restore` (or `dotnet tool install --global dotnet-ef` if no manifest — check `dotnet ef --version` first; the Identity migration was generated on this machine, so the tool should resolve).
Run: `dotnet ef migrations add InitialCreate --project src/services/tasks/TaskManager.Tasks --output-dir Infrastructure/Persistence/Migrations`
Expected: migration + snapshot files created; migration includes tables `boards`, `board_members`, `tasks`, `task_comments`, `task_labels`, `labels`, plus the MassTransit `inbox_state`/`outbox_message`/`outbox_state` (PascalCase `InboxState` etc. is also fine — accept what the generator emits).

- [ ] **Step 11.7: Run the unit suite (now the whole test project compiles), verify, commit**

If the temporary `<Compile Remove="Integration/**/*.cs" />` exclusion from Task 6's note was used, **remove it now**.

Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName!~Integration"`
Expected: PASS, 0 failures.

```powershell
git add src/services/tasks tests/TaskManager.Tasks.Tests
git commit -m "feat(tasks): implement Infrastructure layer with EF Core, repositories and MassTransit outbox"
```

---

### Task 12: Presentation layer (endpoints, middleware, hosted service, Program)

**Files (under `src/services/tasks/TaskManager.Tasks/` unless noted):**
- Create: `Presentation/Extensions/ResultExtensions.cs`
- Create: `Presentation/Extensions/HttpContextExtensions.cs`
- Create: `Presentation/Middleware/ExceptionHandlingMiddleware.cs`
- Create: `Presentation/Endpoints/BoardEndpoints.cs`
- Create: `Presentation/Endpoints/TaskEndpoints.cs`
- Create: `Presentation/Background/DeadlineWorker.cs`
- Replace: `Program.cs`
- Create/verify: `appsettings.Development.json`

- [ ] **Step 12.1: Create `Presentation/Extensions/ResultExtensions.cs`**

Copy of the Identity version (message-prefix convention) with the Tasks namespace:

```csharp
using FluentResults;

namespace TaskManager.Tasks.Presentation.Extensions;

/// <summary>
/// Maps Result failures to HTTP status codes via message prefix
/// ("not found:", "unauthorized:", "forbidden:", "conflict:") — spec §4.3 ToHttpResult convention.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        return MapFailure(result.Errors);
    }

    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess) return Results.NoContent();
        return MapFailure(result.Errors);
    }

    private static IResult MapFailure(IReadOnlyCollection<IError> errors)
    {
        var first = errors.FirstOrDefault()?.Message ?? "request failed";
        var lower = first.ToLowerInvariant();

        if (lower.StartsWith("not found"))
            return Results.NotFound(new { error = first });
        if (lower.StartsWith("unauthorized"))
            return Results.Json(new { error = first }, statusCode: StatusCodes.Status401Unauthorized);
        if (lower.StartsWith("forbidden"))
            return Results.Json(new { error = first }, statusCode: StatusCodes.Status403Forbidden);
        if (lower.StartsWith("conflict"))
            return Results.Conflict(new { error = first });
        return Results.BadRequest(new { errors = errors.Select(e => e.Message) });
    }
}
```

- [ ] **Step 12.2: Create `Presentation/Extensions/HttpContextExtensions.cs`**

```csharp
namespace TaskManager.Tasks.Presentation.Extensions;

public static class HttpContextExtensions
{
    /// <summary>Gateway-injected user id (spec §4.3 authorization: gateway validates JWT, forwards X-User-Id).</summary>
    public static Guid? GetUserId(this HttpContext http)
        => Guid.TryParse(http.Request.Headers["X-User-Id"], out var id) ? id : null;

    /// <summary>If-Match carries the uint RowVersion (xmin) as a quoted string, e.g. "42".</summary>
    public static bool TryGetIfMatch(this HttpContext http, out uint rowVersion)
    {
        rowVersion = 0;
        var raw = http.Request.Headers.IfMatch.ToString().Trim().Trim('"');
        return uint.TryParse(raw, out rowVersion);
    }

    public static void SetETag(this HttpContext http, uint rowVersion)
        => http.Response.Headers.ETag = $"\"{rowVersion}\"";
}
```

- [ ] **Step 12.3: Create `Presentation/Middleware/ExceptionHandlingMiddleware.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Tasks.Presentation.Middleware;

/// <summary>Genuine bugs only — expected domain failures travel as Result (never thrown).</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            });
        }
    }
}
```

- [ ] **Step 12.4: Create `Presentation/Endpoints/BoardEndpoints.cs`**

```csharp
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Presentation.Extensions;

namespace TaskManager.Tasks.Presentation.Endpoints;

public record CreateBoardRequest(string Name, string? Description);
public record UpdateBoardRequest(string Name, string? Description);
public record AddMemberRequest(Guid MemberId, string Role);
public record CreateLabelRequest(string Name, string Color);

public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/boards");

        group.MapGet("/", async (HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new GetBoardsQuery(userId), ct)).ToHttpResult();
        });

        group.MapPost("/", async (CreateBoardRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new CreateBoardCommand(req.Name, req.Description, userId), ct)).ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new GetBoardQuery(id, userId), ct)).ToHttpResult();
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateBoardRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new UpdateBoardCommand(id, req.Name, req.Description, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteBoardCommand(id, userId), ct)).ToHttpResult();
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new AddBoardMemberCommand(id, req.MemberId, req.Role, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}/members/{memberId:guid}", async (Guid id, Guid memberId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new RemoveBoardMemberCommand(id, memberId, userId), ct)).ToHttpResult();
        });

        group.MapGet("/{id:guid}/labels", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var board = await mediator.Send(new GetBoardQuery(id, userId), ct);
            return board.IsSuccess ? Results.Ok(board.Value.Labels) : board.ToHttpResult();
        });

        group.MapPost("/{id:guid}/labels", async (Guid id, CreateLabelRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new CreateLabelCommand(id, req.Name, req.Color, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}/labels/{labelId:guid}", async (Guid id, Guid labelId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteLabelCommand(id, labelId, userId), ct)).ToHttpResult();
        });
    }
}
```

- [ ] **Step 12.5: Create `Presentation/Endpoints/TaskEndpoints.cs`**

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Presentation.Extensions;

namespace TaskManager.Tasks.Presentation.Endpoints;

public record CreateTaskRequest(Guid BoardId, string Title, string? Description, string Priority, DateTimeOffset? DueDate);
public record UpdateTaskRequest(string Title, string? Description, string Priority, DateTimeOffset? DueDate);
public record MoveTaskRequest(string NewStatus, int Position);
public record AssignTaskRequest(Guid? AssigneeId);
public record CommentRequest(string Body);

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks");

        group.MapGet("/", async (Guid? boardId, Guid? assignedTo, string? status, string? priority, DateTimeOffset? dueBefore,
            HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new GetTasksQuery(boardId, assignedTo, status, priority, dueBefore, userId), ct);
            if (result.IsFailed) return result.ToHttpResult();
            if (result.Value.Truncated) http.Response.Headers["X-Result-Truncated"] = "true";
            return Results.Ok(result.Value.Tasks);
        });

        group.MapPost("/", async (CreateTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new CreateTaskCommand(req.BoardId, req.Title, req.Description, req.Priority, req.DueDate, userId), ct);
            return TaskResult(http, result);
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new GetTaskQuery(id, userId), ct);
            return TaskResult(http, result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            var result = await mediator.Send(new UpdateTaskCommand(id, req.Title, req.Description, req.Priority, req.DueDate, rowVersion, userId), ct);
            return await TaskResultWithConflictBody(http, mediator, id, userId, result, ct);
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteTaskCommand(id, userId), ct)).ToHttpResult();
        });

        group.MapPost("/{id:guid}/move", async (Guid id, MoveTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            var result = await mediator.Send(new MoveTaskCommand(id, req.NewStatus, req.Position, rowVersion, userId), ct);
            return await TaskResultWithConflictBody(http, mediator, id, userId, result, ct);
        });

        group.MapPost("/{id:guid}/assign", async (Guid id, AssignTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            var result = await mediator.Send(new AssignTaskCommand(id, req.AssigneeId, rowVersion, userId), ct);
            return await TaskResultWithConflictBody(http, mediator, id, userId, result, ct);
        });

        group.MapPost("/{id:guid}/comments", async (Guid id, CommentRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new AddCommentCommand(id, req.Body, userId), ct)).ToHttpResult();
        });

        group.MapPut("/{id:guid}/comments/{commentId:guid}", async (Guid id, Guid commentId, CommentRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            return (await mediator.Send(new EditCommentCommand(id, commentId, req.Body, rowVersion, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}/comments/{commentId:guid}", async (Guid id, Guid commentId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteCommentCommand(id, commentId, userId), ct)).ToHttpResult();
        });

        group.MapPost("/{id:guid}/labels/{labelId:guid}", async (Guid id, Guid labelId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new AddLabelToTaskCommand(id, labelId, userId), ct);
            return TaskResult(http, result);
        });

        group.MapDelete("/{id:guid}/labels/{labelId:guid}", async (Guid id, Guid labelId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new RemoveLabelFromTaskCommand(id, labelId, userId), ct);
            return TaskResult(http, result);
        });
    }

    /// <summary>Success → 200 with ETag from the task's RowVersion.</summary>
    private static IResult TaskResult(HttpContext http, Result<TaskDto> result)
    {
        if (result.IsFailed) return result.ToHttpResult();
        http.SetETag(result.Value.RowVersion);
        return Results.Ok(result.Value);
    }

    /// <summary>
    /// Spec §4.3 optimistic concurrency: a conflict returns 409 with the CURRENT task body
    /// (and its fresh ETag) so the SPA can refetch + toast.
    /// </summary>
    private static async Task<IResult> TaskResultWithConflictBody(
        HttpContext http, IMediator mediator, Guid taskId, Guid userId, Result<TaskDto> result, CancellationToken ct)
    {
        if (result.IsSuccess) return TaskResult(http, result);

        if (result.Errors.Any(e => e.Message.StartsWith("conflict", StringComparison.OrdinalIgnoreCase)))
        {
            var current = await mediator.Send(new GetTaskQuery(taskId, userId), ct);
            if (current.IsSuccess)
            {
                http.SetETag(current.Value.RowVersion);
                return Results.Conflict(current.Value);
            }
        }
        return result.ToHttpResult();
    }
}
```

- [ ] **Step 12.6: Create `Presentation/Background/DeadlineWorker.cs`**

```csharp
using TaskManager.Tasks.Application.Services;

namespace TaskManager.Tasks.Presentation.Background;

/// <summary>Runs the deadline scan immediately on startup, then every hour (spec §4.3).</summary>
public class DeadlineWorker(IServiceScopeFactory scopeFactory, ILogger<DeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DeadlineScanner>().ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deadline scan failed; will retry next tick");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
```

- [ ] **Step 12.7: Replace `Program.cs`**

```csharp
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskManager.Tasks.Application.Behaviors;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Services;
using TaskManager.Tasks.Infrastructure;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Presentation.Background;
using TaskManager.Tasks.Presentation.Endpoints;
using TaskManager.Tasks.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Tasks"));

// Infrastructure: DbContext + repositories + MassTransit with EF outbox
builder.Services.AddTasksInfrastructure(builder.Configuration);

// Mediator + pipeline behaviors + validators + mapper
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton<TasksMapper>();

// Deadline scan (Application logic, Presentation hosting)
builder.Services.AddScoped<DeadlineScanner>();
builder.Services.AddHostedService<DeadlineWorker>();

// Health checks per spec §8
var connectionForHealth = builder.Configuration["ConnectionStrings:TasksDb"]
                          ?? builder.Configuration["TASKS_DB_CONNECTION"]
                          ?? string.Empty;
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddNpgSql(_ => connectionForHealth, name: "postgres", tags: new[] { "ready" });

var app = builder.Build();

// Apply EF migrations on startup (spec §8)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapBoardEndpoints();
app.MapTaskEndpoints();

app.Run();

public partial class Program;
```

- [ ] **Step 12.8: Create `appsettings.Development.json`**

Copy the Identity service's `appsettings.Development.json` as the starting point (committed non-secret defaults per spec §9):

```powershell
Copy-Item src/services/identity/TaskManager.Identity/appsettings.Development.json src/services/tasks/TaskManager.Tasks/appsettings.Development.json
```

Then edit the copy: replace the Identity connection-string key/value with `"TasksDb": "Host=localhost;Port=5432;Database=tasks_db;Username=postgres;Password=postgres"` under `ConnectionStrings` (drop Jwt-specific sections if present — Tasks doesn't validate JWTs), and add a top-level `"RABBITMQ_URL": "rabbitmq://guest:guest@localhost:5672"`. Keep the Serilog section as-is. Cross-check ports/credentials against `docker-compose.yml` and use the compose values verbatim.

- [ ] **Step 12.9: Build + full local suite green, commit**

Run: `dotnet build SmartTaskManager.sln --no-restore` → Build succeeded.
Run: `dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName!~Integration"` → PASS, 0 failures.
Also: `dotnet test tests/TaskManager.Identity.Tests --filter "FullyQualifiedName!~Integration"` → PASS (no regressions).

```powershell
git add src/services/tasks
git commit -m "feat(tasks): implement Presentation layer with endpoints, concurrency contract and deadline worker"
```

---

### Task 13: CI verification (integration tests run here)

- [ ] **Step 13.1: Push the branch**

```powershell
git push -u origin feature/tasks-service
```

- [ ] **Step 13.2: Watch CI**

Run: `gh run watch` (or `gh run list --branch feature/tasks-service --limit 1` then `gh run view <id> --log-failed`)
Expected: the .NET job passes — including the Testcontainers integration tests that can't run locally.

- [ ] **Step 13.3: Fix-forward loop**

If integration tests fail in CI: read the failed-test log, fix locally, run the unit suite to guard against regressions, commit (`fix(tasks): …`), push, re-watch. Likely first-run suspects, in order:
1. Outbox drain slower than the harness timeout → raise `Harness.TestTimeout` or lower `OUTBOX_QUERY_DELAY_SECONDS` (already 1 s).
2. `AddMassTransitTestHarness` vs. existing RabbitMQ registration → if the harness doesn't observe published events, move `AddMassTransitTestHarness()` registration to run with `x.AddEntityFrameworkOutbox` config replicated inside it (MassTransit docs: the harness replaces the bus but keeps registered configuration).
3. Owned-entity `Color` constructor binding → if EF complains, add a private parameterless-constructor-compatible mapping via `color.WithOwner()` plus `HasField` — or map `Color.Value` with `PropertyAccessMode.Field`.

- [ ] **Step 13.4: Done-check against the spec, then stop**

Verify each item: 22 endpoints implemented and integration-tested; 6 events published with outbox; 409-with-current-body concurrency contract; 200-cap + `X-Result-Truncated`; architecture tests green; Serilog + `/health`. Step 3 is then complete on this branch — PR into `develop` is a separate decision (superpowers:finishing-a-development-branch).

---

## Self-review notes (already applied)

- **Spec coverage:** all 17 commands + 4 queries from §4.3 have handlers and tests; all 22 endpoints mapped in Tasks 12 and integration-tested in Task 5; events/outbox in Tasks 8/11; concurrency, truncation, role rules covered. The `ChangeMemberRole` domain method has no endpoint — the spec's endpoint table has none either; left unexposed.
- **Known deviations (intentional, documented):** `uint RowVersion`/xmin instead of `byte[]` (Npgsql-idiomatic; ETag carries the uint); hand-written `TasksMapper` instead of Mapperly partials (all mappings need custom expressions); `TasksPage` record instead of a tuple in `Result<>` (Mediator/serialization ergonomics).
- **Board update authorization:** spec lists owner-only actions (delete board, members, labels) and Editor actions (tasks) but is silent on board rename — chose **Owner-only** (conservative).
- **3a red-state caveat:** integration tests reference `TasksDbContext` before it exists, so the *test project* is red-by-compile-error between Tasks 5 and 11; the production project builds at every commit. The Task 6 note gives the temporary-exclusion escape hatch to keep unit-test feedback per task.

