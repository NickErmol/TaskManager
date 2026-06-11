# Subtasks / Checklists Implementation Plan (v1.1 Feature 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-task checklist (subtasks) to the Tasks service and surface it in the SPA — an inline editor in the task dialog and a "done/total" progress chip on cards.

**Architecture:** New `ChecklistItem` child entity on the `TaskItem` aggregate in the Tasks service (Onion: Domain → Application → Presentation/Infrastructure, Result pattern, `DateTimeOffset` everywhere). Three new endpoints under the existing `/api/tasks` route group. **Deliberate concurrency exception:** checklist mutations do *not* require `If-Match` and do *not* touch `TaskItem.RowVersion` (`UpdatedAt`) — two members toggling different items must never 409 each other (design § Feature 2). All three endpoints return the full fresh `TaskDto` (like the label routes) so the SPA replaces task state uniformly. No events (checklist changes have no analytics meaning). New `checklist_items` table via EF migration, cascade-deleted with the task.

**Tech Stack:** .NET 10, `martinothamar/Mediator` (NOT MediatR), FluentResults, FluentValidation, EF Core + Npgsql, xUnit + FluentAssertions + NSubstitute + Testcontainers; Angular 18 standalone + NgRx Signals + Angular Material, Jest, Playwright E2E.

**Branch:** `feature/checklists` off `develop`. Conventional Commits. PR into `develop` must pass the 7 required checks (5× `test-dotnet`, `test-angular`, `e2e`).

**Working directory note:** All `npx jest` / `npm` / `npx ng` commands run from `frontend/task-manager-app/`. All `git` / `dotnet` commands run from the repo root.

**Spec:** `docs/superpowers/specs/2026-06-11-v1.1-live-collaboration-design.md` § Feature 2. Key decisions baked into this plan:
- `ChecklistItem`: `Id, TaskItemId, Title (1–200), IsDone, Position, CreatedAt`; private setters + factory + behavior methods.
- No `If-Match`; checklist writes leave `TaskItem.RowVersion`/`UpdatedAt` untouched (this is the whole point — collaborative toggles must not conflict).
- Reordering is out of scope (YAGNI); `Position` = insertion order.
- PUT carries the *desired* state `{ title?, isDone? }` (idempotent setter), not a blind toggle — safer under concurrent edits. The domain method is `SetDone(bool)` + `Rename(string)`; this is a small, documented deviation from the design's literal `Toggle()` naming and is recorded in the spec addendum.

---

### Task 0: Branch setup

- [ ] **Step 0.1: Create the branch**

```bash
git checkout develop && git pull --ff-only origin develop
git checkout -b feature/checklists
```

---

### Task 1: Domain — `ChecklistItem` entity + `TaskItem` behaviors

**Files:**
- Create: `src/services/tasks/TaskManager.Tasks/Domain/Entities/ChecklistItem.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Domain/Entities/TaskItem.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/ChecklistItemTests.cs`

- [ ] **Step 1.1: Write the failing entity test**

Create `tests/TaskManager.Tasks.Tests/Unit/ChecklistItemTests.cs`:

```csharp
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
```

- [ ] **Step 1.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~ChecklistItemTests"
```
Expected: FAIL to compile — `TaskItem` has no `AddChecklistItem` / `Checklist` / `RemoveChecklistItem`.

- [ ] **Step 1.3: Create the `ChecklistItem` entity**

Create `src/services/tasks/TaskManager.Tasks/Domain/Entities/ChecklistItem.cs`:

```csharp
namespace TaskManager.Tasks.Domain.Entities;

/// <summary>
/// A subtask under a <see cref="TaskItem"/>. Independent child collection: mutations are
/// last-write-wins and deliberately do NOT advance the parent task's RowVersion, so two
/// members toggling different items never conflict (spec §13.2).
/// </summary>
public class ChecklistItem
{
    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public string Title { get; private set; } = default!;
    public bool IsDone { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ChecklistItem() { }

    public static ChecklistItem Create(Guid taskItemId, string title, int position)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskItemId,
            Title = title,
            IsDone = false,
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Idempotent — the PUT carries the desired state, not a blind flip.</summary>
    public void SetDone(bool isDone) => IsDone = isDone;

    public void Rename(string title) => Title = title;
}
```

- [ ] **Step 1.4: Add the checklist collection + behaviors to `TaskItem`**

In `src/services/tasks/TaskManager.Tasks/Domain/Entities/TaskItem.cs`:

1. Next to the existing backing fields (after the `_comments` field, around line 29), add:

```csharp
    private readonly List<ChecklistItem> _checklist = new();
```

2. Next to the existing read-only navigations (after the `Comments` property, around line 31), add:

```csharp
    public IReadOnlyList<ChecklistItem> Checklist => _checklist.AsReadOnly();
```

3. At the end of the class (after `RemoveLabel`, before the closing brace), add:

```csharp
    /// <summary>
    /// Appends a checklist item at the end. Intentionally does NOT touch <see cref="UpdatedAt"/>:
    /// checklist writes must not advance RowVersion (xmin) so concurrent toggles by different
    /// members never 409 (spec §13.2). Same for <see cref="RemoveChecklistItem"/> and item edits.
    /// </summary>
    public ChecklistItem AddChecklistItem(string title)
    {
        var position = _checklist.Count == 0 ? 0 : _checklist.Max(i => i.Position) + 1;
        var item = ChecklistItem.Create(Id, title, position);
        _checklist.Add(item);
        return item;
    }

    public bool RemoveChecklistItem(Guid itemId) => _checklist.RemoveAll(i => i.Id == itemId) > 0;
```

- [ ] **Step 1.5: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~ChecklistItemTests"
```
Expected: PASS (3 tests).

- [ ] **Step 1.6: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Domain/Entities/ tests/TaskManager.Tasks.Tests/Unit/ChecklistItemTests.cs
git commit -m "feat(tasks): ChecklistItem entity and TaskItem checklist behaviors"
```

---

### Task 2: DTO + mapper

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/DTOs/TaskDtos.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Mappers/TasksMapper.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/QueryHandlerTests.cs` is not the right place; add a tiny mapper test to a new file.
- Test: `tests/TaskManager.Tasks.Tests/Unit/TasksMapperTests.cs`

- [ ] **Step 2.1: Write the failing mapper test**

Create `tests/TaskManager.Tasks.Tests/Unit/TasksMapperTests.cs`:

```csharp
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class TasksMapperTests
{
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public void ToDto_ProjectsChecklistOrderedByPosition()
    {
        var task = Fake.Task(Guid.NewGuid());
        var a = task.AddChecklistItem("first");
        var b = task.AddChecklistItem("second");
        b.SetDone(true);

        var dto = Mapper.ToDto(task);

        dto.Checklist.Should().HaveCount(2);
        dto.Checklist[0].Id.Should().Be(a.Id);
        dto.Checklist[0].Title.Should().Be("first");
        dto.Checklist[0].IsDone.Should().BeFalse();
        dto.Checklist[1].Id.Should().Be(b.Id);
        dto.Checklist[1].IsDone.Should().BeTrue();
        dto.Checklist[1].Position.Should().Be(1);
    }
}
```

- [ ] **Step 2.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~TasksMapperTests"
```
Expected: FAIL to compile — `TaskDto` has no `Checklist`, `ChecklistItemDto` does not exist.

- [ ] **Step 2.3: Add `ChecklistItemDto` and extend `TaskDto`**

In `src/services/tasks/TaskManager.Tasks/Application/DTOs/TaskDtos.cs`:

1. After the `CommentDto` record (line 7), add:

```csharp
public record ChecklistItemDto(Guid Id, string Title, bool IsDone, int Position);
```

2. Append `Checklist` as the final positional member of `TaskDto` (after `Comments`):

```csharp
public record TaskDto(
    Guid Id, Guid BoardId, string Title, string? Description,
    string Status, string Priority, Guid CreatedBy, Guid? AssignedTo,
    DateTimeOffset? DueDate, int Position, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    uint RowVersion,
    IReadOnlyList<Guid> LabelIds,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<ChecklistItemDto> Checklist);
```

- [ ] **Step 2.4: Extend the mapper**

In `src/services/tasks/TaskManager.Tasks/Application/Mappers/TasksMapper.cs`:

1. After the `ToDto(TaskComment c)` method, add:

```csharp
    public ChecklistItemDto ToDto(ChecklistItem c) => new(c.Id, c.Title, c.IsDone, c.Position);
```

2. In `ToDto(TaskItem t)`, append the checklist projection as the final constructor argument (after the `t.Comments…` line):

```csharp
    public TaskDto ToDto(TaskItem t) => new(
        t.Id, t.BoardId, t.Title, t.Description,
        t.Status.ToString(), t.Priority.ToString(), t.CreatedBy, t.AssignedTo,
        t.DueDate, t.Position, t.CreatedAt, t.UpdatedAt, t.RowVersion,
        t.Labels.Select(l => l.LabelId).ToList(),
        t.Comments.OrderBy(c => c.CreatedAt).Select(ToDto).ToList(),
        t.Checklist.OrderBy(c => c.Position).Select(ToDto).ToList());
```

- [ ] **Step 2.5: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~TasksMapperTests"
```
Expected: PASS (1 test).

- [ ] **Step 2.6: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Application/DTOs/TaskDtos.cs src/services/tasks/TaskManager.Tasks/Application/Mappers/TasksMapper.cs tests/TaskManager.Tasks.Tests/Unit/TasksMapperTests.cs
git commit -m "feat(tasks): ChecklistItemDto and TaskDto.Checklist projection"
```

---

### Task 3: Commands, validators, and handlers

**Files:**
- Create: `src/services/tasks/TaskManager.Tasks/Application/Commands/ChecklistCommands.cs`
- Create: `src/services/tasks/TaskManager.Tasks/Application/Handlers/ChecklistCommandHandlers.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Validators/CommandValidators.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/ChecklistCommandHandlerTests.cs`

- [ ] **Step 3.1: Write the failing handler tests**

Create `tests/TaskManager.Tasks.Tests/Unit/ChecklistCommandHandlerTests.cs`:

```csharp
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class ChecklistCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private static readonly TasksMapper Mapper = new();

    private void SetRole(Guid boardId, Guid userId, BoardRole? role)
        => _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(role);

    [Fact]
    public async Task Add_AsEditor_AppendsItemAndReturnsTaskDto()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AddChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddChecklistItemCommand(task.Id, "Write tests", editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Checklist.Should().ContainSingle(i => i.Title == "Write tests" && !i.IsDone);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_AsViewer_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var viewer = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, viewer, BoardRole.Viewer);
        var handler = new AddChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddChecklistItemCommand(task.Id, "nope", viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task Add_MissingTask_ReturnsNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);
        var handler = new AddChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddChecklistItemCommand(Guid.NewGuid(), "x", Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task Update_SetsDoneAndRenames()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("draft");
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateChecklistItemCommand(task.Id, item.Id, "final", true, editor), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Checklist.Single();
        dto.Title.Should().Be("final");
        dto.IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Update_OnlyIsDone_LeavesTitleUnchanged()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("keep me");
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateChecklistItemCommand(task.Id, item.Id, null, true, editor), default);

        var dto = result.Value.Checklist.Single();
        dto.Title.Should().Be("keep me");
        dto.IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Update_MissingItem_ReturnsNotFound()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateChecklistItemCommand(task.Id, Guid.NewGuid(), null, true, editor), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task Delete_AsEditor_RemovesItem()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("bye");
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new DeleteChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new DeleteChecklistItemCommand(task.Id, item.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Checklist.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_MissingItem_ReturnsNotFound()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new DeleteChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new DeleteChecklistItemCommand(task.Id, Guid.NewGuid(), editor), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }
}
```

- [ ] **Step 3.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~ChecklistCommandHandlerTests"
```
Expected: FAIL to compile — the commands and handlers don't exist.

- [ ] **Step 3.3: Create the commands**

Create `src/services/tasks/TaskManager.Tasks/Application/Commands/ChecklistCommands.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

// Checklist mutations carry no concurrency token: they are an independent child collection
// where last-write-wins is harmless and required (spec §13.2). All three return the fresh TaskDto.
public record AddChecklistItemCommand(Guid TaskId, string Title, Guid UserId) : IRequest<Result<TaskDto>>;
public record UpdateChecklistItemCommand(Guid TaskId, Guid ItemId, string? Title, bool? IsDone, Guid UserId) : IRequest<Result<TaskDto>>;
public record DeleteChecklistItemCommand(Guid TaskId, Guid ItemId, Guid UserId) : IRequest<Result<TaskDto>>;
```

- [ ] **Step 3.4: Create the handlers**

Create `src/services/tasks/TaskManager.Tasks/Application/Handlers/ChecklistCommandHandlers.cs`:

```csharp
using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class AddChecklistItemCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddChecklistItemCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(AddChecklistItemCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        task.AddChecklistItem(cmd.Title);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class UpdateChecklistItemCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateChecklistItemCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(UpdateChecklistItemCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        var item = task.Checklist.FirstOrDefault(i => i.Id == cmd.ItemId);
        if (item is null) return Result.Fail("not found: checklist item");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        if (cmd.Title is not null) item.Rename(cmd.Title);
        if (cmd.IsDone is not null) item.SetDone(cmd.IsDone.Value);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class DeleteChecklistItemCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<DeleteChecklistItemCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(DeleteChecklistItemCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (!task.RemoveChecklistItem(cmd.ItemId)) return Result.Fail("not found: checklist item");

        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}
```

(`TaskAccess` is the `internal static` helper already defined in `TaskCommandHandlers.cs`; same namespace, so no import needed.)

- [ ] **Step 3.5: Add the validators**

In `src/services/tasks/TaskManager.Tasks/Application/Validators/CommandValidators.cs`, append at the end of the file (after `CreateLabelCommandValidator`):

```csharp
public class AddChecklistItemCommandValidator : AbstractValidator<AddChecklistItemCommand>
{
    public AddChecklistItemCommandValidator()
        => RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
}

public class UpdateChecklistItemCommandValidator : AbstractValidator<UpdateChecklistItemCommand>
{
    public UpdateChecklistItemCommandValidator()
        // Title is optional on PUT; when present it must be 1–200 chars.
        => RuleFor(x => x.Title!).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
}
```

- [ ] **Step 3.6: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~ChecklistCommandHandlerTests"
```
Expected: PASS (8 tests).

- [ ] **Step 3.7: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Application/ tests/TaskManager.Tasks.Tests/Unit/ChecklistCommandHandlerTests.cs
git commit -m "feat(tasks): checklist commands, handlers, and validators"
```

---

### Task 4: EF configuration, repository includes, and migration

**Files:**
- Create: `src/services/tasks/TaskManager.Tasks/Infrastructure/Persistence/Configurations/ChecklistItemConfiguration.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Infrastructure/Persistence/Configurations/TaskItemConfiguration.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Infrastructure/Persistence/Repositories/TaskRepository.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Infrastructure/Persistence/Repositories/BoardRepository.cs`
- Create (generated): migration under `…/Infrastructure/Persistence/Migrations/`

- [ ] **Step 4.1: Create `ChecklistItemConfiguration`**

Create `src/services/tasks/TaskManager.Tasks/Infrastructure/Persistence/Configurations/ChecklistItemConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("checklist_items");
        builder.HasKey(c => c.Id);
        // IDs are factory-set; without this EF treats convention-generated keys as existing
        // rows and issues a 0-row UPDATE instead of an INSERT (same fix as TaskComment).
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.TaskItemId);
    }
}
```

- [ ] **Step 4.2: Wire the navigation on `TaskItem`**

In `TaskItemConfiguration.cs`, after the existing `HasMany(t => t.Labels)…` line (line 23) add the checklist relationship, and after the `Navigation(t => t.Labels)…` line (line 25) add the field access mode:

```csharp
        builder.HasMany(t => t.Checklist).WithOne().HasForeignKey(c => c.TaskItemId).OnDelete(DeleteBehavior.Cascade);
```

```csharp
        builder.Navigation(t => t.Checklist).UsePropertyAccessMode(PropertyAccessMode.Field);
```

- [ ] **Step 4.3: Eager-load checklist on both read paths**

In `TaskRepository.cs`, `GetByIdAsync` — add the checklist include (this feeds the single-task fetch behind the task dialog):

```csharp
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Tasks
            .Include(t => t.Comments)
            .Include(t => t.Labels)
            .Include(t => t.Checklist)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
```

In `BoardRepository.cs`, `GetByIdAsync` — add `.ThenInclude(t => t.Checklist)` (this is the board-detail load path that feeds the card progress chip, via `GetBoardQuery` → `ToDetailDto`):

```csharp
    public Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Boards
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Checklist)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
```

- [ ] **Step 4.4: Generate the migration**

Ensure the EF tool is available, then add the migration (the repo has `TasksDbContextDesignTimeFactory`, so no running DB is needed):

```bash
dotnet tool restore 2>$null; dotnet ef migrations add AddChecklistItems --project src/services/tasks/TaskManager.Tasks --context TasksDbContext
```

If `dotnet ef` is not found: `dotnet tool install --global dotnet-ef --version 10.*` then re-run the migration command.

Expected: a new `…_AddChecklistItems.cs` migration + updated `TasksDbContextModelSnapshot.cs`. Open the migration and confirm `Up()` creates the `checklist_items` table with an FK to `tasks` and `onDelete: Cascade`, and an index on `TaskItemId`. It must NOT alter the `tasks` table.

- [ ] **Step 4.5: Build to verify**

```bash
dotnet build src/services/tasks/TaskManager.Tasks --no-restore
```
Expected: 0 errors (Scriban/NU190x warnings from Mapperly are pre-existing noise — ignore).

- [ ] **Step 4.6: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Infrastructure/
git commit -m "feat(tasks): checklist_items EF config, includes, and migration"
```

---

### Task 5: Endpoints + integration test

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Presentation/Endpoints/TaskEndpoints.cs`
- Test: `tests/TaskManager.Tasks.Tests/Integration/TaskEndpointsTests.cs`

- [ ] **Step 5.1: Write the failing integration test**

Append to `TaskEndpointsTests` (in `tests/TaskManager.Tasks.Tests/Integration/TaskEndpointsTests.cs`), before the final `Endpoints_WithoutUserIdHeader_Return401` test:

```csharp
    [Fact]
    public async Task Checklist_AddToggleRename_Delete_FullLifecycle()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);

        // add
        var added = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/checklist", new { Title = "Write tests" });
        await added.ShouldBeAsync(HttpStatusCode.OK);
        var afterAdd = (await added.Content.ReadFromJsonAsync<TaskDto>())!;
        afterAdd.Checklist.Should().ContainSingle(i => i.Title == "Write tests" && !i.IsDone);
        var itemId = afterAdd.Checklist[0].Id;

        // toggle done + rename via PUT
        var updated = await client.PutAsJsonAsync($"/api/tasks/{task.Id}/checklist/{itemId}",
            new { Title = "Write more tests", IsDone = true });
        await updated.ShouldBeAsync(HttpStatusCode.OK);
        var afterUpdate = (await updated.Content.ReadFromJsonAsync<TaskDto>())!;
        afterUpdate.Checklist[0].Title.Should().Be("Write more tests");
        afterUpdate.Checklist[0].IsDone.Should().BeTrue();

        // delete
        var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}/checklist/{itemId}");
        await deleted.ShouldBeAsync(HttpStatusCode.OK);
        (await deleted.Content.ReadFromJsonAsync<TaskDto>())!.Checklist.Should().BeEmpty();
    }

    [Fact]
    public async Task Checklist_DoesNotRequireIfMatch_AndDoesNotChangeRowVersion()
    {
        var (boardId, _, editor, _) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);
        var client = factory.As(editor);
        var etagBefore = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();

        // No If-Match header — must still succeed (spec §13.2).
        var added = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/checklist", new { Title = "no if-match needed" });
        await added.ShouldBeAsync(HttpStatusCode.OK);

        // RowVersion (xmin) must NOT have advanced: checklist writes don't touch the task row.
        var etagAfter = (await client.GetAsync($"/api/tasks/{task.Id}")).Etag();
        etagAfter.Should().Be(etagBefore, "checklist writes must not advance the task RowVersion");
    }

    [Fact]
    public async Task Checklist_AddAsViewer_Returns403()
    {
        var (boardId, _, editor, viewer) = await factory.SeedBoardAsync();
        var task = await factory.SeedTaskAsync(boardId, editor);

        var response = await factory.As(viewer).PostAsJsonAsync($"/api/tasks/{task.Id}/checklist", new { Title = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
```

- [ ] **Step 5.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Checklist_AddToggleRename"
```
Expected: FAIL — endpoints return 404 (routes not mapped) so the OK assertion fails. (Requires Docker for Testcontainers; if Docker is unavailable locally, rely on the `test-dotnet (tasks)` CI check — note this in the PR.)

- [ ] **Step 5.3: Map the endpoints**

In `TaskEndpoints.cs`:

1. After the existing request records (after `CommentRequest`, line 14), add:

```csharp
public record AddChecklistItemRequest(string Title);
public record UpdateChecklistItemRequest(string? Title, bool? IsDone);
```

2. Inside `MapTaskEndpoints`, after the label routes (after the `MapDelete("/{id:guid}/labels/{labelId:guid}", …)` block, ~line 111), add:

```csharp
        group.MapPost("/{id:guid}/checklist", async (Guid id, AddChecklistItemRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new AddChecklistItemCommand(id, req.Title, userId), ct);
            return TaskResult(http, result);
        });

        group.MapPut("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, UpdateChecklistItemRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new UpdateChecklistItemCommand(id, itemId, req.Title, req.IsDone, userId), ct);
            return TaskResult(http, result);
        });

        group.MapDelete("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new DeleteChecklistItemCommand(id, itemId, userId), ct);
            return TaskResult(http, result);
        });
```

(`TaskResult` returns 200 + ETag on success and maps failures to status codes. No `If-Match` parsing — checklist routes intentionally skip it.)

- [ ] **Step 5.4: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Checklist"
```
Expected: PASS (3 integration + the 8 unit handler tests + entity/mapper tests all match `Checklist`).

- [ ] **Step 5.5: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Presentation/Endpoints/TaskEndpoints.cs tests/TaskManager.Tasks.Tests/Integration/TaskEndpointsTests.cs
git commit -m "feat(tasks): checklist endpoints (POST/PUT/DELETE) with integration coverage"
```

---

### Task 6: Frontend models, API methods, and factory

**Files:**
- Modify: `frontend/task-manager-app/src/app/core/models/tasks.models.ts`
- Modify: `frontend/task-manager-app/src/app/testing/factories.ts`
- Modify: `frontend/task-manager-app/src/app/core/http/tasks-api.service.ts`
- Test: `frontend/task-manager-app/src/app/core/http/tasks-api.service.spec.ts`

- [ ] **Step 6.1: Add the model + factory**

In `tasks.models.ts`, after the `CommentDto` interface (line 27), add:

```typescript
export interface ChecklistItemDto {
  id: string;
  title: string;
  isDone: boolean;
  position: number;
}
```

Add `checklist: ChecklistItemDto[];` to the `TaskDto` interface (after `comments`):

```typescript
export interface TaskDto {
  id: string;
  boardId: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  createdBy: string;
  assignedTo: string | null;
  dueDate: string | null;
  position: number;
  createdAt: string;
  updatedAt: string;
  rowVersion: number;
  labelIds: string[];
  comments: CommentDto[];
  checklist: ChecklistItemDto[];
}
```

In `factories.ts`:

1. Add `ChecklistItemDto` to the import from `'../core/models'`.
2. Add `checklist: [],` to the `makeTask` object literal (after `comments: [],`).
3. After `makeLabel`, add a factory:

```typescript
export const makeChecklistItem = (overrides: Partial<ChecklistItemDto> = {}): ChecklistItemDto => ({
  id: nextGuid(),
  title: 'A subtask',
  isDone: false,
  position: 0,
  ...overrides,
});
```

- [ ] **Step 6.2: Write the failing API tests**

Append inside the `describe('TasksApiService', …)` block in `tasks-api.service.spec.ts` (ensure `makeTask` is imported from the factories):

```typescript
it('addChecklistItem() issues POST /api/tasks/{id}/checklist without If-Match', () => {
  const task = makeTask();

  service.addChecklistItem(task.id, 'Write tests').subscribe();

  const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/checklist`));
  expect(req.request.method).toBe('POST');
  expect(req.request.body).toEqual({ title: 'Write tests' });
  expect(req.request.headers.has('If-Match')).toBe(false);
  req.flush(task);
});

it('updateChecklistItem() issues PUT /api/tasks/{id}/checklist/{itemId}', () => {
  const task = makeTask();

  service.updateChecklistItem(task.id, 'item-1', { isDone: true }).subscribe();

  const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/checklist/item-1`));
  expect(req.request.method).toBe('PUT');
  expect(req.request.body).toEqual({ isDone: true });
  expect(req.request.headers.has('If-Match')).toBe(false);
  req.flush(task);
});

it('deleteChecklistItem() issues DELETE /api/tasks/{id}/checklist/{itemId}', () => {
  const task = makeTask();

  service.deleteChecklistItem(task.id, 'item-1').subscribe();

  const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/checklist/item-1`));
  expect(req.request.method).toBe('DELETE');
  req.flush(task);
});
```

- [ ] **Step 6.3: Run to verify failure**

```bash
npx jest src/app/core/http/tasks-api.service
```
Expected: the three new tests FAIL with `service.addChecklistItem is not a function`.

- [ ] **Step 6.4: Implement the API methods**

In `tasks-api.service.ts`, add `ChecklistItemDto` is not needed (methods return `TaskDto`). Append to the class (after `detachLabel`):

```typescript
  addChecklistItem(id: string, title: string): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/checklist`), { title });
  }

  updateChecklistItem(
    id: string,
    itemId: string,
    patch: { title?: string; isDone?: boolean },
  ): Observable<TaskDto> {
    return this.http.put<TaskDto>(apiUrl(`/api/tasks/${id}/checklist/${itemId}`), patch);
  }

  deleteChecklistItem(id: string, itemId: string): Observable<TaskDto> {
    return this.http.delete<TaskDto>(apiUrl(`/api/tasks/${id}/checklist/${itemId}`));
  }
```

- [ ] **Step 6.5: Run to verify pass — then the whole suite (the `makeTask` change touches many specs)**

```bash
npx jest src/app/core/http/tasks-api.service
npx jest
```
Expected: PASS; no other suite broken (every spec gets `checklist: []` for free via `makeTask`).

- [ ] **Step 6.6: Commit**

```bash
git add frontend/task-manager-app/src/app/core/models/tasks.models.ts frontend/task-manager-app/src/app/testing/factories.ts frontend/task-manager-app/src/app/core/http/tasks-api.service.ts frontend/task-manager-app/src/app/core/http/tasks-api.service.spec.ts
git commit -m "feat(frontend): checklist model, factory, and API methods"
```

---

### Task 7: Checklist editor in the task dialog

Inline editor inside `TaskDetailComponent`: add an item, toggle done, rename (on blur), delete. Each call hits the API immediately and replaces the local checklist with the returned `TaskDto.checklist`; a `checklistChanged` flag drives the board refetch on close (same pattern as the label picker). Dialogs are exercised through E2E (Task 9), not Jest, per the convention in `invite-member-dialog.component.ts` / Feature 1.

**Files:**
- Modify: `frontend/task-manager-app/src/app/features/tasks/task-detail.component.ts`

- [ ] **Step 7.1: Extend imports and dialog wiring**

In `task-detail.component.ts`:

1. Add `computed` to the `@angular/core` import (it currently imports `ChangeDetectionStrategy, Component, inject, signal`):

```typescript
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
```

2. Add `FormsModule` to the `@angular/forms` import:

```typescript
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
```

3. Add a `MatIconModule` import after the other Material imports:

```typescript
import { MatIconModule } from '@angular/material/icon';
```

4. Add `ChecklistItemDto` to the `../../core/models` import:

```typescript
import { ChecklistItemDto, LabelDto, TaskDto, TaskPriority, UserDto } from '../../core/models';
```

5. Add `FormsModule` and `MatIconModule` to the component `imports` array.

- [ ] **Step 7.2: Add the editor template**

In the template, after the labels `@if (data.boardLabels.length > 0) { … }` block (before the `selectedAssignee` paragraph), insert:

```html
        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-slate-600">
            Checklist
            @if (checklist().length > 0) {
              <span data-testid="checklist-progress-dialog" class="ml-1 text-xs text-slate-400">
                {{ doneCount() }}/{{ checklist().length }}
              </span>
            }
          </span>

          <ul class="flex flex-col gap-1">
            @for (item of checklist(); track item.id) {
              <li class="flex items-center gap-2" data-testid="checklist-item">
                <input
                  type="checkbox"
                  data-testid="checklist-toggle"
                  [checked]="item.isDone"
                  (change)="toggleItem(item)"
                />
                <input
                  class="flex-1 border-b border-transparent bg-transparent text-sm focus:border-slate-300 focus:outline-none"
                  [class.text-slate-400]="item.isDone"
                  [class.line-through]="item.isDone"
                  [value]="item.title"
                  (change)="renameItem(item, $event)"
                />
                <button
                  type="button"
                  data-testid="checklist-delete"
                  class="text-slate-400 hover:text-red-600"
                  [attr.aria-label]="'Delete checklist item ' + item.title"
                  (click)="deleteItem(item)"
                >
                  <mat-icon inline>close</mat-icon>
                </button>
              </li>
            }
          </ul>

          <div class="flex items-center gap-2">
            <input
              class="flex-1 border-b border-slate-200 bg-transparent text-sm focus:border-slate-400 focus:outline-none"
              data-testid="checklist-new-input"
              placeholder="Add an item"
              [(ngModel)]="newItemTitle"
              [ngModelOptions]="{ standalone: true }"
              (keyup.enter)="addItem()"
            />
            <button
              mat-button
              type="button"
              data-testid="checklist-add-button"
              [disabled]="newItemTitle.trim().length === 0 || isSaving()"
              (click)="addItem()"
            >
              Add
            </button>
          </div>
        </div>
```

- [ ] **Step 7.3: Add the editor logic**

In the class, after the `labelsChanged` signal (around line 129), add:

```typescript
  readonly checklist = signal<ChecklistItemDto[]>([...this.data.task.checklist]);
  readonly checklistChanged = signal(false);
  newItemTitle = '';

  protected readonly doneCount = computed(() => this.checklist().filter((i) => i.isDone).length);

  async addItem(): Promise<void> {
    const title = this.newItemTitle.trim();
    if (title.length === 0 || this.isSaving()) return;
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.tasksApi.addChecklistItem(this.data.task.id, title));
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
      this.newItemTitle = '';
    } catch {
      this.error.set('Could not add the checklist item.');
    }
  }

  async toggleItem(item: ChecklistItemDto): Promise<void> {
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.tasksApi.updateChecklistItem(this.data.task.id, item.id, { isDone: !item.isDone }),
      );
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
    } catch {
      this.error.set('Could not update the checklist item.');
    }
  }

  async renameItem(item: ChecklistItemDto, event: Event): Promise<void> {
    const title = (event.target as HTMLInputElement).value.trim();
    if (title.length === 0 || title === item.title) return;
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.tasksApi.updateChecklistItem(this.data.task.id, item.id, { title }),
      );
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
    } catch {
      this.error.set('Could not rename the checklist item.');
    }
  }

  async deleteItem(item: ChecklistItemDto): Promise<void> {
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.tasksApi.deleteChecklistItem(this.data.task.id, item.id));
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
    } catch {
      this.error.set('Could not delete the checklist item.');
    }
  }
```

- [ ] **Step 7.4: Make Cancel report checklist changes too**

The Cancel button currently closes with `labelsChanged()`. Change it so a checklist-only edit still triggers the board refetch:

```html
        <button mat-button type="button" [mat-dialog-close]="labelsChanged() || checklistChanged()">Cancel</button>
```

(`save()` already closes with the updated task, which is truthy, so a Save path refetches regardless.)

- [ ] **Step 7.5: Verify compile, lint, and existing specs**

```bash
npx ng build --configuration development
npm run lint
npx jest src/app/features/tasks
```
Expected: build + lint clean; `task-detail.component.spec.ts` still green (its `makeTask` data now carries `checklist: []`, so `checklist()` initializes empty). If the spec constructs dialog data inline without `checklist`, the factory default covers it; otherwise add `checklist: []`.

- [ ] **Step 7.6: Commit**

```bash
git add frontend/task-manager-app/src/app/features/tasks/task-detail.component.ts
git commit -m "feat(frontend): inline checklist editor in the task dialog"
```

---

### Task 8: Checklist progress chip on the card

**Files:**
- Modify: `frontend/task-manager-app/src/app/shared/components/task-card.component.ts`

- [ ] **Step 8.1: Add the chip**

In `task-card.component.ts`:

1. In the class, after the `labels` computed (before `isOverdue`), add:

```typescript
  readonly checklistTotal = computed(() => this.task().checklist.length);
  readonly checklistDone = computed(() => this.task().checklist.filter((i) => i.isDone).length);
```

(`computed` is already imported.)

2. In the template, inside the bottom chips row (`<div class="mt-2 flex flex-wrap items-center gap-1">`), after the `@for (label of labels(); …)` block and before the due-date `@if`, add:

```html
        @if (checklistTotal() > 0) {
          <span
            data-testid="checklist-progress"
            class="flex items-center gap-0.5 rounded-full bg-slate-100 px-1.5 py-0.5 text-xs"
            [class.text-green-600]="checklistDone() === checklistTotal()"
            [class.text-slate-600]="checklistDone() !== checklistTotal()"
          >
            <mat-icon inline>checklist</mat-icon>{{ checklistDone() }}/{{ checklistTotal() }}
          </span>
        }
```

(`MatIconModule` is already imported by the card.)

- [ ] **Step 8.2: Verify compile, lint, full suite**

```bash
npx ng build --configuration development
npm run lint
npx jest
```
Expected: all green.

- [ ] **Step 8.3: Commit**

```bash
git add frontend/task-manager-app/src/app/shared/components/task-card.component.ts
git commit -m "feat(frontend): checklist progress chip on task cards"
```

---

### Task 9: E2E flow

One Playwright test: create a task, add a checklist item (card shows `0/1`), complete it (card shows `1/1`).

**Files:**
- Modify: `tests/TaskManager.E2E.Tests/Infrastructure/Flows.cs`
- Modify: `tests/TaskManager.E2E.Tests/BoardAndTaskFlowTests.cs`

- [ ] **Step 9.1: Add a Flows helper**

Append to the `Flows` class in `Flows.cs`:

```csharp
/// <summary>Adds a checklist item to a task through the task dialog, then closes the dialog.</summary>
public static async Task AddChecklistItemAsync(IPage page, string taskTitle, string itemText)
{
    await TaskCard(page, taskTitle).ClickAsync();
    var dialog = page.Locator("mat-dialog-container");
    await dialog.GetByTestId("checklist-new-input").FillAsync(itemText);
    await dialog.GetByTestId("checklist-add-button").ClickAsync();
    await dialog.Locator("[data-testid='checklist-item']", new() { HasText = itemText }).WaitForAsync();
    await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
    await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
}
```

- [ ] **Step 9.2: Add the E2E test**

Append to `BoardAndTaskFlowTests`:

```csharp
[Fact]
public async Task Checklist_add_and_complete_updates_card_progress()
{
    var page = await NewBoardPageAsync();
    await Flows.CreateTaskAsync(page, "Task with checklist");

    await Flows.AddChecklistItemAsync(page, "Task with checklist", "Write tests");

    var card = Flows.TaskCard(page, "Task with checklist");
    await Assertions.Expect(card.GetByTestId("checklist-progress")).ToContainTextAsync("0/1");

    // complete the only item
    await card.ClickAsync();
    var dialog = page.Locator("mat-dialog-container");
    await dialog.GetByTestId("checklist-toggle").First.CheckAsync();
    await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
    await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });

    await Assertions.Expect(card.GetByTestId("checklist-progress")).ToContainTextAsync("1/1");
}
```

(If `NewBoardPageAsync`, `Flows.CreateTaskAsync`, or `Flows.TaskCard` have different signatures in the current suite, match them — they are the same helpers the Feature 1 E2E test used.)

- [ ] **Step 9.3: Build the E2E project**

```bash
dotnet build tests/TaskManager.E2E.Tests --configuration Release
```
Expected: 0 errors.

- [ ] **Step 9.4: Run the E2E suite locally (full stack required)**

```powershell
docker compose up -d --build
cd frontend/task-manager-app; npm start   # separate terminal, leave running
$env:PLAYWRIGHT_BROWSERS_PATH = 'D:\playwright-browsers'
dotnet test tests/TaskManager.E2E.Tests --configuration Release
```
Expected: 22 passed (21 existing + the new one). If only CI verification is feasible, rely on the `e2e` required check on the PR — it runs the identical suite.

- [ ] **Step 9.5: Commit**

```bash
git add tests/TaskManager.E2E.Tests/
git commit -m "test(e2e): checklist add and complete updates card progress"
```

---

### Task 10: Spec addendum + PR

**Files:**
- Modify: `smart-task-manager-spec.md` (append §13.2 after §13.1)

- [ ] **Step 10.1: Append the addendum**

In `smart-task-manager-spec.md`, after the §13.1 block (ends ~line 1680), add:

```markdown

### 13.2 Subtasks / checklists (Feature 2)
`TaskItem` gains a `ChecklistItem` child collection (`Id, TaskItemId, Title 1–200, IsDone,
Position, CreatedAt`; rich domain model — private setters + factory + `SetDone`/`Rename`).
New `checklist_items` table, cascade-deleted with the task.

Endpoints under `/api/tasks` (return the full fresh `TaskDto`, like the label routes):
- `POST /api/tasks/{id}/checklist` — body `{ title }`; appends at end.
- `PUT /api/tasks/{id}/checklist/{itemId}` — body `{ title?, isDone? }`; carries the
  *desired* state (idempotent), not a blind toggle.
- `DELETE /api/tasks/{id}/checklist/{itemId}`.

**Documented concurrency exception** (alongside the `AppUser` setter exception): checklist
mutations do **not** require `If-Match` and do **not** advance `TaskItem.RowVersion`
(`UpdatedAt` is untouched). They are an independent child collection where last-write-wins
is harmless — two members toggling different items must never 409 each other, which is
exactly the collaborative use the feature exists for. No integration events are published
(checklist changes have no analytics meaning). Reordering is out of scope; `Position` is
insertion order.

**SPA:** the task dialog gets an inline editor (add, toggle, rename on blur, delete); cards
show a `done/total` progress chip when a checklist exists (green at 100%).
```

- [ ] **Step 10.2: Full local gate**

```bash
dotnet build SmartTaskManager.sln --no-restore
dotnet test tests/TaskManager.Tasks.Tests
cd frontend/task-manager-app && npx jest && npm run lint && cd ..\..
```
Expected: solution builds; Tasks unit + integration tests green (Docker required for the integration half); Jest + lint green.

- [ ] **Step 10.3: Commit, push, PR**

```bash
git add smart-task-manager-spec.md
git commit -m "docs(spec): v1.1 addendum — subtasks / checklists"
git push -u origin feature/checklists
gh pr create --base develop --head feature/checklists \
  --title "feat: subtasks / checklists (v1.1 Feature 2)" \
  --body "Adds a per-task checklist to the Tasks service and SPA. New ChecklistItem child entity, checklist_items table (EF migration, cascade delete), three /api/tasks/{id}/checklist endpoints returning the fresh TaskDto. Documented concurrency exception: checklist writes require no If-Match and do not advance the task RowVersion, so concurrent member toggles never 409. SPA: inline checklist editor in the task dialog + done/total progress chip on cards. xUnit unit + integration coverage, Jest API coverage, one E2E flow. Spec addendum §13.2."
```

- [ ] **Step 10.4: Watch the 7 required checks; merge when green**

```bash
gh pr checks --watch
gh pr merge --merge
```
Expected: all 7 checks green (`e2e` runs the 22-test suite); merge completes Feature 2.

---

## Self-review notes (already applied)

- **Spec coverage (design § Feature 2):** `ChecklistItem` entity with private setters + factory + behavior methods ✔ (Task 1); `TaskItem.AddChecklistItem`/`RemoveChecklistItem`, items load with the task ✔ (Tasks 1, 4); three endpoints with the exact verbs/routes/bodies ✔ (Task 5); `If-Match`-free + no RowVersion touch, asserted ✔ (Tasks 1, 5); `TaskDto.Checklist` of `ChecklistItemDto(Id, Title, IsDone, Position)` ✔ (Task 2); card `n/m` progress chip ✔ (Task 8); inline editor add/toggle/rename/delete ✔ (Task 7); no events ✔ (Task 3 handlers take no `IEventPublisher`); `checklist_items` table + cascade delete via migration ✔ (Task 4); spec addendum ✔ (Task 10); E2E checklist progress flow ✔ (Task 9).
- **Documented deviation:** the design names the domain method `Toggle()`; this plan implements `SetDone(bool)` + `Rename(string)` because the PUT carries the desired state, which is idempotent and safer under concurrent edits. Recorded in the plan header and the §13.2 addendum.
- **Type consistency:** `ChecklistItemDto` is defined once (Task 2, backend; Task 6, frontend) with identical members `id/title/isDone/position`. `AddChecklistItemCommand`/`UpdateChecklistItemCommand`/`DeleteChecklistItemCommand` defined in Task 3 and only referenced afterward (Task 5 endpoints). Frontend `addChecklistItem`/`updateChecklistItem`/`deleteChecklistItem` defined in Task 6 and consumed in Task 7. `checklistChanged`/`checklist`/`doneCount` introduced together in Task 7. `checklistTotal`/`checklistDone` introduced in Task 8.
- **No placeholders:** every code step carries the actual code; every run step carries the command and expected outcome.
- **Test-first per phase:** each backend task writes the failing test before the implementation; the migration (Task 4) is structural and verified by build + the Task 5 integration tests that exercise the real schema via Testcontainers.
