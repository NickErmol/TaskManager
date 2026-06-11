# Per-Board Activity Feed Implementation Plan (v1.1 Feature 4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A live, per-board audit trail — "Alice moved *Fix login* → Done · just now" — in a collapsible panel on the board, refreshed off the Feature-3 hub signal.

**Architecture:** Tasks already publishes user events to RabbitMQ and Analytics already projects them into a `task_events` read model (with `EventType`, `UserId`, `OccurredAt`). Feature 4 **reuses `task_events`** rather than adding a near-duplicate table, adding two columns (`ActorId`, `TaskTitle`) so the feed renders without cross-service lookups. Two new contract events (`TaskUpdatedEvent`, `TaskDeletedEvent`) cover edits and deletes. A membership-checked `GET /api/analytics/boards/{boardId}/activity` serves the newest N rows; membership is verified by Analytics calling Tasks `GET /api/boards/{boardId}` with the caller's `X-User-Id`. The SPA shows a collapsible panel that reloads when a Feature-3 `TaskUpserted`/`TaskDeleted` frame arrives, with actor names resolved client-side via the existing Identity `GET /api/users/{id}`.

**Tech Stack:** .NET 10, MassTransit (existing topic exchange + EF inbox on Analytics), EF Core/Npgsql; xUnit + Testcontainers; Angular 18 standalone + NgRx Signals; Jest; Playwright.

**Branch:** `feature/activity-feed` off `develop`. Conventional Commits. PR into `develop` must pass the 7 required checks.

**Working directory note:** `npx`/`npm` from `frontend/task-manager-app/`; `dotnet`/`git` from repo root.

**Environment note:** local Docker may be unavailable (wedged secrets-engine socket — needs a host reboot). Where a step needs Docker (Testcontainers integration tests, full-stack E2E), build to prove compilation and rely on the CI `test-dotnet (analytics)` / `e2e` checks. Same fallback Features 2–3 used.

**Spec:** `docs/superpowers/specs/2026-06-11-v1.1-live-collaboration-design.md` § Feature 4. **Two documented refinements over the design** (both reduce scope/risk and are recorded in the §13.4 addendum):
1. **Reuse `task_events`, no new `board_activity` table, no physical retention/trim.** `task_events` is already the unbounded event log that powers the completion trend (which needs 30 days of `task.completed`), so trimming it would corrupt the trend. The feed query simply `LIMIT`s to the requested count (≤100). Net: add `ActorId` + `TaskTitle` columns; everything else is reuse.
2. **No reshaping of the 5 existing events.** They already carry an actor (`CreatedBy`/`ChangedBy`/`AssignedBy`/`CompletedBy`/`AuthorId`) and `Title`; the projector maps those into the new columns. So there is **no breaking change to the Notifications consumers** — only two *additive* new events. `ActorId` is the **performer** (distinct from `UserId`, which for `task.assigned` stays the assignee for "assigned to me" user-activity). Membership uses the caller's `X-User-Id` (Tasks REST trusts that header; it does not validate bearers), refining the design's "forward the bearer".

---

## File structure

**Contracts** — `src/shared/TaskManager.Contracts/Events/`: `TaskUpdatedEvent.cs`, `TaskDeletedEvent.cs`.

**Tasks** — `Application/Handlers/TaskCommandHandlers.cs` (Update + Delete publish the new events), `Infrastructure/DependencyInjection.cs` (two `MapEvent` routing lines).

**Analytics:**
- `Domain/ReadModels/TaskEventRecord.cs` (+`ActorId`, +`TaskTitle`), `Infrastructure/Persistence/AnalyticsDbContext.cs` (column config + index), new EF migration.
- `Application/EventProjector.cs` (project `ActorId`/`TaskTitle` for all event types + 2 new), `Application/DTOs/AnalyticsDtos.cs` (`BoardActivityItemDto`), `Application/AnalyticsQueryService.cs` (`GetBoardActivityAsync`).
- `Domain/Interfaces/IAnalyticsRepository.cs` + `Infrastructure/Persistence/AnalyticsRepository.cs` (`GetBoardActivityAsync`).
- `Application/Interfaces/IBoardMembershipChecker.cs` + `Infrastructure/Http/TasksBoardMembershipChecker.cs` + DI + `docker-compose.yml` `TASKS_URL`.
- `Infrastructure/Messaging/AnalyticsConsumers.cs` + `Infrastructure/DependencyInjection.cs` (2 consumers + bindings).
- `Presentation/Endpoints/AnalyticsEndpoints.cs` (activity endpoint).

**Frontend:**
- `core/models/analytics.models.ts` (`BoardActivityItemDto`), `core/http/analytics-api.service.ts` (`getBoardActivity`), `core/users/user-name.service.ts` (memoizing id→name).
- `features/boards/board-activity-panel.component.ts` (new), `features/boards/board-detail.component.ts` (host the panel + refresh on realtime signal).

**Tests:** `tests/TaskManager.Tasks.Tests` (publish unit tests), `tests/TaskManager.Analytics.Tests` (projector unit, query unit, endpoint integration), Jest specs, `tests/TaskManager.E2E.Tests` (feed flow).

---

### Task 0: Branch setup

- [ ] **Step 0.1: Create the branch**

```bash
git checkout develop && git pull --ff-only origin develop
git checkout -b feature/activity-feed
```

---

### Task 1: Contract events

**Files:**
- Create: `src/shared/TaskManager.Contracts/Events/TaskUpdatedEvent.cs`
- Create: `src/shared/TaskManager.Contracts/Events/TaskDeletedEvent.cs`

- [ ] **Step 1.1: Create the records**

Create `TaskUpdatedEvent.cs`:
```csharp
namespace TaskManager.Contracts.Events;

// Title + ActorId carried so Analytics renders the board activity feed without a
// cross-service lookup (spec §13.4). OccurredAt is the edit time.
public record TaskUpdatedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid ActorId,
    DateTimeOffset OccurredAt);
```

Create `TaskDeletedEvent.cs`:
```csharp
namespace TaskManager.Contracts.Events;

public record TaskDeletedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid ActorId,
    DateTimeOffset OccurredAt);
```

- [ ] **Step 1.2: Build the Contracts project**

```bash
dotnet build src/shared/TaskManager.Contracts --no-restore
```
Expected: 0 errors.

- [ ] **Step 1.3: Commit**

```bash
git add src/shared/TaskManager.Contracts/Events/
git commit -m "feat(contracts): TaskUpdatedEvent and TaskDeletedEvent for the activity feed"
```

---

### Task 2: Tasks publishes the new events

`PUT /api/tasks/{id}` (update) publishes `TaskUpdatedEvent`; `DELETE /api/tasks/{id}` publishes `TaskDeletedEvent`. Both ride the existing EF outbox.

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/TaskCommandHandlers.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Infrastructure/DependencyInjection.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/TaskCommandHandlerTests.cs`

- [ ] **Step 2.0: READ FIRST**
Read `TaskCommandHandlers.cs` — find `UpdateTaskCommandHandler` and `DeleteTaskCommandHandler`. Note their constructor parameters (which inject `IEventPublisher publisher`? The comment/assign handlers do; confirm Update's and Delete's). Note how `AddCommentCommandHandler` publishes: `await publisher.PublishAsync(new TaskCommentAddedEvent(...), ct);` BEFORE `SaveChangesAsync`. The outbox makes publish+save atomic. Read `Infrastructure/DependencyInjection.cs` lines ~55-69 for the `MapEvent<T>(cfg, routingKey)` pattern.

- [ ] **Step 2.1: Write the failing tests**

In `tests/TaskManager.Tasks.Tests/Unit/TaskCommandHandlerTests.cs`, add (adapt fixture field names — `_tasks`, `_boards`, `_uow`, `_publisher` — and the handler constructor calls to match the existing update/delete tests EXACTLY; if Update/Delete handlers don't currently take `_publisher`, the test won't compile until Step 2.2 adds it):

```csharp
    [Fact]
    public async Task UpdateTaskCommandHandler_OnSuccess_PublishesTaskUpdatedEvent()
    {
        var (boardId, editor) = (Guid.NewGuid(), Guid.NewGuid());
        var task = Fake.Task(boardId);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(boardId, editor, Arg.Any<CancellationToken>()).Returns(BoardRole.Editor);
        var handler = new UpdateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(
            new UpdateTaskCommand(task.Id, "new title", null, "Medium", null, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskUpdatedEvent>(e => e.TaskId == task.Id && e.BoardId == boardId && e.ActorId == editor && e.Title == "new title"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTaskCommandHandler_OnSuccess_PublishesTaskDeletedEvent()
    {
        var (boardId, editor) = (Guid.NewGuid(), Guid.NewGuid());
        var task = Fake.Task(boardId);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(boardId, editor, Arg.Any<CancellationToken>()).Returns(BoardRole.Editor);
        var handler = new DeleteTaskCommandHandler(_tasks, _boards, _uow, _publisher);

        var result = await handler.Handle(new DeleteTaskCommand(task.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskDeletedEvent>(e => e.TaskId == task.Id && e.BoardId == boardId && e.ActorId == editor),
            Arg.Any<CancellationToken>());
    }
```
The test file already has a `_publisher = Substitute.For<IEventPublisher>()` field if other tests use it; if not, add it. `Mapper` is the existing `TasksMapper` field. Confirm `UpdateTaskCommand`'s positional shape from `TaskCommands.cs` and match it.

- [ ] **Step 2.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~PublishesTaskUpdatedEvent|FullyQualifiedName~PublishesTaskDeletedEvent"
```
Expected: FAIL to compile (handlers don't take `_publisher` and/or don't publish) or assertion fails.

- [ ] **Step 2.3: Publish from the Update handler**

In `UpdateTaskCommandHandler`: if it does not already inject `IEventPublisher publisher`, add it to the primary constructor (mirror `AddCommentCommandHandler`'s ctor). After the successful mutation and BEFORE/at the same point other handlers publish (before `SaveChangesAsync`), add:
```csharp
        await publisher.PublishAsync(
            new TaskUpdatedEvent(task.Id, task.BoardId, task.Title, cmd.UserId, DateTimeOffset.UtcNow), ct);
```
Place it after `task.Update(...)` succeeds and after the RowVersion check, alongside the existing save. Keep the existing concurrency try/catch. Ensure `using TaskManager.Contracts.Events;` is present (it is — other handlers use it).

- [ ] **Step 2.4: Publish from the Delete handler**

In `DeleteTaskCommandHandler`: add `IEventPublisher publisher` to the primary constructor. After the authorization check and `tasks.Remove(task)` (or wherever it removes) and before `SaveChangesAsync`, add:
```csharp
        await publisher.PublishAsync(
            new TaskDeletedEvent(task.Id, task.BoardId, task.Title, cmd.UserId, DateTimeOffset.UtcNow), ct);
```
Keep returning `Result.Ok(task.BoardId)` (from Feature 3).

- [ ] **Step 2.5: Add publisher routing for the two events**

In `Infrastructure/DependencyInjection.cs`, next to the existing `MapEvent<...>` lines, add:
```csharp
        MapEvent<TaskUpdatedEvent>(cfg, "task.updated");
        MapEvent<TaskDeletedEvent>(cfg, "task.deleted");
```

- [ ] **Step 2.6: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~PublishesTaskUpdatedEvent|FullyQualifiedName~PublishesTaskDeletedEvent"
dotnet build SmartTaskManager.sln --no-restore
```
Expected: both tests pass; solution builds. Run the broader update/delete handler tests too to confirm no regression:
```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~UpdateTaskCommandHandler|FullyQualifiedName~DeleteTaskCommandHandler"
```

- [ ] **Step 2.7: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Application/Handlers/TaskCommandHandlers.cs src/services/tasks/TaskManager.Tasks/Infrastructure/DependencyInjection.cs tests/TaskManager.Tasks.Tests/Unit/TaskCommandHandlerTests.cs
git commit -m "feat(tasks): publish TaskUpdatedEvent/TaskDeletedEvent via the outbox"
```

---

### Task 3: Analytics consumers + bindings for the new events

**Files:**
- Modify: `src/services/analytics/TaskManager.Analytics/Infrastructure/Messaging/AnalyticsConsumers.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Infrastructure/DependencyInjection.cs`

- [ ] **Step 3.1: Add the consumers**

In `AnalyticsConsumers.cs`, append two consumers mirroring the existing ones:
```csharp
public class TaskUpdatedEventConsumer(EventProjector projector) : IConsumer<TaskUpdatedEvent>
{
    public Task Consume(ConsumeContext<TaskUpdatedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskDeletedEventConsumer(EventProjector projector) : IConsumer<TaskDeletedEvent>
{
    public Task Consume(ConsumeContext<TaskDeletedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}
```

- [ ] **Step 3.2: Register + bind the consumers**

In `Infrastructure/DependencyInjection.cs`:
1. After the existing `x.AddConsumer<...>()` lines, add:
```csharp
            x.AddConsumer<TaskUpdatedEventConsumer>();
            x.AddConsumer<TaskDeletedEventConsumer>();
```
2. After the existing `MapTaskManagerEvent<...>` lines, add:
```csharp
                MapTaskManagerEvent<TaskUpdatedEvent>(cfg, "task.updated");
                MapTaskManagerEvent<TaskDeletedEvent>(cfg, "task.deleted");
```
3. After the existing `ReceiveFromTopic<...>` lines, add:
```csharp
                ReceiveFromTopic<TaskUpdatedEventConsumer>(context, cfg, "analytics-task-updated", "task.updated");
                ReceiveFromTopic<TaskDeletedEventConsumer>(context, cfg, "analytics-task-deleted", "task.deleted");
```

- [ ] **Step 3.3: Build**

```bash
dotnet build src/services/analytics/TaskManager.Analytics --no-restore
```
Expected: 0 errors. (Projector handling of these events is Task 4 — the projector's default case currently ignores unknown events, so this compiles and is harmless until Task 4.)

- [ ] **Step 3.4: Commit**

```bash
git add src/services/analytics/TaskManager.Analytics/Infrastructure/
git commit -m "feat(analytics): consume task.updated/task.deleted from the topic exchange"
```

---

### Task 4: Read-model columns + migration + projector enrichment

**Files:**
- Modify: `src/services/analytics/TaskManager.Analytics/Domain/ReadModels/TaskEventRecord.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Infrastructure/Persistence/AnalyticsDbContext.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Application/EventProjector.cs`
- Test: `tests/TaskManager.Analytics.Tests/Unit/EventProjectorTests.cs`
- Generated: a new EF migration.

- [ ] **Step 4.0: READ FIRST**
Read `tests/TaskManager.Analytics.Tests/Unit/EventProjectorTests.cs` — note how it constructs the projector (substituted `IAnalyticsRepository`/`IUnitOfWork`) and asserts `AddEvent` was called with a `TaskEventRecord`. Mirror that style.

- [ ] **Step 4.1: Add the columns to the read model**

In `TaskEventRecord.cs`, add two properties:
```csharp
    public Guid ActorId { get; set; }
    public string? TaskTitle { get; set; }
```

- [ ] **Step 4.2: Configure + index the new columns**

In `AnalyticsDbContext.cs`, inside the `Entity<TaskEventRecord>` config block, add a max length for the title and an index supporting the newest-per-board feed query:
```csharp
            e.Property(x => x.TaskTitle).HasMaxLength(200);
            e.HasIndex(x => new { x.BoardId, x.OccurredAt });
```
(Add these alongside the existing `e.HasIndex(...)` lines.)

- [ ] **Step 4.3: Write the failing projector tests**

Append to `EventProjectorTests.cs`:
```csharp
    [Fact]
    public async Task ProjectAsync_TaskAssigned_RecordsActorAsAssigner_NotAssignee()
    {
        TaskEventRecord? captured = null;
        _repository.When(r => r.AddEvent(Arg.Any<TaskEventRecord>())).Do(ci => captured = ci.Arg<TaskEventRecord>());
        var projector = new EventProjector(_repository, _uow);
        var assignedTo = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();
        var e = new TaskAssignedEvent(Guid.NewGuid(), Guid.NewGuid(), "Ship it", assignedTo, assignedBy, null);

        await projector.ProjectAsync(e, default);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(assignedTo, "user activity keeps the assignee");
        captured.ActorId.Should().Be(assignedBy, "the board feed actor is the assigner");
        captured.TaskTitle.Should().Be("Ship it");
    }

    [Fact]
    public async Task ProjectAsync_TaskUpdated_RecordsActorAndTitle()
    {
        TaskEventRecord? captured = null;
        _repository.When(r => r.AddEvent(Arg.Any<TaskEventRecord>())).Do(ci => captured = ci.Arg<TaskEventRecord>());
        var projector = new EventProjector(_repository, _uow);
        var actor = Guid.NewGuid();
        var when = DateTimeOffset.UtcNow;
        var e = new TaskUpdatedEvent(Guid.NewGuid(), Guid.NewGuid(), "Renamed", actor, when);

        await projector.ProjectAsync(e, default);

        captured!.EventType.Should().Be("task.updated");
        captured.ActorId.Should().Be(actor);
        captured.TaskTitle.Should().Be("Renamed");
        captured.OccurredAt.Should().Be(when);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectAsync_TaskDeleted_RecordsDeletion()
    {
        TaskEventRecord? captured = null;
        _repository.When(r => r.AddEvent(Arg.Any<TaskEventRecord>())).Do(ci => captured = ci.Arg<TaskEventRecord>());
        var projector = new EventProjector(_repository, _uow);
        var actor = Guid.NewGuid();
        var e = new TaskDeletedEvent(Guid.NewGuid(), Guid.NewGuid(), "Gone", actor, DateTimeOffset.UtcNow);

        await projector.ProjectAsync(e, default);

        captured!.EventType.Should().Be("task.deleted");
        captured.ActorId.Should().Be(actor);
        captured.TaskTitle.Should().Be("Gone");
    }

    [Fact]
    public async Task ProjectAsync_TaskCreated_RecordsActorAndTitle()
    {
        TaskEventRecord? captured = null;
        _repository.When(r => r.AddEvent(Arg.Any<TaskEventRecord>())).Do(ci => captured = ci.Arg<TaskEventRecord>());
        var projector = new EventProjector(_repository, _uow);
        var creator = Guid.NewGuid();
        var e = new TaskCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "Born", creator, DateTimeOffset.UtcNow);

        await projector.ProjectAsync(e, default);

        captured!.ActorId.Should().Be(creator);
        captured.TaskTitle.Should().Be("Born");
    }
```
The fixture's substituted repository/uow fields are likely `_repository` and `_uow` — confirm and match.

- [ ] **Step 4.4: Run to verify failure**

```bash
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~EventProjectorTests"
```
Expected: FAIL — `TaskEventRecord` has no `ActorId`/`TaskTitle`, and the projector doesn't set them or handle the new events.

- [ ] **Step 4.5: Extend the projector**

In `EventProjector.cs`, change the `Record` helper to also accept actor + title, and update every case to pass them. Replace the `Record` method and each `case` Record call:

```csharp
    public async Task ProjectAsync(object @event, CancellationToken ct = default)
    {
        switch (@event)
        {
            case TaskCreatedEvent e:
                Record(e.TaskId, e.BoardId, "task.created", e.CreatedBy, e.CreatedBy, e.Title, e.CreatedAt);
                await repository.ApplyBoardDeltaAsync(e.BoardId, totalDelta: 1, completedDelta: 0, overdueDelta: 0, ct);
                await repository.ApplyUserDeltaAsync(e.CreatedBy, createdDelta: 1, completedDelta: 0, assignedDelta: 0, ct);
                break;

            case TaskCompletedEvent e:
                Record(e.TaskId, e.BoardId, "task.completed", e.CompletedBy, e.CompletedBy, e.Title, e.CompletedAt);
                await repository.ApplyBoardDeltaAsync(e.BoardId, totalDelta: 0, completedDelta: 1, overdueDelta: 0, ct);
                await repository.ApplyUserDeltaAsync(e.CompletedBy, createdDelta: 0, completedDelta: 1, assignedDelta: 0, ct);
                break;

            case TaskAssignedEvent e:
                // UserId stays the assignee (their "assigned to me" activity); ActorId is the assigner.
                Record(e.TaskId, e.BoardId, "task.assigned", e.AssignedTo, e.AssignedBy, e.Title, DateTimeOffset.UtcNow);
                await repository.ApplyUserDeltaAsync(e.AssignedTo, createdDelta: 0, completedDelta: 0, assignedDelta: 1, ct);
                break;

            case TaskStatusChangedEvent e:
                Record(e.TaskId, e.BoardId, "task.status-changed", e.ChangedBy, e.ChangedBy, e.Title, DateTimeOffset.UtcNow);
                break;

            case TaskCommentAddedEvent e:
                Record(e.TaskId, e.BoardId, "task.comment-added", e.AuthorId, e.AuthorId, e.Title, DateTimeOffset.UtcNow);
                break;

            case TaskUpdatedEvent e:
                Record(e.TaskId, e.BoardId, "task.updated", e.ActorId, e.ActorId, e.Title, e.OccurredAt);
                break;

            case TaskDeletedEvent e:
                Record(e.TaskId, e.BoardId, "task.deleted", e.ActorId, e.ActorId, e.Title, e.OccurredAt);
                break;

            default:
                // DeadlineApproachingEvent etc. — system events, not user activity.
                return;
        }

        await uow.SaveChangesAsync(ct);
    }

    private void Record(Guid taskId, Guid boardId, string eventType, Guid userId, Guid actorId, string? taskTitle, DateTimeOffset occurredAt)
        => repository.AddEvent(new TaskEventRecord
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            BoardId = boardId,
            EventType = eventType,
            UserId = userId,
            ActorId = actorId,
            TaskTitle = taskTitle,
            OccurredAt = occurredAt,
        });
```
Ensure `using TaskManager.Contracts.Events;` is present (it is).

- [ ] **Step 4.6: Run to verify pass**

```bash
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~EventProjectorTests"
```
Expected: PASS (existing projector tests + the 4 new ones).

- [ ] **Step 4.7: Generate the migration**

```bash
dotnet tool restore 2>$null; dotnet ef migrations add AddActivityColumns --project src/services/analytics/TaskManager.Analytics --context AnalyticsDbContext
```
(If `dotnet ef` is missing: `dotnet tool install --global dotnet-ef --version 10.*`.) Open the new migration and confirm `Up()` adds `ActorId` (uuid) + `TaskTitle` (varchar(200), nullable) to `task_events` and creates the `IX_task_events_BoardId_OccurredAt` index. It must NOT drop/recreate the table or touch inbox/outbox tables.

- [ ] **Step 4.8: Build to verify**

```bash
dotnet build src/services/analytics/TaskManager.Analytics --no-restore
```
Expected: 0 errors.

- [ ] **Step 4.9: Commit**

```bash
git add src/services/analytics/TaskManager.Analytics/Domain/ReadModels/TaskEventRecord.cs src/services/analytics/TaskManager.Analytics/Infrastructure/Persistence/ src/services/analytics/TaskManager.Analytics/Application/EventProjector.cs tests/TaskManager.Analytics.Tests/Unit/EventProjectorTests.cs
git commit -m "feat(analytics): project ActorId + TaskTitle, handle task.updated/deleted"
```

---

### Task 5: Board activity query (repository + DTO + query service)

**Files:**
- Modify: `src/services/analytics/TaskManager.Analytics/Domain/Interfaces/IAnalyticsRepository.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Infrastructure/Persistence/AnalyticsRepository.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Application/DTOs/AnalyticsDtos.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Application/AnalyticsQueryService.cs`
- Test: `tests/TaskManager.Analytics.Tests/Unit/AnalyticsQueryServiceTests.cs`

- [ ] **Step 5.1: Add the DTO**

In `AnalyticsDtos.cs`, add:
```csharp
public record BoardActivityItemDto(string EventType, Guid TaskId, string? TaskTitle, Guid ActorId, DateTimeOffset OccurredAt);
```

- [ ] **Step 5.2: Add the repository method**

In `IAnalyticsRepository.cs`, add:
```csharp
    /// <summary>Newest-first activity for a board (capped by <paramref name="count"/>).</summary>
    Task<List<TaskEventRecord>> GetBoardActivityAsync(Guid boardId, int count, CancellationToken ct = default);
```

In `AnalyticsRepository.cs`, add:
```csharp
    public Task<List<TaskEventRecord>> GetBoardActivityAsync(Guid boardId, int count, CancellationToken ct = default)
        => db.TaskEvents.AsNoTracking()
            .Where(e => e.BoardId == boardId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToListAsync(ct);
```

- [ ] **Step 5.3: Write the failing query-service test**

Append to `AnalyticsQueryServiceTests.cs` (match the fixture's substituted `_repository` field name):
```csharp
    [Fact]
    public async Task GetBoardActivityAsync_MapsNewestFirst_WithActorAndTitle()
    {
        var boardId = Guid.NewGuid();
        var rows = new List<TaskEventRecord>
        {
            new() { Id = Guid.NewGuid(), BoardId = boardId, TaskId = Guid.NewGuid(), EventType = "task.updated",
                    UserId = Guid.NewGuid(), ActorId = Guid.NewGuid(), TaskTitle = "Edited", OccurredAt = DateTimeOffset.UtcNow },
        };
        _repository.GetBoardActivityAsync(boardId, 50, Arg.Any<CancellationToken>()).Returns(rows);
        var service = new AnalyticsQueryService(_repository);

        var result = await service.GetBoardActivityAsync(boardId, 50, default);

        result.Should().ContainSingle();
        result[0].EventType.Should().Be("task.updated");
        result[0].TaskTitle.Should().Be("Edited");
        result[0].ActorId.Should().Be(rows[0].ActorId);
    }

    [Fact]
    public async Task GetBoardActivityAsync_ClampsCountTo100()
    {
        var boardId = Guid.NewGuid();
        _repository.GetBoardActivityAsync(boardId, 100, Arg.Any<CancellationToken>())
            .Returns(new List<TaskEventRecord>());
        var service = new AnalyticsQueryService(_repository);

        await service.GetBoardActivityAsync(boardId, 9999, default);

        await _repository.Received(1).GetBoardActivityAsync(boardId, 100, Arg.Any<CancellationToken>());
    }
```

- [ ] **Step 5.4: Run to verify failure**

```bash
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~GetBoardActivityAsync"
```
Expected: FAIL — `AnalyticsQueryService.GetBoardActivityAsync` doesn't exist.

- [ ] **Step 5.5: Add the query-service method**

In `AnalyticsQueryService.cs`, add a constant and method:
```csharp
    private const int MaxBoardActivity = 100;

    /// <summary>Newest-first board activity, count clamped to [1, 100].</summary>
    public async Task<IReadOnlyList<BoardActivityItemDto>> GetBoardActivityAsync(Guid boardId, int count, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(count, 1, MaxBoardActivity);
        var events = await repository.GetBoardActivityAsync(boardId, clamped, ct);
        return events
            .Select(e => new BoardActivityItemDto(e.EventType, e.TaskId, e.TaskTitle, e.ActorId, e.OccurredAt))
            .ToList();
    }
```

- [ ] **Step 5.6: Run to verify pass**

```bash
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~GetBoardActivityAsync"
```
Expected: PASS (2 tests).

- [ ] **Step 5.7: Commit**

```bash
git add src/services/analytics/TaskManager.Analytics/Domain/Interfaces/IAnalyticsRepository.cs src/services/analytics/TaskManager.Analytics/Infrastructure/Persistence/AnalyticsRepository.cs src/services/analytics/TaskManager.Analytics/Application/DTOs/AnalyticsDtos.cs src/services/analytics/TaskManager.Analytics/Application/AnalyticsQueryService.cs tests/TaskManager.Analytics.Tests/Unit/AnalyticsQueryServiceTests.cs
git commit -m "feat(analytics): board activity query (newest-first, count-clamped)"
```

---

### Task 6: Board-membership checker (Analytics → Tasks)

Analytics verifies the caller is a board member by calling Tasks `GET /api/boards/{boardId}` with the caller's `X-User-Id` (Tasks REST trusts that header). 200 ⇒ member.

**Files:**
- Create: `src/services/analytics/TaskManager.Analytics/Application/Interfaces/IBoardMembershipChecker.cs`
- Create: `src/services/analytics/TaskManager.Analytics/Infrastructure/Http/TasksBoardMembershipChecker.cs`
- Modify: `src/services/analytics/TaskManager.Analytics/Infrastructure/DependencyInjection.cs`
- Modify: `docker-compose.yml` (add `TASKS_URL` to `analytics-svc`)
- Modify: `src/services/analytics/TaskManager.Analytics/appsettings.Development.json` (local `TASKS_URL`)

- [ ] **Step 6.0: READ FIRST**
Read `src/services/notifications/TaskManager.Notifications/Infrastructure/DependencyInjection.cs` (or wherever Notifications registers its typed `HttpClient` for `IdentityUserDirectory`) for the `services.AddHttpClient<TInterface, TImpl>(c => c.BaseAddress = new Uri(config["IDENTITY_URL"]...))` pattern. Read `docker-compose.yml` around `notifications-svc` for the `IDENTITY_URL: http://identity-svc:8080` env line. Read `src/services/analytics/.../Presentation/Extensions/HttpContextExtensions.cs` for `GetUserId()`.

- [ ] **Step 6.1: Create the port**

Create `Application/Interfaces/IBoardMembershipChecker.cs` (create the `Application/Interfaces/` folder if absent):
```csharp
namespace TaskManager.Analytics.Application.Interfaces;

/// <summary>
/// Verifies a caller's board membership by asking Tasks (the membership owner). Keeps
/// Analytics free of a membership table and of Identity coupling (spec §13.4).
/// </summary>
public interface IBoardMembershipChecker
{
    Task<bool> IsMemberAsync(Guid boardId, Guid userId, CancellationToken ct = default);
}
```

- [ ] **Step 6.2: Create the HTTP adapter**

Create `Infrastructure/Http/TasksBoardMembershipChecker.cs`:
```csharp
using System.Net;
using TaskManager.Analytics.Application.Interfaces;

namespace TaskManager.Analytics.Infrastructure.Http;

/// <summary>
/// Calls Tasks GET /api/boards/{id} with the caller's X-User-Id (Tasks REST trusts the
/// gateway-style header). 200 ⇒ member; 403/404 ⇒ not a member. Base address from TASKS_URL.
/// </summary>
public class TasksBoardMembershipChecker(HttpClient http) : IBoardMembershipChecker
{
    public async Task<bool> IsMemberAsync(Guid boardId, Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/boards/{boardId}");
        request.Headers.Add("X-User-Id", userId.ToString());
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
```

- [ ] **Step 6.3: Register the typed client**

In `Infrastructure/DependencyInjection.cs`, inside `AddAnalyticsInfrastructure`, add (mirror Notifications' `AddHttpClient` registration):
```csharp
        var tasksUrl = config["TASKS_URL"] ?? "http://tasks-svc:8080";
        services.AddHttpClient<IBoardMembershipChecker, TasksBoardMembershipChecker>(c =>
            c.BaseAddress = new Uri(tasksUrl));
```
Add the needed usings: `using TaskManager.Analytics.Application.Interfaces;` and `using TaskManager.Analytics.Infrastructure.Http;`.

- [ ] **Step 6.4: Configure TASKS_URL for compose + local dev**

In `docker-compose.yml`, under `analytics-svc:` `environment:`, add (next to `ANALYTICS_DB_CONNECTION`):
```yaml
      TASKS_URL: http://tasks-svc:8080
```
In `src/services/analytics/TaskManager.Analytics/appsettings.Development.json`, add a `TASKS_URL` for local non-Docker runs pointing at the Tasks local dev port (check the gateway's tasks-cluster dev address — per CLAUDE.md/local notes it is `http://localhost:5159`; use that):
```json
  "TASKS_URL": "http://localhost:5159"
```
(Place it as a top-level key in that JSON. Confirm the exact local Tasks port from `src/gateway/.../appsettings.Development.json` `tasks-cluster` and match it.)

- [ ] **Step 6.5: Build**

```bash
dotnet build src/services/analytics/TaskManager.Analytics --no-restore
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~Architecture"
```
Expected: 0 errors; architecture tests pass (the port is in Application, the HTTP adapter in Infrastructure).

- [ ] **Step 6.6: Commit**

```bash
git add src/services/analytics/TaskManager.Analytics/Application/Interfaces/ src/services/analytics/TaskManager.Analytics/Infrastructure/Http/ src/services/analytics/TaskManager.Analytics/Infrastructure/DependencyInjection.cs src/services/analytics/TaskManager.Analytics/appsettings.Development.json docker-compose.yml
git commit -m "feat(analytics): board membership checker calling Tasks with caller X-User-Id"
```

---

### Task 7: Activity endpoint (membership-enforced) + integration test

**Files:**
- Modify: `src/services/analytics/TaskManager.Analytics/Presentation/Endpoints/AnalyticsEndpoints.cs`
- Test: `tests/TaskManager.Analytics.Tests/Integration/AnalyticsEndpointsTests.cs`

- [ ] **Step 7.0: READ FIRST**
Read `tests/TaskManager.Analytics.Tests/Integration/AnalyticsWebAppFactory.cs` and `AnalyticsEndpointsTests.cs` — note how a client is created, how `X-User-Id` is set, and whether the factory allows replacing a service for a test (`WithWebHostBuilder` / `ConfigureTestServices`). The activity endpoint depends on `IBoardMembershipChecker` (HTTP to Tasks), which isn't available in the isolated Analytics integration test — so the test replaces it with a stub.

- [ ] **Step 7.1: Add the endpoint**

In `AnalyticsEndpoints.cs`, add (the membership check needs the caller id + the injected checker):
```csharp
        group.MapGet("/boards/{boardId:guid}/activity",
            async (Guid boardId, int? count, HttpContext http,
                   AnalyticsQueryService queries, IBoardMembershipChecker membership, CancellationToken ct) =>
            {
                if (http.GetUserId() is not { } userId) return Results.Unauthorized();
                if (!await membership.IsMemberAsync(boardId, userId, ct)) return Results.Forbid();
                return Results.Ok(await queries.GetBoardActivityAsync(boardId, count ?? 50, ct));
            });
```
Add `using TaskManager.Analytics.Application.Interfaces;` at the top.

- [ ] **Step 7.2: Write the integration test**

Append to `AnalyticsEndpointsTests.cs`. The test seeds events via the projector or direct DB insert, stubs the membership checker, and checks 200-for-member / 403-for-non-member. Adapt the seeding + client helpers to the real factory API:
```csharp
    [Fact]
    public async Task GetBoardActivity_AsMember_ReturnsNewestFirst_AsNonMember_403()
    {
        var boardId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        // Seed two activity rows directly via the DbContext exposed by the factory.
        await factory.SeedActivityAsync(boardId, actor); // helper added below

        // Member: stub the checker to allow.
        var memberClient = factory.WithMembership(allow: true).As(actor);
        var ok = await memberClient.GetAsync($"/api/analytics/boards/{boardId}/activity?count=10");
        ok.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var items = await ok.Content.ReadFromJsonAsync<List<BoardActivityItemDto>>();
        items!.Should().NotBeEmpty();
        items.Should().BeInDescendingOrder(i => i.OccurredAt);

        // Non-member: stub the checker to deny.
        var strangerClient = factory.WithMembership(allow: false).As(Guid.NewGuid());
        var forbidden = await strangerClient.GetAsync($"/api/analytics/boards/{boardId}/activity");
        forbidden.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
```
You will need two small test helpers. Add to `AnalyticsWebAppFactory` (or a test extensions file, matching the existing test infra style):
- `SeedActivityAsync(Guid boardId, Guid actor)` — opens a scope, gets `AnalyticsDbContext`, adds two `TaskEventRecord`s (different `OccurredAt`) for the board, `SaveChangesAsync`.
- `WithMembership(bool allow)` — returns a factory/client whose `IBoardMembershipChecker` is replaced by a stub returning `allow` (via `WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.RemoveAll<IBoardMembershipChecker>(); s.AddSingleton<IBoardMembershipChecker>(new StubChecker(allow)); }))`). Define a tiny `private sealed class StubChecker(bool allow) : IBoardMembershipChecker { public Task<bool> IsMemberAsync(Guid b, Guid u, CancellationToken ct = default) => Task.FromResult(allow); }`.

If the existing factory does not expose a `Services`/scope accessor or an `As(...)` client helper, mirror whatever the existing `AnalyticsEndpointsTests` use to set `X-User-Id` and to reach the DbContext; the key behaviors to assert are 200+ordering for a member and 403 for a non-member.

- [ ] **Step 7.3: Build (Docker required to RUN; build to verify compile)**

```bash
dotnet build tests/TaskManager.Analytics.Tests --no-restore
```
Expected: 0 errors. If Docker is up locally, run:
```bash
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~GetBoardActivity"
```
Otherwise rely on the CI `test-dotnet (analytics)` check.

- [ ] **Step 7.4: Commit**

```bash
git add src/services/analytics/TaskManager.Analytics/Presentation/Endpoints/AnalyticsEndpoints.cs tests/TaskManager.Analytics.Tests/
git commit -m "feat(analytics): GET /boards/{id}/activity (membership-enforced) + integration test"
```

---

### Task 8: SPA models, API method, and user-name cache

**Files:**
- Modify: `frontend/task-manager-app/src/app/core/models/analytics.models.ts`
- Modify: `frontend/task-manager-app/src/app/core/http/analytics-api.service.ts`
- Create: `frontend/task-manager-app/src/app/core/users/user-name.service.ts`
- Create: `frontend/task-manager-app/src/app/core/users/index.ts`
- Modify: `frontend/task-manager-app/src/app/testing/factories.ts` (add `makeBoardActivity`)
- Test: `frontend/task-manager-app/src/app/core/http/analytics-api.service.spec.ts` (if it exists; else add a focused spec)
- Test: `frontend/task-manager-app/src/app/core/users/user-name.service.spec.ts`

- [ ] **Step 8.1: Add the model + factory**

In `analytics.models.ts`, add:
```typescript
export interface BoardActivityItemDto {
  eventType: string;
  taskId: string;
  taskTitle: string | null;
  actorId: string;
  occurredAt: string;
}
```
In `factories.ts` (add `BoardActivityItemDto` to the `../core/models` import), add after `makeActivity`:
```typescript
export const makeBoardActivity = (overrides: Partial<BoardActivityItemDto> = {}): BoardActivityItemDto => ({
  eventType: 'task.updated',
  taskId: nextGuid(),
  taskTitle: 'A task',
  actorId: nextGuid(),
  occurredAt: '2026-06-01T00:00:00Z',
  ...overrides,
});
```

- [ ] **Step 8.2: Add the API method**

In `analytics-api.service.ts`, add `BoardActivityItemDto` to the `../models` import and add (uses `HttpParams` for `count`):
```typescript
  getBoardActivity(boardId: string, count = 50): Observable<BoardActivityItemDto[]> {
    return this.http.get<BoardActivityItemDto[]>(
      apiUrl(`/api/analytics/boards/${boardId}/activity`),
      { params: new HttpParams().set('count', count) },
    );
  }
```
Add `HttpParams` to the `@angular/common/http` import.

- [ ] **Step 8.3: Create the user-name cache**

Create `core/users/user-name.service.ts`:
```typescript
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UsersApiService } from '../http/users-api.service';

/**
 * Memoizing id → display-name resolver over Identity's GET /api/users/{id}. One in-flight
 * request per id; resolved names are cached for the session. Keeps the activity feed (and
 * any future name display) from issuing N duplicate lookups.
 */
@Injectable({ providedIn: 'root' })
export class UserNameService {
  private readonly usersApi = inject(UsersApiService);
  private readonly cache = new Map<string, Promise<string>>();

  resolve(userId: string): Promise<string> {
    const hit = this.cache.get(userId);
    if (hit) return hit;
    const pending = firstValueFrom(this.usersApi.getById(userId))
      .then((u) => u.displayName)
      .catch(() => 'Someone'); // a deleted/unknown user still renders
    this.cache.set(userId, pending);
    return pending;
  }
}
```
Create `core/users/index.ts`:
```typescript
export * from './user-name.service';
```

- [ ] **Step 8.4: Write the tests**

Create `core/users/user-name.service.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { apiUrl } from '../http/api-base';
import { UserNameService } from './user-name.service';

describe('UserNameService', () => {
  let service: UserNameService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(UserNameService);
    http = TestBed.inject(HttpTestingController);
  });

  it('resolves a display name and caches it (one HTTP call for repeat ids)', async () => {
    const p1 = service.resolve('user-1');
    const req = http.expectOne(apiUrl('/api/users/user-1'));
    req.flush({ id: 'user-1', email: 'a@b.c', displayName: 'Alice', avatarUrl: null });
    await expect(p1).resolves.toBe('Alice');

    const p2 = service.resolve('user-1');
    http.expectNone(apiUrl('/api/users/user-1')); // served from cache
    await expect(p2).resolves.toBe('Alice');
  });

  it('falls back to "Someone" when the lookup fails', async () => {
    const p = service.resolve('ghost');
    http.expectOne(apiUrl('/api/users/ghost')).flush('nope', { status: 404, statusText: 'Not Found' });
    await expect(p).resolves.toBe('Someone');
  });
});
```
If `analytics-api.service.spec.ts` exists, append a test for `getBoardActivity` mirroring the existing `getCompletionTrend` test (assert GET URL + `count` param). If it does not exist, skip the API-service spec (the method is thin and covered by the panel/E2E).

- [ ] **Step 8.5: Run + lint**

```bash
npx jest src/app/core/users/user-name.service
npx jest
npm run lint
```
Expected: new tests pass; full suite green; lint clean.

- [ ] **Step 8.6: Commit**

```bash
git add frontend/task-manager-app/src/app/core/models/analytics.models.ts frontend/task-manager-app/src/app/core/http/analytics-api.service.ts frontend/task-manager-app/src/app/core/users/ frontend/task-manager-app/src/app/testing/factories.ts
git commit -m "feat(frontend): board activity model/API + memoizing user-name cache"
```

---

### Task 9: Activity panel + board-detail wiring

**Files:**
- Create: `frontend/task-manager-app/src/app/features/boards/board-activity-panel.component.ts`
- Modify: `frontend/task-manager-app/src/app/features/boards/board-detail.component.ts`

- [ ] **Step 9.1: Create the panel component**

Create `board-activity-panel.component.ts`. It is a smart-ish component: given a `boardId` input and a `refreshSignal` input (a number that increments when a realtime frame arrives), it loads the feed and resolves actor names. Collapsible; manual refresh button.

```typescript
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { BoardActivityItemDto } from '../../core/models';
import { AnalyticsApiService } from '../../core/http/analytics-api.service';
import { UserNameService } from '../../core/users';

const VERBS: Record<string, string> = {
  'task.created': 'created',
  'task.updated': 'updated',
  'task.status-changed': 'moved',
  'task.completed': 'completed',
  'task.assigned': 'assigned',
  'task.comment-added': 'commented on',
  'task.deleted': 'deleted',
};

interface ActivityRow {
  readonly key: string;
  readonly actorName: string;
  readonly verb: string;
  readonly title: string;
  readonly occurredAt: string;
}

// Collapsible per-board activity feed. Reloads when refreshSignal() changes (a Feature-3
// realtime frame arrived for this board) and on the manual refresh button.
@Component({
  selector: 'tm-board-activity-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatIconModule],
  template: `
    <section class="rounded-xl bg-white p-3 shadow-sm" data-testid="activity-panel">
      <header class="flex items-center gap-2">
        <button mat-icon-button type="button" (click)="open.set(!open())" [attr.aria-label]="open() ? 'Collapse activity' : 'Expand activity'">
          <mat-icon>{{ open() ? 'expand_less' : 'expand_more' }}</mat-icon>
        </button>
        <h2 class="flex-1 text-sm font-semibold text-slate-600">Activity</h2>
        <button mat-icon-button type="button" data-testid="activity-refresh" aria-label="Refresh activity" (click)="reload()">
          <mat-icon>refresh</mat-icon>
        </button>
      </header>

      @if (open()) {
        @if (rows().length === 0) {
          <p class="px-2 py-3 text-sm text-slate-400">No activity yet.</p>
        } @else {
          <ul class="flex flex-col gap-1 pt-1">
            @for (row of rows(); track row.key) {
              <li class="px-2 py-1 text-sm text-slate-700" data-testid="activity-item">
                <span class="font-medium">{{ row.actorName }}</span>
                {{ row.verb }}
                <span class="font-medium">{{ row.title }}</span>
                <span class="text-slate-400">· {{ row.occurredAt | date: 'MMM d, h:mm a' }}</span>
              </li>
            }
          </ul>
        }
      }
    </section>
  `,
})
export class BoardActivityPanelComponent {
  private readonly analyticsApi = inject(AnalyticsApiService);
  private readonly userNames = inject(UserNameService);

  readonly boardId = input.required<string>();
  /** Increments when a realtime frame for this board arrives; triggers a reload. */
  readonly refreshSignal = input(0);

  readonly open = signal(true);
  readonly rows = signal<ActivityRow[]>([]);

  constructor() {
    // Reload whenever the boardId or the refresh signal changes.
    effect(() => {
      const id = this.boardId();
      this.refreshSignal(); // tracked dependency
      void this.load(id);
    });
  }

  protected reload(): void {
    void this.load(this.boardId());
  }

  private async load(boardId: string): Promise<void> {
    try {
      const items = await firstValueFrom(this.analyticsApi.getBoardActivity(boardId, 50));
      const rows = await Promise.all(items.map((i) => this.toRow(i)));
      this.rows.set(rows);
    } catch {
      // leave the last good list; the manual refresh button lets the user retry
    }
  }

  private async toRow(item: BoardActivityItemDto): Promise<ActivityRow> {
    const actorName = await this.userNames.resolve(item.actorId);
    return {
      key: `${item.taskId}:${item.eventType}:${item.occurredAt}`,
      actorName,
      verb: VERBS[item.eventType] ?? item.eventType,
      title: item.taskTitle ?? 'a task',
      occurredAt: item.occurredAt,
    };
  }
}
```

- [ ] **Step 9.2: Host the panel in board-detail + refresh on realtime**

In `board-detail.component.ts`:
1. Add imports:
```typescript
import { BoardActivityPanelComponent } from './board-activity-panel.component';
```
Add `BoardActivityPanelComponent` to the component `imports` array.

2. Add a signal that increments on each realtime frame, and bump it in the existing realtime handlers. Add a field:
```typescript
  protected readonly activityTick = signal(0);
```
(Add `signal` to the `@angular/core` import if not already present.) In the `ngOnInit` `realtime.join(...)` handlers (added in Feature 3), bump the tick on upsert/delete so the panel reloads:
```typescript
    void this.realtime.join(this.boardId, {
      onUpsert: (task) => { this.store.applyRealtimeUpsert(task); this.activityTick.update((n) => n + 1); },
      onDelete: (taskId) => { this.store.applyRealtimeDelete(taskId); this.activityTick.update((n) => n + 1); },
      onReconnected: () => { void this.store.loadBoard(this.boardId); this.activityTick.update((n) => n + 1); },
    });
```

3. In the template, add the panel. Place it after the kanban columns `</div>` (the `grid ... xl:grid-cols-4` block's closing tag), inside the `<main>`:
```html
      <tm-board-activity-panel class="mt-6 block" [boardId]="boardId" [refreshSignal]="activityTick()" />
```
(`boardId` is the existing component field — a string. Confirm it is accessible from the template; it is a class field. If it is `private`, change it to `protected readonly boardId` so the template can bind it.)

- [ ] **Step 9.3: Verify build, lint, full suite**

```bash
npx ng build --configuration development
npm run lint
npx jest
```
Expected: all green. If `board-detail.component.spec.ts` now needs `AnalyticsApiService`/`UserNameService` providers, they are `providedIn: 'root'` and the panel only calls them on `effect` (which runs in the test) — provide `provideHttpClient()` + `provideHttpClientTesting()` in that spec if a real HTTP call is attempted, OR ensure the existing `BoardRealtimeService` stub keeps the panel from erroring. If the panel's `effect` fires an HTTP GET in the board-detail spec, flush/ignore it via `provideHttpClientTesting`. Report any spec changes.

- [ ] **Step 9.4: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/board-activity-panel.component.ts frontend/task-manager-app/src/app/features/boards/board-detail.component.ts
git commit -m "feat(frontend): collapsible board activity panel, live-refreshed by the hub signal"
```

---

### Task 10: E2E — a member's action shows in the feed

**Files:**
- Modify: `tests/TaskManager.E2E.Tests/Infrastructure/Flows.cs` (if a helper is useful)
- Create: `tests/TaskManager.E2E.Tests/ActivityFeedFlowTests.cs`

- [ ] **Step 10.0: READ FIRST**
Read `tests/TaskManager.E2E.Tests/Infrastructure/Flows.cs` (the `RegisterAsync`/`CreateBoardAsync`/`OpenBoardAsync`/`CreateTaskAsync`/`DragTaskToColumnAsync`/`TaskCard` helpers) and `BoardAndTaskFlowTests.cs` (class header `[Collection("E2E")] public class ...(PlaywrightFixture fixture)` + `Assertions.Expect` usage). The activity panel exposes `data-testid="activity-panel"` and `data-testid="activity-item"`; the refresh button is `data-testid="activity-refresh"`.

- [ ] **Step 10.1: Write the test**

Create `tests/TaskManager.E2E.Tests/ActivityFeedFlowTests.cs` (match the class header + usings to `BoardAndTaskFlowTests`):
```csharp
using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

[Collection("E2E")]
public class ActivityFeedFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Board_activity_feed_shows_a_members_actions()
    {
        var page = await fixture.NewPageAsync();
        await Flows.RegisterAsync(page, "activity");
        var boardName = $"Activity {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(page, boardName);
        await Flows.OpenBoardAsync(page, boardName);
        await Flows.CreateTaskAsync(page, "Feed me");

        // The activity feed (eventually-consistent: outbox → RabbitMQ → projection) shows the
        // create, then the move, after the realtime frame ticks a reload. Auto-retry absorbs lag.
        await Assertions.Expect(page.GetByTestId("activity-panel")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("activity-item").First).ToContainTextAsync("created", new() { Timeout = 20_000 });

        await Flows.DragTaskToColumnAsync(page, "Feed me", "Done");
        // The move broadcasts a realtime frame which ticks the panel reload; the new row appears.
        await Assertions.Expect(
            page.Locator("[data-testid='activity-item']", new() { HasText = "moved" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
    }
}
```
NOTE: the feed is eventually consistent (event must traverse the outbox → broker → Analytics projection) so generous timeouts are used. The panel reloads on the Feature-3 realtime frame (which fires immediately on the move) and via its initial load; the 20 s auto-retry covers the projection lag. If the suite prefers an explicit refresh nudge, click `page.GetByTestId("activity-refresh")` before the assertion — but the realtime tick should suffice. Adapt helper names to the real `Flows`.

- [ ] **Step 10.2: Build the E2E project (Docker NOT required to compile)**

```bash
dotnet build tests/TaskManager.E2E.Tests --configuration Release
```
Expected: 0 errors. Do NOT run locally (Docker down). The `e2e` CI check runs it.

- [ ] **Step 10.3: Commit**

```bash
git add tests/TaskManager.E2E.Tests/
git commit -m "test(e2e): board activity feed shows a member's actions"
```

---

### Task 11: Spec addendum + PR

**Files:**
- Modify: `smart-task-manager-spec.md` (append §13.4 after §13.3)

- [ ] **Step 11.1: Append the addendum**

After the §13.3 block, add:
```markdown

### 13.4 Per-board activity feed (Feature 4)
A live, per-board audit trail in a collapsible panel on board detail.

**Events** — two additive contract events, `TaskUpdatedEvent` and `TaskDeletedEvent`
(`TaskId, BoardId, Title, ActorId, OccurredAt`), published by Tasks' update/delete handlers
via the existing outbox and bound on the topic exchange (`task.updated` / `task.deleted`).
The five existing task events are **not reshaped** — they already carry an actor and a title,
so there is no breaking change to the Notifications consumers.

**Projection — reuses `task_events`** (no separate `board_activity` table). Two columns are
added: `ActorId` (the *performer*; distinct from `UserId`, which for `task.assigned` stays the
assignee so "assigned to me" user-activity is unchanged) and `TaskTitle`. The existing
inbox-dedup `EventProjector` populates them for every event type. No physical retention/trim:
`task_events` is already the unbounded log that powers the completion trend (which needs 30
days of `task.completed`), so the feed instead **query-limits** to the requested count.

**Endpoint** — `GET /api/analytics/boards/{boardId}/activity?count=50` (count clamped to
[1, 100], newest first). **Membership** is enforced by Analytics calling Tasks
`GET /api/boards/{boardId}` with the caller's `X-User-Id` (Tasks REST trusts that header; it
does not validate bearers — this refines the design's "forward the bearer"); 200 ⇒ member,
otherwise 403. `IBoardMembershipChecker` (Application port) + typed `HttpClient` adapter
(base address `TASKS_URL`) keep Analytics free of a membership table and of Identity coupling.

**SPA** — a collapsible panel on board detail loads the feed and reloads when a Feature-3
`TaskUpserted`/`TaskDeleted` frame arrives (a tick signal), plus a manual refresh button. Actor
display names resolve client-side via a memoizing cache over Identity `GET /api/users/{id}`.
The feed is eventually consistent (outbox → RabbitMQ → projection), so it may trail the hub
frame by a moment — the reload happens on the signal and the manual control covers the gap.
```

- [ ] **Step 11.2: Full local gate**

```bash
dotnet build SmartTaskManager.sln --no-restore
dotnet test tests/TaskManager.Analytics.Tests --filter "FullyQualifiedName~Unit"
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Unit"
cd frontend/task-manager-app && npx jest && npm run lint && cd ..\..
```
Expected: solution builds; Analytics + Tasks unit tests green; Jest + lint green. (Integration + E2E run on CI.)

- [ ] **Step 11.3: Commit, push, PR**

```bash
git add smart-task-manager-spec.md
git commit -m "docs(spec): v1.1 addendum — per-board activity feed (§13.4)"
git push -u origin feature/activity-feed
gh pr create --base develop --head feature/activity-feed \
  --title "feat: per-board activity feed (v1.1 Feature 4)" \
  --body "Live per-board audit trail. Two additive contract events (TaskUpdated/TaskDeleted) published by Tasks via the outbox; Analytics reuses task_events (adds ActorId + TaskTitle, no new table, no trim) and projects all event types; membership-enforced GET /api/analytics/boards/{id}/activity (Analytics → Tasks with caller X-User-Id); collapsible SPA panel reloaded by the Feature-3 hub signal with client-side actor-name resolution. Unit (projector, query, publish), Analytics endpoint integration, Jest (user-name cache), and an E2E feed flow. Spec addendum §13.4. Plan: docs/superpowers/plans/2026-06-11-activity-feed.md."
```

- [ ] **Step 11.4: Watch the 7 required checks; merge when green**

```bash
gh pr checks --watch
gh pr merge --merge
```
Expected: all 7 green (`test-dotnet (analytics)` runs the endpoint integration + projector tests; `e2e` runs the feed flow); merge completes Feature 4 — the last v1.1 feature.

---

## Self-review notes (already applied)

- **Spec coverage (design § Feature 4):** new `TaskUpdatedEvent`/`TaskDeletedEvent` ✔ (Task 1); Tasks publishes them ✔ (Task 2); event enrichment with ActorId + TaskTitle ✔ (Task 4, projector) — done by *projecting* existing fields rather than reshaping events (documented refinement: no Notifications breakage); Analytics projection of board activity ✔ (Tasks 3–4) — *reuses `task_events`* instead of a new table (documented refinement, retention dropped because the trend needs the full log); membership-enforced endpoint with caller forwarding ✔ (Tasks 6–7) — uses `X-User-Id` not a bearer (documented refinement matching Tasks' auth model); collapsible SPA panel refreshed off the hub signal + manual refresh ✔ (Task 9); client-side actor-name resolution ✔ (Task 8); E2E feed flow ✔ (Task 10); spec addendum ✔ (Task 11).
- **Documented refinements (3):** reuse `task_events` (no `board_activity` table, no trim); no reshaping of existing events (additive only → no consumer breakage); membership via `X-User-Id` (Tasks REST is header-trusted). All recorded in the §13.4 addendum and the plan header.
- **Type consistency:** `TaskUpdatedEvent`/`TaskDeletedEvent (TaskId, BoardId, Title, ActorId, OccurredAt)` defined in Task 1 and consumed in Tasks (Task 2), Analytics consumers (Task 3), and the projector (Task 4). `TaskEventRecord.ActorId`/`TaskTitle` (Task 4) flow into `BoardActivityItemDto(EventType, TaskId, TaskTitle, ActorId, OccurredAt)` (Task 5) → SPA `BoardActivityItemDto` (Task 8) → panel (Task 9). `IBoardMembershipChecker.IsMemberAsync` defined (Task 6) and used by the endpoint (Task 7). `GetBoardActivityAsync` signatures match between repository (Task 5), query service (Task 5), and the endpoint (Task 7).
- **No placeholders:** every code step carries real code; every run step carries its command + expected outcome. Where helper/fixture names depend on existing test infra (Analytics factory seeding/service-replacement, E2E `Flows`), the step says to confirm and adapt against the real names.
- **Risk — eventual consistency in the E2E:** the feed trails the hub frame (outbox → broker → projection), so the E2E uses generous (20 s) auto-retry and the panel reloads on the realtime tick; a manual refresh control exists as a fallback. The deterministic coverage (projector actor/title mapping incl. the assigner-vs-assignee subtlety, query clamping, membership 200/403) lives in the unit + integration tests.
```