# Real-Time Collaborative Boards Implementation Plan (v1.1 Feature 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Card changes made by one board member appear instantly for everyone viewing that board, plus live presence ("who's looking at this board now").

**Architecture:** A SignalR `BoardHub` hosted **in the Tasks service** (not Notifications) because joining a board group requires a *board-membership* check and only Tasks owns membership. After each successful task mutation the endpoint broadcasts the fresh `TaskDto` to the board's SignalR group — **best-effort, fire-after-commit, deliberately NOT through the RabbitMQ outbox** (a missed frame self-heals on reload). Presence is tracked in-memory behind an `IPresenceTracker` interface (connection-refcounted: a user with two tabs counts once). The Onion rule is preserved by an `IBoardBroadcaster` *port* in Application with the SignalR *adapter* in Presentation; endpoints (Presentation) call the port after a successful `Result`. The SPA opens one hub connection per board view, applies `TaskUpserted` only when `rowVersion` is strictly newer (drops stale frames and the echo of its own optimistic writes), and shows presence avatars.

**Tech Stack:** .NET 10, ASP.NET Core SignalR, `martinothamar/Mediator`, FluentResults, EF Core; xUnit + FluentAssertions + NSubstitute + Testcontainers; Angular 18 standalone + NgRx Signals + `@microsoft/signalr` (already a dependency, used by `notification.service.ts`); Jest; Playwright (two browser contexts).

**Branch:** `feature/realtime-boards` off `develop`. Conventional Commits. PR into `develop` must pass the 7 required checks (5× `test-dotnet`, `test-angular`, `e2e`).

**Working directory note:** `npx`/`npm` run from `frontend/task-manager-app/`; `dotnet`/`git` run from repo root `D:\work\Task Manager`.

**Environment note:** local Docker Desktop may be unavailable (it was crash-looping during Feature 2 — a wedged secrets-engine socket needing a host reboot). Where a step needs Docker (Testcontainers integration tests, the full-stack E2E), build the project to prove compilation and rely on the CI `test-dotnet (tasks)` / `e2e` checks, which run Docker on the runner. This is the same fallback Feature 2 used.

**Spec:** `docs/superpowers/specs/2026-06-11-v1.1-live-collaboration-design.md` § Feature 3. Key decisions baked into this plan:
- Hub in **Tasks**; `JoinBoard(boardId)` validates membership via the board repository; non-members get a `HubException`.
- Broadcast is **best-effort after commit**, not via the outbox; the durable RabbitMQ path (Analytics/Notifications) is untouched.
- Presence is **in-memory behind `IPresenceTracker`** (the seam for a future Redis impl; not built now).
- The SPA gates `TaskUpserted` on strictly-newer `rowVersion`; on reconnect it refetches the board once.
- `TaskDeleted` carries the `boardId` so the endpoint can address the group — the delete command is changed to return the board id.

---

## File structure

**Backend — `src/services/tasks/TaskManager.Tasks/`:**
- `Application/Interfaces/IBoardBroadcaster.cs` — port: `TaskUpsertedAsync(boardId, TaskDto, actorId)`, `TaskDeletedAsync(boardId, taskId, actorId)`.
- `Application/Interfaces/IPresenceTracker.cs` — port for presence; pure in-memory contract.
- `Infrastructure/Realtime/PresenceTracker.cs` — thread-safe connection-refcounted impl (singleton).
- `Presentation/Hubs/BoardHub.cs` — `[Authorize]` hub; `JoinBoard`/`LeaveBoard`/`OnDisconnectedAsync`.
- `Presentation/Hubs/SignalRBoardBroadcaster.cs` — adapter implementing `IBoardBroadcaster` via `IHubContext<BoardHub>`.
- `Presentation/Endpoints/TaskEndpoints.cs` — invoke the broadcaster after each successful mutation.
- `Application/Commands/TaskCommands.cs` + `Application/Handlers/TaskCommandHandlers.cs` — `DeleteTaskCommand` returns `Result<Guid>` (board id).
- `Program.cs` — add JWT bearer (query-string for `/hubs`), `AddSignalR`, DI for broadcaster + presence, `UseAuthentication/Authorization`, `MapHub<BoardHub>("/hubs/board")`.

**Gateway:** `appsettings.json` — add a `/hubs/board` route → `tasks-cluster` (more specific than the existing `/hubs/{**catch-all}` → notifications).

**Frontend — `frontend/task-manager-app/src/app/`:**
- `core/realtime/board-realtime.service.ts` — one hub connection; join/leave; handlers; reconnect→refetch.
- `core/realtime/index.ts` — barrel.
- `features/boards/boards.store.ts` — `applyRealtimeUpsert`/`applyRealtimeDelete`/presence signal.
- `shared/components/presence-avatars.component.ts` — initials chips + "+n".
- `features/boards/board-detail.component.ts` — join on enter, leave on destroy, render presence, wire realtime→store.

**Tests:** `tests/TaskManager.Tasks.Tests/Unit/PresenceTrackerTests.cs`, `.../Unit/BoardHubTests.cs`, `.../Integration/BoardHubAuthTests.cs`; Jest specs alongside the SPA files; `tests/TaskManager.E2E.Tests/` two-context sync test.

---

### Task 0: Branch setup

- [ ] **Step 0.1: Create the branch**

```bash
git checkout develop && git pull --ff-only origin develop
git checkout -b feature/realtime-boards
```

---

### Task 1: Presence tracker (Application port + in-memory impl)

The refcounting is the unit-testable core: a user with two connections counts once; the last connection leaving removes them.

**Files:**
- Create: `src/services/tasks/TaskManager.Tasks/Application/Interfaces/IPresenceTracker.cs`
- Create: `src/services/tasks/TaskManager.Tasks/Infrastructure/Realtime/PresenceTracker.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/PresenceTrackerTests.cs`

- [ ] **Step 1.1: Write the failing tests**

Create `tests/TaskManager.Tasks.Tests/Unit/PresenceTrackerTests.cs`:

```csharp
using TaskManager.Tasks.Infrastructure.Realtime;

namespace TaskManager.Tasks.Tests.Unit;

public class PresenceTrackerTests
{
    private readonly PresenceTracker _tracker = new();

    [Fact]
    public void Join_AddsViewer_AndReturnsCurrentViewers()
    {
        var board = Guid.NewGuid();
        var user = Guid.NewGuid();

        var viewers = _tracker.Join(board, user, "conn-1");

        viewers.Should().BeEquivalentTo(new[] { user });
        _tracker.ViewersOf(board).Should().BeEquivalentTo(new[] { user });
    }

    [Fact]
    public void Join_SameUserTwoConnections_CountsOnce()
    {
        var board = Guid.NewGuid();
        var user = Guid.NewGuid();

        _tracker.Join(board, user, "conn-1");
        var viewers = _tracker.Join(board, user, "conn-2");

        viewers.Should().BeEquivalentTo(new[] { user }, "two tabs are one viewer");
    }

    [Fact]
    public void Leave_OneOfTwoConnections_KeepsViewer_LastRemovesIt()
    {
        var board = Guid.NewGuid();
        var user = Guid.NewGuid();
        _tracker.Join(board, user, "conn-1");
        _tracker.Join(board, user, "conn-2");

        _tracker.Leave(board, user, "conn-1").Should().BeEquivalentTo(new[] { user });
        _tracker.Leave(board, user, "conn-2").Should().BeEmpty("last connection leaving removes the viewer");
        _tracker.ViewersOf(board).Should().BeEmpty();
    }

    [Fact]
    public void RemoveConnection_UnwindsEveryBoardThatConnectionWasIn()
    {
        var boardA = Guid.NewGuid();
        var boardB = Guid.NewGuid();
        var user = Guid.NewGuid();
        _tracker.Join(boardA, user, "conn-1");
        _tracker.Join(boardB, user, "conn-1");

        var affected = _tracker.RemoveConnection("conn-1");

        affected.Select(a => a.BoardId).Should().BeEquivalentTo(new[] { boardA, boardB });
        affected.Should().OnlyContain(a => a.Viewers.Count == 0);
        _tracker.ViewersOf(boardA).Should().BeEmpty();
        _tracker.ViewersOf(boardB).Should().BeEmpty();
    }

    [Fact]
    public void RemoveConnection_LeavesOtherUsersViewing()
    {
        var board = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _tracker.Join(board, alice, "conn-a");
        _tracker.Join(board, bob, "conn-b");

        var affected = _tracker.RemoveConnection("conn-a");

        affected.Should().ContainSingle();
        affected[0].BoardId.Should().Be(board);
        affected[0].Viewers.Should().BeEquivalentTo(new[] { bob });
    }
}
```

- [ ] **Step 1.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~PresenceTrackerTests"
```
Expected: FAIL to compile — `PresenceTracker`/`IPresenceTracker` don't exist.

- [ ] **Step 1.3: Create the interface**

Create `src/services/tasks/TaskManager.Tasks/Application/Interfaces/IPresenceTracker.cs`:

```csharp
namespace TaskManager.Tasks.Application.Interfaces;

/// <summary>
/// Tracks which users are currently viewing each board, refcounted by connection so a
/// user with multiple tabs counts once. In-memory today (single Tasks instance); the
/// interface is the seam for a Redis-backed impl if Tasks ever scales out (spec §F3).
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Registers a connection on a board. Returns the board's current distinct viewers.</summary>
    IReadOnlyList<Guid> Join(Guid boardId, Guid userId, string connectionId);

    /// <summary>Removes a connection from a board. Returns the board's remaining distinct viewers.</summary>
    IReadOnlyList<Guid> Leave(Guid boardId, Guid userId, string connectionId);

    /// <summary>Removes a connection from every board it was in (used on disconnect).</summary>
    IReadOnlyList<(Guid BoardId, IReadOnlyList<Guid> Viewers)> RemoveConnection(string connectionId);

    /// <summary>Current distinct viewers of a board.</summary>
    IReadOnlyList<Guid> ViewersOf(Guid boardId);
}
```

- [ ] **Step 1.4: Implement the in-memory tracker**

Create `src/services/tasks/TaskManager.Tasks/Infrastructure/Realtime/PresenceTracker.cs`:

```csharp
using TaskManager.Tasks.Application.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Realtime;

/// <summary>
/// Thread-safe in-memory presence store. Registered as a singleton. SignalR invokes
/// Join/Leave/RemoveConnection from connection threads, so every read/write holds the lock.
/// </summary>
public class PresenceTracker : IPresenceTracker
{
    private readonly object _gate = new();
    // board -> (user -> set of connection ids)
    private readonly Dictionary<Guid, Dictionary<Guid, HashSet<string>>> _boards = new();

    public IReadOnlyList<Guid> Join(Guid boardId, Guid userId, string connectionId)
    {
        lock (_gate)
        {
            var users = _boards.TryGetValue(boardId, out var u) ? u : _boards[boardId] = new();
            var conns = users.TryGetValue(userId, out var c) ? c : users[userId] = new();
            conns.Add(connectionId);
            return users.Keys.ToList();
        }
    }

    public IReadOnlyList<Guid> Leave(Guid boardId, Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_boards.TryGetValue(boardId, out var users)) return Array.Empty<Guid>();
            if (users.TryGetValue(userId, out var conns))
            {
                conns.Remove(connectionId);
                if (conns.Count == 0) users.Remove(userId);
            }
            if (users.Count == 0) { _boards.Remove(boardId); return Array.Empty<Guid>(); }
            return users.Keys.ToList();
        }
    }

    public IReadOnlyList<(Guid BoardId, IReadOnlyList<Guid> Viewers)> RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            var affected = new List<(Guid, IReadOnlyList<Guid>)>();
            foreach (var (boardId, users) in _boards.ToList())
            {
                var touched = false;
                foreach (var (userId, conns) in users.ToList())
                {
                    if (conns.Remove(connectionId))
                    {
                        touched = true;
                        if (conns.Count == 0) users.Remove(userId);
                    }
                }
                if (!touched) continue;
                if (users.Count == 0) _boards.Remove(boardId);
                affected.Add((boardId, users.Keys.ToList()));
            }
            return affected;
        }
    }

    public IReadOnlyList<Guid> ViewersOf(Guid boardId)
    {
        lock (_gate)
        {
            return _boards.TryGetValue(boardId, out var users) ? users.Keys.ToList() : Array.Empty<Guid>();
        }
    }
}
```

- [ ] **Step 1.5: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~PresenceTrackerTests"
```
Expected: PASS (5 tests).

- [ ] **Step 1.6: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Application/Interfaces/IPresenceTracker.cs src/services/tasks/TaskManager.Tasks/Infrastructure/Realtime/PresenceTracker.cs tests/TaskManager.Tasks.Tests/Unit/PresenceTrackerTests.cs
git commit -m "feat(tasks): in-memory connection-refcounted presence tracker"
```

---

### Task 2: Broadcaster port (Application interface)

**Files:**
- Create: `src/services/tasks/TaskManager.Tasks/Application/Interfaces/IBoardBroadcaster.cs`

- [ ] **Step 2.1: Create the interface**

Create `src/services/tasks/TaskManager.Tasks/Application/Interfaces/IBoardBroadcaster.cs`:

```csharp
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Interfaces;

/// <summary>
/// Best-effort, fire-after-commit real-time fan-out to a board's SignalR group (spec §F3).
/// Deliberately NOT the durable RabbitMQ outbox: a missed frame self-heals on reload, which
/// is the right consistency class for ephemeral UI sync. The SignalR adapter lives in
/// Presentation so the Onion/architecture rules stay satisfied.
/// </summary>
public interface IBoardBroadcaster
{
    Task TaskUpsertedAsync(Guid boardId, TaskDto task, Guid actorId, CancellationToken ct = default);
    Task TaskDeletedAsync(Guid boardId, Guid taskId, Guid actorId, CancellationToken ct = default);
}
```

- [ ] **Step 2.2: Build to verify it compiles**

```bash
dotnet build src/services/tasks/TaskManager.Tasks --no-restore
```
Expected: 0 errors.

- [ ] **Step 2.3: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Application/Interfaces/IBoardBroadcaster.cs
git commit -m "feat(tasks): IBoardBroadcaster application port"
```

---

### Task 3: `DeleteTaskCommand` returns the board id

`TaskDeleted` must address the board group, but the delete endpoint only has the task id. Make the delete handler return the deleted task's `BoardId`.

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Commands/TaskCommands.cs`
- Modify: `src/services/tasks/TaskManager.Tasks/Application/Handlers/TaskCommandHandlers.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/TaskCommandHandlerTests.cs`

- [ ] **Step 3.1: Update the failing test**

In `tests/TaskManager.Tasks.Tests/Unit/TaskCommandHandlerTests.cs`, find the existing delete-handler test (search for `DeleteTaskCommandHandler`). Add an assertion that the success result carries the board id. Append this new test next to it:

```csharp
    [Fact]
    public async Task DeleteTaskCommandHandler_OnSuccess_ReturnsBoardId()
    {
        var boardId = Guid.NewGuid();
        var task = Fake.Task(boardId);
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(boardId, editor, Arg.Any<CancellationToken>()).Returns(BoardRole.Editor);
        var handler = new DeleteTaskCommandHandler(_tasks, _boards, _uow, _publisher);

        var result = await handler.Handle(new DeleteTaskCommand(task.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(boardId);
    }
```

NOTE: confirm the field names used by the existing `TaskCommandHandlerTests` fixture (`_tasks`, `_boards`, `_uow`, `_publisher`) — match them. If `DeleteTaskCommandHandler`'s constructor differs (e.g. it takes an `IEventPublisher`), pass exactly what the existing delete tests pass.

- [ ] **Step 3.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~DeleteTaskCommandHandler_OnSuccess_ReturnsBoardId"
```
Expected: FAIL to compile — `DeleteTaskCommand` is `IRequest<Result>`, has no `Result<Guid>` value.

- [ ] **Step 3.3: Change the command + handler**

In `Application/Commands/TaskCommands.cs`, change the delete command's result type:

```csharp
public record DeleteTaskCommand(Guid TaskId, Guid UserId) : IRequest<Result<Guid>>;
```

In `Application/Handlers/TaskCommandHandlers.cs`, find `DeleteTaskCommandHandler`. Change its declared interface to `IRequestHandler<DeleteTaskCommand, Result<Guid>>` and return the board id on success. The existing handler body looks up the task, checks the role, removes it, publishes a `TaskDeletedEvent` (if present) and saves. Keep all of that; only change the return type and the final `Result.Ok()` to `Result.Ok(task.BoardId)`. Concretely the success path becomes:

```csharp
        // ... existing not-found / authorization / publish / SaveChanges logic unchanged ...
        return Result.Ok(task.BoardId);
```

If the handler currently returns `Result.Ok()` in more than one place, every success return becomes `Result.Ok(task.BoardId)` and every failure return stays `Result.Fail("...")` (a `Result.Fail` is assignable to `Result<Guid>` via FluentResults' implicit conversion — if the compiler complains, change `return Result.Fail("...");` to `return Result.Fail<Guid>("...");`).

- [ ] **Step 3.4: Fix the delete endpoint call site**

In `Presentation/Endpoints/TaskEndpoints.cs`, the delete route currently does:

```csharp
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteTaskCommand(id, userId), ct)).ToHttpResult();
        });
```

`ToHttpResult()` on a `Result<Guid>` must still map success → 204 No Content (not 200 with the guid body). Check the shared `ToHttpResult` extension in `Presentation/Extensions/ResultExtensions.cs`: if `Result<T>` success maps to `Results.Ok(value)`, that would change the delete response from 204 to 200. To preserve the 204 contract, map explicitly here:

```csharp
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new DeleteTaskCommand(id, userId), ct);
            return result.IsSuccess ? Results.NoContent() : result.ToResult().ToHttpResult();
        });
```

`result.ToResult()` converts `Result<Guid>` → non-generic `Result` for the failure mapping (FluentResults provides `ToResult()`). If `ToResult()` is unavailable, use `Results.NoContent()` on success and replicate the failure status mapping by inspecting `result.Errors` the same way `ToHttpResult` does. The broadcast wiring (Task 6) will use `result.Value` here.

- [ ] **Step 3.5: Run to verify pass + build the solution (the result-type change can ripple)**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~DeleteTaskCommandHandler"
dotnet build SmartTaskManager.sln --no-restore
```
Expected: delete handler tests pass; solution builds (0 errors). If the existing integration test `DeleteTask_AsViewer403_AsEditor204` exists, confirm it still expects 204 — the endpoint change above preserves that.

- [ ] **Step 3.6: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Application/Commands/TaskCommands.cs src/services/tasks/TaskManager.Tasks/Application/Handlers/TaskCommandHandlers.cs src/services/tasks/TaskManager.Tasks/Presentation/Endpoints/TaskEndpoints.cs tests/TaskManager.Tasks.Tests/Unit/TaskCommandHandlerTests.cs
git commit -m "feat(tasks): DeleteTaskCommand returns board id for realtime fan-out"
```

---

### Task 4: BoardHub + SignalR broadcaster adapter

**Files:**
- Create: `src/services/tasks/TaskManager.Tasks/Presentation/Hubs/BoardHub.cs`
- Create: `src/services/tasks/TaskManager.Tasks/Presentation/Hubs/SignalRBoardBroadcaster.cs`
- Test: `tests/TaskManager.Tasks.Tests/Unit/BoardHubTests.cs`

- [ ] **Step 4.1: Write the failing hub tests**

The hub's authorization logic is unit-testable by substituting `IBoardRepository`, the SignalR `HubCallerContext`, `IGroupManager`, and `IHubCallerClients`. Create `tests/TaskManager.Tasks.Tests/Unit/BoardHubTests.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Presentation.Hubs;

namespace TaskManager.Tasks.Tests.Unit;

public class BoardHubTests
{
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IPresenceTracker _presence = Substitute.For<IPresenceTracker>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();

    private BoardHub CreateHub(Guid userId, string connectionId = "conn-1")
    {
        _context.ConnectionId.Returns(connectionId);
        _context.UserIdentifier.Returns(userId.ToString());
        return new BoardHub(_boards, _presence) { Clients = _clients, Groups = _groups, Context = _context };
    }

    [Fact]
    public async Task JoinBoard_AsMember_AddsToGroupAndRegistersPresence()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(BoardRole.Viewer);
        _presence.Join(boardId, userId, "conn-1").Returns(new[] { userId });
        var group = Substitute.For<IClientProxy>();
        _clients.Group($"board:{boardId}").Returns(group);
        var hub = CreateHub(userId);

        await hub.JoinBoard(boardId);

        await _groups.Received(1).AddToGroupAsync("conn-1", $"board:{boardId}", Arg.Any<CancellationToken>());
        _presence.Received(1).Join(boardId, userId, "conn-1");
        await group.Received(1).SendCoreAsync("PresenceChanged",
            Arg.Is<object?[]>(a => a.Length == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinBoard_AsNonMember_ThrowsHubException_AndDoesNotJoin()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var hub = CreateHub(userId);

        var act = async () => await hub.JoinBoard(boardId);

        await act.Should().ThrowAsync<HubException>();
        await _groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _presence.DidNotReceive().Join(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LeaveBoard_RemovesFromGroupAndPresence()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _presence.Leave(boardId, userId, "conn-1").Returns(Array.Empty<Guid>());
        var group = Substitute.For<IClientProxy>();
        _clients.Group($"board:{boardId}").Returns(group);
        var hub = CreateHub(userId);

        await hub.LeaveBoard(boardId);

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", $"board:{boardId}", Arg.Any<CancellationToken>());
        _presence.Received(1).Leave(boardId, userId, "conn-1");
    }
}
```

- [ ] **Step 4.2: Run to verify failure**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~BoardHubTests"
```
Expected: FAIL to compile — `BoardHub`/`SignalRBoardBroadcaster` don't exist.

- [ ] **Step 4.3: Create the hub**

Create `src/services/tasks/TaskManager.Tasks/Presentation/Hubs/BoardHub.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Presentation.Hubs;

/// <summary>
/// Board-scoped real-time sync (spec §F3). Hosted in Tasks because joining a board group
/// requires a membership check only Tasks can do. JWT arrives via the query string (wired in
/// Program.cs OnMessageReceived), so plain [Authorize] works. Group name = "board:{boardId}".
/// </summary>
[Authorize]
public class BoardHub(IBoardRepository boards, IPresenceTracker presence) : Hub
{
    public static string Group(Guid boardId) => $"board:{boardId}";

    public async Task JoinBoard(Guid boardId)
    {
        if (!TryGetUserId(out var userId)) throw new HubException("unauthorized");
        // Only board members may join the group — the whole reason the hub lives in Tasks.
        if (await boards.GetMemberRoleAsync(boardId, userId, Context.ConnectionAborted) is null)
            throw new HubException("forbidden: not a board member");

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(boardId), Context.ConnectionAborted);
        var viewers = presence.Join(boardId, userId, Context.ConnectionId);
        await Clients.Group(Group(boardId)).SendAsync("PresenceChanged", viewers, Context.ConnectionAborted);
    }

    public async Task LeaveBoard(Guid boardId)
    {
        if (!TryGetUserId(out var userId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(boardId), Context.ConnectionAborted);
        var viewers = presence.Leave(boardId, userId, Context.ConnectionId);
        await Clients.Group(Group(boardId)).SendAsync("PresenceChanged", viewers, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (boardId, viewers) in presence.RemoveConnection(Context.ConnectionId))
            await Clients.Group(Group(boardId)).SendAsync("PresenceChanged", viewers);
        await base.OnDisconnectedAsync(exception);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(Context.UserIdentifier, out userId);
}
```

- [ ] **Step 4.4: Create the broadcaster adapter**

Create `src/services/tasks/TaskManager.Tasks/Presentation/Hubs/SignalRBoardBroadcaster.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Interfaces;

namespace TaskManager.Tasks.Presentation.Hubs;

/// <summary>SignalR adapter for the IBoardBroadcaster port. Fan-out only; no durability.</summary>
public class SignalRBoardBroadcaster(IHubContext<BoardHub> hub) : IBoardBroadcaster
{
    public Task TaskUpsertedAsync(Guid boardId, TaskDto task, Guid actorId, CancellationToken ct = default)
        => hub.Clients.Group(BoardHub.Group(boardId)).SendAsync("TaskUpserted", task, actorId, ct);

    public Task TaskDeletedAsync(Guid boardId, Guid taskId, Guid actorId, CancellationToken ct = default)
        => hub.Clients.Group(BoardHub.Group(boardId)).SendAsync("TaskDeleted", taskId, actorId, ct);
}
```

- [ ] **Step 4.5: Run to verify pass**

```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~BoardHubTests"
```
Expected: PASS (3 tests). If the `SendAsync("PresenceChanged", viewers, …)` extension resolves to `SendCoreAsync` with a different arg-array shape than the test asserts, adjust the test's `Arg.Is<object?[]>` predicate to match (the key assertion is that the group proxy was sent a `PresenceChanged` message).

- [ ] **Step 4.6: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Presentation/Hubs/ tests/TaskManager.Tasks.Tests/Unit/BoardHubTests.cs
git commit -m "feat(tasks): BoardHub with membership-gated join + SignalR broadcaster"
```

---

### Task 5: Wire SignalR + auth + hub into Program.cs

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Program.cs`

- [ ] **Step 5.1: Add auth, SignalR, DI, and the hub mapping**

Tasks currently has NO authentication (it trusts the gateway's `X-User-Id` for REST). The hub needs real JWT validation. Add the following to `Program.cs`, mirroring the Notifications service exactly.

1. Add usings at the top:
```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Infrastructure.Realtime;
using TaskManager.Tasks.Presentation.Hubs;
```

2. After the existing service registrations (after `builder.Services.AddSingleton<TasksMapper>();`), add SignalR + realtime DI:
```csharp
// Real-time board sync (spec §F3): SignalR hub + best-effort broadcaster + in-memory presence.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
builder.Services.AddSingleton<IBoardBroadcaster, SignalRBoardBroadcaster>();
```

3. After the SignalR block, add JWT auth (the WS handshake can't carry an Authorization header, so the token rides `?access_token=` on `/hubs/*` paths):
```csharp
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:SecretKey"] ?? string.Empty;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TaskManager.Identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TaskManager",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();
```

4. In the middleware pipeline (after `app.UseSerilogRequestLogging();` and before the endpoint maps), add:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

5. After `app.MapTaskEndpoints();`, map the hub:
```csharp
app.MapHub<BoardHub>("/hubs/board");
```

IMPORTANT — do NOT add `[Authorize]` to the REST endpoints or `RequireAuthorization()` on the route groups: the REST endpoints must keep trusting the gateway's `X-User-Id` header (they receive no bearer token from the gateway). Only the hub uses `[Authorize]`. Adding global auth would break every existing REST endpoint and integration test.

- [ ] **Step 5.2: Build + run the existing Tasks unit tests to confirm nothing regressed**

```bash
dotnet build src/services/tasks/TaskManager.Tasks --no-restore
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Unit"
```
Expected: 0 build errors; all unit tests pass.

- [ ] **Step 5.3: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Program.cs
git commit -m "feat(tasks): host BoardHub at /hubs/board with JWT query-string auth"
```

---

### Task 6: Broadcast after each successful mutation

The endpoints already produce a `Result<TaskDto>` (or `Result<Guid>` for delete). After a success, fire the broadcaster — best-effort, after the handler's `SaveChangesAsync` has committed.

**Files:**
- Modify: `src/services/tasks/TaskManager.Tasks/Presentation/Endpoints/TaskEndpoints.cs`

- [ ] **Step 6.1: Inject the broadcaster and fan out on success**

The mutating routes are: create (`POST /`), update (`PUT /{id}`), move (`POST /{id}/move`), assign (`POST /{id}/assign`), delete (`DELETE /{id}`), comments (add/edit/delete), labels (attach/detach), checklist (add/update/delete). All the `TaskDto`-returning ones flow through the private helpers `TaskResult` / `TaskResultWithConflictBody`. Centralize the upsert broadcast in those helpers so every `TaskDto` success fans out once; handle delete separately.

In `TaskEndpoints.cs`:

1. The minimal-API handler lambdas receive services as parameters. Add `IBoardBroadcaster broadcaster` to each mutating lambda's parameter list and pass it into the helpers. To avoid threading it through every lambda, instead resolve it inside the helpers via an added parameter. Change the two helper signatures and all their call sites:

Change `TaskResult` to broadcast on success:
```csharp
    /// <summary>Success → 200 with ETag; also fan out the fresh task to the board group (spec §F3).</summary>
    private static IResult TaskResult(HttpContext http, Result<TaskDto> result, IBoardBroadcaster broadcaster, Guid actorId)
    {
        if (result.IsFailed) return result.ToHttpResult();
        http.SetETag(result.Value.RowVersion);
        // Best-effort, fire-after-commit. Do not await failures into the response — a missed
        // frame self-heals on reload. Fire-and-forget with an unobserved-exception guard.
        _ = broadcaster.TaskUpsertedAsync(result.Value.BoardId, result.Value, actorId)
            .ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        return Results.Ok(result.Value);
    }
```

And `TaskResultWithConflictBody` similarly forwards the broadcaster + actor to `TaskResult`:
```csharp
    private static async Task<IResult> TaskResultWithConflictBody(
        HttpContext http, IMediator mediator, Guid taskId, Guid userId, Result<TaskDto> result,
        IBoardBroadcaster broadcaster, CancellationToken ct)
    {
        if (result.IsSuccess) return TaskResult(http, result, broadcaster, userId);

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
```

2. Update every mutating lambda to take `IBoardBroadcaster broadcaster` and pass it. For example the create route:
```csharp
        group.MapPost("/", async (CreateTaskRequest req, HttpContext http, IMediator mediator, IBoardBroadcaster broadcaster, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new CreateTaskCommand(req.BoardId, req.Title, req.Description, req.Priority, req.DueDate, userId), ct);
            return TaskResult(http, result, broadcaster, userId);
        });
```
Apply the same parameter addition + argument pass to: `GET /{id}` (read-only — does NOT broadcast; leave it calling a non-broadcasting path, see note below), `PUT /{id}`, `POST /{id}/move`, `POST /{id}/assign`, the comment add/edit/delete routes, the label attach/detach routes, and the checklist add/update/delete routes. For routes that call `TaskResultWithConflictBody`, add `broadcaster` to the lambda and pass it through.

NOTE on `GET /{id:guid}`: it currently calls `TaskResult(http, result)` too, but a read must not broadcast. Give the read path its own tiny helper so reads don't fan out:
```csharp
    /// <summary>Read result: 200 + ETag, no broadcast.</summary>
    private static IResult ReadTaskResult(HttpContext http, Result<TaskDto> result)
    {
        if (result.IsFailed) return result.ToHttpResult();
        http.SetETag(result.Value.RowVersion);
        return Results.Ok(result.Value);
    }
```
and change the `GET /{id:guid}` route to call `ReadTaskResult(http, result)`.

3. Delete fans out a `TaskDeleted` using the board id from `Result<Guid>`:
```csharp
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, IBoardBroadcaster broadcaster, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new DeleteTaskCommand(id, userId), ct);
            if (result.IsFailed) return result.ToResult().ToHttpResult();
            _ = broadcaster.TaskDeletedAsync(result.Value, id, userId)
                .ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            return Results.NoContent();
        });
```

- [ ] **Step 6.2: Build + run Tasks unit tests**

```bash
dotnet build SmartTaskManager.sln --no-restore
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Unit"
```
Expected: 0 build errors; unit tests pass (the endpoint changes are not covered by unit tests — they're integration-tested next and via the existing endpoint integration tests, which must still pass for status codes/ETags).

- [ ] **Step 6.3: Commit**

```bash
git add src/services/tasks/TaskManager.Tasks/Presentation/Endpoints/TaskEndpoints.cs
git commit -m "feat(tasks): broadcast TaskUpserted/TaskDeleted to the board group after commit"
```

---

### Task 7: Integration test — hub membership authorization

A real-server SignalR test against the Testcontainers Postgres confirms a member can join and a non-member is rejected. (Docker required → runs on CI if unavailable locally; build to verify compilation.)

**Files:**
- Create: `tests/TaskManager.Tasks.Tests/Integration/BoardHubAuthTests.cs`

- [ ] **Step 7.1: Write the integration test**

This uses the existing `TasksWebAppFactory` (boots the service against real Postgres + RabbitMQ) and a real `HubConnection` from `Microsoft.AspNetCore.SignalR.Client` pointed at the in-memory test server. The hub authorizes via JWT; the test mints a token the same way the service validates it. Create `tests/TaskManager.Tasks.Tests/Integration/BoardHubAuthTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class BoardHubAuthTests(TasksWebAppFactory factory)
{
    private string MintToken(Guid userId)
    {
        // Mirror the service's validation params (issuer/audience/secret from config).
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var secret = config["JWT_SECRET"] ?? config["Jwt:SecretKey"] ?? "test-secret-test-secret-test-secret-32+";
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "TaskManager.Identity",
            audience: config["Jwt:Audience"] ?? "TaskManager",
            claims: new[] { new Claim("sub", userId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HubConnection HubFor(Guid userId)
    {
        var server = factory.Server; // in-memory TestServer
        return new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}hubs/board", o =>
            {
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(MintToken(userId));
            })
            .Build();
    }

    [Fact]
    public async Task JoinBoard_AsMember_Succeeds_AsNonMember_Throws()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        var memberConn = HubFor(owner);
        await memberConn.StartAsync();
        // Member: JoinBoard completes without throwing.
        await memberConn.InvokeAsync("JoinBoard", boardId);
        await memberConn.StopAsync();

        var strangerConn = HubFor(Guid.NewGuid());
        await strangerConn.StartAsync();
        var act = async () => await strangerConn.InvokeAsync("JoinBoard", boardId);
        await act.Should().ThrowAsync<HubException>().WithMessage("*not a board member*");
        await strangerConn.StopAsync();
    }
}
```

NOTE: this requires the `Microsoft.AspNetCore.SignalR.Client` package in the test project. Check `tests/TaskManager.Tasks.Tests/TaskManager.Tasks.Tests.csproj`; if absent, add it:
```bash
dotnet add tests/TaskManager.Tasks.Tests package Microsoft.AspNetCore.SignalR.Client
```
Also confirm `TasksWebAppFactory` exposes `Server` (the `WebApplicationFactory<T>.Server` property is available by default) and that JWT validation in the test environment uses a secret the test can read. If `appsettings.Development.json`/test config sets `Jwt:SecretKey` or `JWT_SECRET`, the `MintToken` fallback must match it — read the actual configured value via `factory.Services` as shown.

- [ ] **Step 7.2: Build the test project (Docker not required to compile)**

```bash
dotnet build tests/TaskManager.Tasks.Tests --no-restore
```
Expected: 0 errors. If Docker is available locally, also run:
```bash
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~BoardHubAuthTests"
```
Expected: PASS. Otherwise rely on the CI `test-dotnet (tasks)` check.

- [ ] **Step 7.3: Commit**

```bash
git add tests/TaskManager.Tasks.Tests/
git commit -m "test(tasks): BoardHub membership authorization integration test"
```

---

### Task 8: Gateway route for the board hub

**Files:**
- Modify: `src/gateway/TaskManager.Gateway/appsettings.json`

- [ ] **Step 8.1: Add the `/hubs/board` route to the tasks cluster**

The existing `hubs` route sends `/hubs/{**catch-all}` to `notifications-cluster`. Add a more-specific route for the board hub to `tasks-cluster`, with a lower `Order` so it wins. In the `ReverseProxy.Routes` object, add:

```json
      "hubs-board": {
        "ClusterId": "tasks-cluster",
        "Order": 0,
        "Match": { "Path": "/hubs/board/{**catch-all}" }
      },
```

Leave the existing `"hubs"` route (notifications) unchanged. The SignalR client negotiates at `/hubs/board/negotiate` and upgrades on `/hubs/board`, both matched by `/hubs/board/{**catch-all}`. YARP picks the most specific path, and `Order: 0` guarantees the board route is evaluated before the `/hubs/{**catch-all}` fallback.

- [ ] **Step 8.2: Build the gateway**

```bash
dotnet build src/gateway/TaskManager.Gateway --no-restore
```
Expected: 0 errors (JSON config isn't compiled, but this confirms nothing else broke).

- [ ] **Step 8.3: Commit**

```bash
git add src/gateway/TaskManager.Gateway/appsettings.json
git commit -m "feat(gateway): route /hubs/board to the tasks cluster"
```

---

### Task 9: SPA realtime service

One hub connection managing join/leave and dispatching frames into the store. Mirrors `core/notifications/notification.service.ts`.

**Files:**
- Create: `frontend/task-manager-app/src/app/core/realtime/board-realtime.service.ts`
- Create: `frontend/task-manager-app/src/app/core/realtime/index.ts`
- Test: `frontend/task-manager-app/src/app/core/realtime/board-realtime.service.spec.ts`

- [ ] **Step 9.1: Write the failing test**

The service is mostly SignalR glue; the unit-testable part is that it registers handlers that forward to the injected callbacks and that connect/disconnect manage a single connection. Create `board-realtime.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { BoardRealtimeService } from './board-realtime.service';

describe('BoardRealtimeService', () => {
  let service: BoardRealtimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(BoardRealtimeService);
  });

  it('is created and starts disconnected', () => {
    expect(service).toBeTruthy();
    expect(service.isConnected()).toBe(false);
  });

  it('exposes a viewers signal defaulting to empty', () => {
    expect(service.viewers()).toEqual([]);
  });
});
```

(The deep SignalR behavior — strictly-newer gating, reconnect refetch — is verified by the store unit tests in Task 10 and the two-browser E2E in Task 12. This service is thin transport; over-mocking `HubConnection` adds no real coverage, consistent with how `notification.service.ts` is left untested.)

- [ ] **Step 9.2: Run to verify failure**

```bash
npx jest src/app/core/realtime/board-realtime.service
```
Expected: FAIL — `Cannot find module './board-realtime.service'`.

- [ ] **Step 9.3: Implement the service**

Create `board-realtime.service.ts`:

```typescript
import { inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { apiUrl } from '../http/api-base';
import { AuthStore } from '../auth';
import { TaskDto } from '../models';

export interface BoardRealtimeHandlers {
  onUpsert: (task: TaskDto, actorId: string) => void;
  onDelete: (taskId: string, actorId: string) => void;
  onReconnected: () => void;
}

/**
 * One SignalR connection to the board hub (spec §F3). The JWT travels via accessTokenFactory
 * (?access_token=) since browsers can't set headers on the WS handshake. Presence viewer ids
 * land in the `viewers` signal; task frames go to the injected handlers.
 */
@Injectable({ providedIn: 'root' })
export class BoardRealtimeService {
  private readonly auth = inject(AuthStore);
  private connection: HubConnection | null = null;
  private joinedBoardId: string | null = null;

  readonly viewers = signal<string[]>([]);

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  async join(boardId: string, handlers: BoardRealtimeHandlers): Promise<void> {
    await this.leave();

    const connection = new HubConnectionBuilder()
      .withUrl(apiUrl('/hubs/board'), { accessTokenFactory: () => this.auth.accessToken() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('TaskUpserted', (task: TaskDto, actorId: string) => handlers.onUpsert(task, actorId));
    connection.on('TaskDeleted', (taskId: string, actorId: string) => handlers.onDelete(taskId, actorId));
    connection.on('PresenceChanged', (viewerIds: string[]) => this.viewers.set(viewerIds));
    // On reconnect, frames may have been missed while down — rejoin and let the caller refetch.
    connection.onreconnected(async () => {
      await connection.invoke('JoinBoard', boardId);
      handlers.onReconnected();
    });

    this.connection = connection;
    await connection.start();
    await connection.invoke('JoinBoard', boardId);
    this.joinedBoardId = boardId;
  }

  async leave(): Promise<void> {
    const connection = this.connection;
    const boardId = this.joinedBoardId;
    this.connection = null;
    this.joinedBoardId = null;
    this.viewers.set([]);
    if (connection && boardId && connection.state === HubConnectionState.Connected) {
      try {
        await connection.invoke('LeaveBoard', boardId);
      } catch {
        // best-effort; stopping the connection unwinds presence server-side anyway
      }
    }
    await connection?.stop();
  }
}
```

Create `core/realtime/index.ts`:
```typescript
export * from './board-realtime.service';
```

- [ ] **Step 9.4: Run to verify pass + lint**

```bash
npx jest src/app/core/realtime/board-realtime.service
npm run lint
```
Expected: PASS; lint clean.

- [ ] **Step 9.5: Commit**

```bash
git add frontend/task-manager-app/src/app/core/realtime/
git commit -m "feat(frontend): board realtime SignalR service with presence signal"
```

---

### Task 10: Store realtime apply logic (strictly-newer gating)

The store decides whether an incoming frame is applied: `TaskUpserted` wins only if its `rowVersion` is strictly greater than the local copy (drops stale frames and the echo of the user's own optimistic writes); `TaskDeleted` removes the card.

**Files:**
- Modify: `frontend/task-manager-app/src/app/features/boards/boards.store.ts`
- Test: `frontend/task-manager-app/src/app/features/boards/boards.store.spec.ts`

- [ ] **Step 10.1: Write the failing tests**

Append to `boards.store.spec.ts` (it exists from Feature 1; merge imports). The store needs a board loaded first; use the existing test setup idiom (HttpClient testing). Add:

```typescript
import { makeBoardDetail, makeTask } from '../../testing/factories';

describe('BoardsStore realtime', () => {
  let store: InstanceType<typeof BoardsStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatSnackBarModule],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    });
    store = TestBed.inject(BoardsStore);
  });

  function seedBoardWith(task: TaskDto): void {
    const board = makeBoardDetail({ id: task.boardId, tasksByStatus: { Todo: [task] } });
    store.setCurrentBoardForTest(board);
  }

  it('applyRealtimeUpsert replaces a task when rowVersion is strictly newer', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1, title: 'old' });
    seedBoardWith(task);

    store.applyRealtimeUpsert({ ...task, rowVersion: 2, title: 'new' });

    const todo = store.currentBoard()!.tasksByStatus.Todo!;
    expect(todo[0].title).toBe('new');
  });

  it('applyRealtimeUpsert ignores a stale or equal rowVersion (drops own echo)', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 5, title: 'current' });
    seedBoardWith(task);

    store.applyRealtimeUpsert({ ...task, rowVersion: 5, title: 'echo' });
    store.applyRealtimeUpsert({ ...task, rowVersion: 3, title: 'stale' });

    expect(store.currentBoard()!.tasksByStatus.Todo![0].title).toBe('current');
  });

  it('applyRealtimeUpsert moves a task to a new column when status changed', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(task);

    store.applyRealtimeUpsert({ ...task, rowVersion: 2, status: 'Done' });

    const board = store.currentBoard()!;
    expect(board.tasksByStatus.Todo ?? []).toHaveLength(0);
    expect(board.tasksByStatus.Done!).toHaveLength(1);
  });

  it('applyRealtimeUpsert inserts a brand-new task not seen before', () => {
    const existing = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(existing);
    const fresh = makeTask({ boardId: existing.boardId, status: 'InProgress', rowVersion: 1 });

    store.applyRealtimeUpsert(fresh);

    expect(store.currentBoard()!.tasksByStatus.InProgress!).toHaveLength(1);
  });

  it('applyRealtimeDelete removes the task from its column', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(task);

    store.applyRealtimeDelete(task.id);

    expect(store.currentBoard()!.tasksByStatus.Todo ?? []).toHaveLength(0);
  });

  it('ignores realtime frames for a different board', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(task);
    const otherBoardTask = makeTask({ rowVersion: 9, status: 'Todo' }); // different boardId

    store.applyRealtimeUpsert(otherBoardTask);

    expect(store.currentBoard()!.tasksByStatus.Todo!).toHaveLength(1);
  });
});
```

- [ ] **Step 10.2: Run to verify failure**

```bash
npx jest src/app/features/boards/boards.store
```
Expected: FAIL — `store.applyRealtimeUpsert`/`applyRealtimeDelete`/`setCurrentBoardForTest` don't exist.

- [ ] **Step 10.3: Implement the store methods**

In `boards.store.ts`:

1. Add a helper near `applyMove`/`replaceTask` that finds a task's current status across columns and removes it, returning the cleaned board (used by upsert when the status changed) and a pure upsert:

```typescript
/** Find the column a task currently sits in, or null if it isn't on the board. */
const findTask = (board: BoardDetailDto, taskId: string): { status: TaskStatus; task: TaskDto } | null => {
  for (const [status, tasks] of Object.entries(board.tasksByStatus)) {
    const task = (tasks ?? []).find((t) => t.id === taskId);
    if (task) return { status: status as TaskStatus, task };
  }
  return null;
};

/** Remove a task from whichever column holds it. */
const removeTask = (board: BoardDetailDto, taskId: string): BoardDetailDto => ({
  ...board,
  tasksByStatus: Object.fromEntries(
    Object.entries(board.tasksByStatus).map(([status, tasks]) => [
      status,
      (tasks ?? []).filter((t) => t.id !== taskId),
    ]),
  ) as BoardDetailDto['tasksByStatus'],
});

/** Insert/replace a task in its (possibly new) status column, ordered by position. */
const upsertTask = (board: BoardDetailDto, task: TaskDto): BoardDetailDto => {
  const cleaned = removeTask(board, task.id);
  const column = [...(cleaned.tasksByStatus[task.status] ?? []), task].sort((a, b) => a.position - b.position);
  return { ...cleaned, tasksByStatus: { ...cleaned.tasksByStatus, [task.status]: column } };
};
```

2. Add three methods to the object returned by `withMethods` (after `clearFilter`):

```typescript
      /** Test-only seam to set the current board without an HTTP round-trip. */
      setCurrentBoardForTest(board: BoardDetailDto): void {
        patchState(store, { currentBoard: board });
      },

      /**
       * Apply a realtime TaskUpserted. Ignored unless it targets the current board and its
       * rowVersion is strictly newer than the local copy — this drops stale out-of-order frames
       * and the echo of the user's own optimistic mutation (spec §F3).
       */
      applyRealtimeUpsert(task: TaskDto): void {
        const board = store.currentBoard();
        if (board === null || board.id !== task.boardId) return;
        const existing = findTask(board, task.id);
        if (existing !== null && task.rowVersion <= existing.task.rowVersion) return;
        patchState(store, { currentBoard: upsertTask(board, task) });
      },

      /** Apply a realtime TaskDeleted: remove the card if it targets the current board. */
      applyRealtimeDelete(taskId: string): void {
        const board = store.currentBoard();
        if (board === null) return;
        if (findTask(board, taskId) === null) return;
        patchState(store, { currentBoard: removeTask(board, taskId) });
      },
```

NOTE: `setCurrentBoardForTest` is a deliberate test seam (the store has no other way to set `currentBoard` synchronously without HTTP). If the project prefers not to ship a test-only method, the alternative is to drive `loadBoard` with `provideHttpClientTesting` and flush a board response in each test — but the explicit seam keeps these unit tests focused on the apply logic. Keep it; it is harmless (it only sets state) and named clearly.

- [ ] **Step 10.4: Run to verify pass + full suite**

```bash
npx jest src/app/features/boards/boards.store
npx jest
```
Expected: the new tests pass; full suite green.

- [ ] **Step 10.5: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/boards.store.ts frontend/task-manager-app/src/app/features/boards/boards.store.spec.ts
git commit -m "feat(frontend): realtime upsert/delete with strictly-newer rowVersion gating"
```

---

### Task 11: Presence avatars + board-detail wiring

Render presence avatars in the board header and wire the realtime service: join on board enter, leave on destroy, dispatch frames to the store, refetch on reconnect.

**Files:**
- Create: `frontend/task-manager-app/src/app/shared/components/presence-avatars.component.ts`
- Modify: `frontend/task-manager-app/src/app/shared/components/index.ts` (export it)
- Modify: `frontend/task-manager-app/src/app/features/boards/board-detail.component.ts`

- [ ] **Step 11.1: Create the presence avatars component**

Dumb presentational component: takes viewer ids + a name resolver, renders initials chips with a "+n" overflow. Create `presence-avatars.component.ts`:

```typescript
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Dumb component: initials chips for the users currently viewing the board, with +n overflow. */
@Component({
  selector: 'tm-presence-avatars',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (viewerIds().length > 0) {
      <div class="flex items-center -space-x-1" data-testid="presence-avatars">
        @for (id of shown(); track id) {
          <span
            class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-slate-500 text-xs font-medium text-white"
            [title]="nameFor()(id)"
          >{{ initials(nameFor()(id)) }}</span>
        }
        @if (overflow() > 0) {
          <span
            class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-slate-300 text-xs font-medium text-slate-700"
            data-testid="presence-overflow"
          >+{{ overflow() }}</span>
        }
      </div>
    }
  `,
})
export class PresenceAvatarsComponent {
  readonly viewerIds = input.required<string[]>();
  /** id → display name; defaults to the id when unknown. */
  readonly nameFor = input<(id: string) => string>((id) => id);
  readonly max = input(5);

  protected readonly shown = computed(() => this.viewerIds().slice(0, this.max()));
  protected readonly overflow = computed(() => Math.max(0, this.viewerIds().length - this.max()));

  protected initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
```

In `shared/components/index.ts`, add:
```typescript
export * from './presence-avatars.component';
```

- [ ] **Step 11.2: Wire the realtime service into board-detail**

In `features/boards/board-detail.component.ts`:

1. Add imports:
```typescript
import { OnDestroy } from '@angular/core';
import { BoardRealtimeService } from '../../core/realtime';
import { PresenceAvatarsComponent } from '../../shared/components';
```
(Merge `OnDestroy` into the existing `@angular/core` import; the class already implements `OnInit`.)

2. Add `PresenceAvatarsComponent` to the component `imports` array.

3. Inject the service and implement `OnDestroy`. Add to the class:
```typescript
  private readonly realtime = inject(BoardRealtimeService);

  protected readonly viewerIds = this.realtime.viewers;
```

4. In `ngOnInit`, after the existing `void this.store.loadBoard(this.boardId);` and query-param restore, start the hub:
```typescript
    void this.realtime.join(this.boardId, {
      onUpsert: (task) => this.store.applyRealtimeUpsert(task),
      onDelete: (taskId) => this.store.applyRealtimeDelete(taskId),
      onReconnected: () => void this.store.loadBoard(this.boardId),
    });
```

5. Add `OnDestroy` to the class declaration (`implements OnInit, OnDestroy`) and the method:
```typescript
  ngOnDestroy(): void {
    void this.realtime.leave();
  }
```

6. In the template header (the `<div class="mb-6 flex flex-wrap items-center gap-3">` block), after the board title `<h1>` and before the `<span class="flex-1">` spacer, add the avatars:
```html
        <tm-presence-avatars [viewerIds]="viewerIds()" />
```

(Resolving viewer ids → display names via the user-search cache is a refinement; the default `nameFor` shows the id-as-title which the E2E asserts presence appears/disappears by element count. A follow-up can pass a real resolver. Keep the default here — YAGNI for the flagship slice; names are not required for the spec's "presence avatar appears/disappears" behavior.)

- [ ] **Step 11.3: Verify build, lint, full suite**

```bash
npx ng build --configuration development
npm run lint
npx jest
```
Expected: all green. If `board-detail.component.spec.ts` now needs `BoardRealtimeService` provided, add it to the test providers (the service has no hard deps beyond `AuthStore`, which the spec likely already provides; if not, provide both). The realtime `join` will no-op safely in tests because there is no SignalR server — but if the spec errors on the unhandled connection, stub the service in the test providers: `{ provide: BoardRealtimeService, useValue: { viewers: signal([]), join: async () => {}, leave: async () => {} } }`.

- [ ] **Step 11.4: Commit**

```bash
git add frontend/task-manager-app/src/app/shared/components/ frontend/task-manager-app/src/app/features/boards/board-detail.component.ts
git commit -m "feat(frontend): presence avatars + board-detail realtime wiring"
```

---

### Task 12: E2E — two-browser-context live sync + presence

The showpiece: user A moves a card; it moves on user B's open board with no reload; presence avatar appears when B joins and disappears when B leaves.

**Files:**
- Modify: `tests/TaskManager.E2E.Tests/Infrastructure/Flows.cs` (helpers as needed)
- Create: `tests/TaskManager.E2E.Tests/RealtimeSyncFlowTests.cs`

- [ ] **Step 12.1: Read the E2E fixture first**

Read `tests/TaskManager.E2E.Tests/Infrastructure/PlaywrightFixture.cs` and an existing test (e.g. `BoardAndTaskFlowTests.cs`) to learn: how a browser/page is created, how a second `IBrowserContext` is opened, how login + board creation work as reusable flows, the `DragTaskToColumnAsync` helper (it already waits for the server-confirmed move), and the `data-testid` selectors for columns/cards. Two contexts = two logged-in users; both must be members of the same board (user A is owner; invite user B via the existing invite flow, or seed both as members). Match whatever helper the suite already provides for "two users on the same board" — if none exists, build it from the existing login + invite flows.

- [ ] **Step 12.2: Add the two-context sync test**

Create `tests/TaskManager.E2E.Tests/RealtimeSyncFlowTests.cs`. Adapt the page/context/login/board helpers to the real fixture API discovered in 12.1:

```csharp
using Microsoft.Playwright;

namespace TaskManager.E2E.Tests;

[Collection("e2e")]
public class RealtimeSyncFlowTests(PlaywrightFixture fixture) : IClassFixture<PlaywrightFixture>
{
    [Fact]
    public async Task Card_moved_by_one_user_appears_for_another_without_reload()
    {
        // User A: create a board + task, invite user B (reuse the suite's helpers).
        var (a, boardId, userBEmail) = await fixture.NewBoardWithInvitedMemberAsync();
        await Flows.CreateTaskAsync(a, "Realtime card");

        // User B: a second browser context, logged in, on the SAME board.
        var b = await fixture.OpenBoardAsAsync(userBEmail, boardId);
        await Assertions.Expect(Flows.TaskCard(b, "Realtime card")).ToBeVisibleAsync();

        // Presence: A should now see at least one viewer avatar (B joined).
        await Assertions.Expect(a.GetByTestId("presence-avatars")).ToBeVisibleAsync();

        // User A drags the card to Done; assert it lands on B WITHOUT B reloading.
        await Flows.DragTaskToColumnAsync(a, "Realtime card", "Done");
        var bDoneColumn = b.Locator("[data-testid='board-column']", new() { HasText = "Done" });
        await Assertions.Expect(bDoneColumn.GetByText("Realtime card")).ToBeVisibleAsync();

        // Presence disappears when B closes the board.
        await b.CloseAsync();
        await Assertions.Expect(a.GetByTestId("presence-avatars")).ToBeHiddenAsync();
    }
}
```

IMPORTANT: the exact helper names (`NewBoardWithInvitedMemberAsync`, `OpenBoardAsAsync`, `Flows.DragTaskToColumnAsync`, `Flows.TaskCard`, `Flows.CreateTaskAsync`) must match the real fixture. `DragTaskToColumnAsync` already waits for the server-confirmed `POST .../move` (per the suite's prior fix). The presence-hidden assertion relies on the hub's `OnDisconnectedAsync` firing when B's context closes; allow Playwright's auto-retry to absorb the small delay. If a "two members on one board" helper does not exist, implement it in `Flows.cs` from the existing login + `POST /api/boards/{id}/members` (or the invite-member dialog) flows, and a `TaskCard`/column visibility assertion that matches the existing label-filter test idiom.

- [ ] **Step 12.3: Build the E2E project (Docker/stack not required to compile)**

```bash
dotnet build tests/TaskManager.E2E.Tests --configuration Release
```
Expected: 0 errors. Do NOT run locally if Docker is down — the `e2e` CI check runs the full stack.

- [ ] **Step 12.4: Commit**

```bash
git add tests/TaskManager.E2E.Tests/
git commit -m "test(e2e): two-browser live card sync + presence"
```

---

### Task 13: Spec addendum + PR

**Files:**
- Modify: `smart-task-manager-spec.md` (append §13.3 after §13.2)

- [ ] **Step 13.1: Append the addendum**

After the §13.2 block, add:

```markdown

### 13.3 Real-time collaborative boards (Feature 3)
Card changes fan out live to everyone viewing a board, plus presence.

**`BoardHub` (Tasks Presentation, route `/hubs/board`)** — amends §4.4: *user-targeted*
notifications stay in Notifications; *board-scoped* sync lives with the data owner, because
joining a board group requires a membership check only Tasks can do. JWT arrives via the
query string (`?access_token=`, same wiring as the notifications hub); `[Authorize]` on the
hub. `JoinBoard(boardId)` validates membership via the board repository (non-members get a
`HubException`) then adds the connection to group `board:{boardId}` and registers presence;
`LeaveBoard`/`OnDisconnectedAsync` unwind both. Tasks now also validates JWTs (previously it
trusted only the gateway's `X-User-Id` header for REST — REST still does; the hub adds bearer
validation).

**Broadcast** — after each successful task mutation the endpoint fans the fresh `TaskDto`
(`TaskUpserted(task, actorId)`) or `TaskDeleted(taskId, actorId)` to the board group. This is
**best-effort, fire-after-commit, NOT through the RabbitMQ outbox**: the durable path
(Analytics/Notifications) is untouched, and a missed frame self-heals on reload — the right
consistency class for ephemeral UI sync. The Onion rule holds via an `IBoardBroadcaster` port
in Application with the SignalR adapter in Presentation; endpoints invoke it after a successful
`Result`. `DeleteTaskCommand` now returns the board id so the delete endpoint can address the
group.

**Presence** — `IPresenceTracker`, in-memory and connection-refcounted (a user with two tabs
counts once; the last connection leaving removes them). Correct for the single-instance
deployment; the interface is the drop-in seam for a Redis-backed impl if Tasks scales out
(not built now). `PresenceChanged(viewerIds)` broadcasts to the group on every change.

**Gateway** — `/hubs/board` routes to the tasks cluster (more specific than the existing
`/hubs/{**catch-all}` → notifications). CORS already allows the SignalR headers.

**SPA** — `core/realtime/board-realtime.service.ts` manages one hub connection
(`accessTokenFactory`, auto-reconnect, join/leave on board route enter/exit). `boards.store.ts`
applies `TaskUpserted` only when `rowVersion` is **strictly newer** than the local copy
(drops stale frames and the echo of the user's own optimistic write); `TaskDeleted` removes the
card; on reconnect the board is refetched once. Presence avatars (initials chips, "+n" overflow)
render in the board header.
```

- [ ] **Step 13.2: Full local gate**

```bash
dotnet build SmartTaskManager.sln --no-restore
dotnet test tests/TaskManager.Tasks.Tests --filter "FullyQualifiedName~Unit"
cd frontend/task-manager-app && npx jest && npm run lint && cd ..\..
```
Expected: solution builds; Tasks unit tests green; Jest + lint green. (Integration + E2E run on CI where Docker is available.)

- [ ] **Step 13.3: Commit, push, PR**

```bash
git add smart-task-manager-spec.md
git commit -m "docs(spec): v1.1 addendum — real-time collaborative boards (§13.3)"
git push -u origin feature/realtime-boards
gh pr create --base develop --head feature/realtime-boards \
  --title "feat: real-time collaborative boards (v1.1 Feature 3)" \
  --body "Live board sync + presence via a Tasks-hosted SignalR BoardHub. Membership-gated JoinBoard; best-effort fire-after-commit TaskUpserted/TaskDeleted broadcast (not via the outbox); in-memory connection-refcounted presence behind IPresenceTracker. Gateway routes /hubs/board to the tasks cluster. SPA: board-realtime service (accessTokenFactory + auto-reconnect), strictly-newer rowVersion gating in BoardsStore, presence avatars. Tasks now validates JWTs for the hub (REST still trusts the gateway X-User-Id header). Unit tests (presence refcounting, hub auth, store gating), hub-auth integration test, two-browser E2E sync+presence. Spec addendum §13.3. Plan: docs/superpowers/plans/2026-06-11-realtime-boards.md."
```

- [ ] **Step 13.4: Watch the 7 required checks; merge when green**

```bash
gh pr checks --watch
gh pr merge --merge
```
Expected: all 7 green (the `test-dotnet (tasks)` check runs the hub-auth integration test; `e2e` runs the two-browser sync test); merge completes Feature 3.

---

## Self-review notes (already applied)

- **Spec coverage (design § Feature 3):** hub in Tasks with membership-gated `JoinBoard` ✔ (Task 4); JWT via query string ✔ (Task 5); `LeaveBoard`/`OnDisconnectedAsync` unwind ✔ (Task 4); broadcast after every mutation, best-effort, not via outbox ✔ (Task 6); `TaskUpserted(TaskDto, actorId)` / `TaskDeleted(taskId, actorId)` ✔ (Tasks 4, 6); `IBoardBroadcaster` port in Application + SignalR adapter in Presentation ✔ (Tasks 2, 4); `IPresenceTracker` in-memory refcounted + `PresenceChanged` ✔ (Tasks 1, 4); gateway `/hubs/board` route ✔ (Task 8); SPA realtime service with accessTokenFactory + reconnect ✔ (Task 9); strictly-newer `rowVersion` apply + reconnect refetch + `TaskDeleted` removal ✔ (Tasks 9, 10, 11); presence avatars ✔ (Task 11); two-browser E2E sync + presence ✔ (Task 12); spec addendum ✔ (Task 13).
- **Architecture rules:** Domain untouched; the broadcaster/presence **ports** live in Application, **adapters** (SignalR, in-memory tracker) live in Presentation/Infrastructure — the onion/NetArchTest rules stay green. The outbox path is untouched (best-effort SignalR only).
- **Documented exception:** Tasks gains JWT validation for the hub while REST keeps trusting the gateway header — called out in Task 5 and the §13.3 addendum so it isn't mistaken for a regression.
- **Type consistency:** `IBoardBroadcaster.TaskUpsertedAsync/TaskDeletedAsync`, `IPresenceTracker.Join/Leave/RemoveConnection/ViewersOf`, `BoardHub.Group(boardId)`, and the SignalR method names (`TaskUpserted`/`TaskDeleted`/`PresenceChanged`, `JoinBoard`/`LeaveBoard`) are defined once and reused identically across backend (Tasks 1–6), the SPA handlers (Task 9: `onUpsert`/`onDelete`/`onReconnected`), and the store (`applyRealtimeUpsert`/`applyRealtimeDelete`). `DeleteTaskCommand : IRequest<Result<Guid>>` is introduced in Task 3 and consumed in Task 6.
- **No placeholders:** every code step carries real code; every run step carries its command + expected outcome. Where a helper name depends on existing test infra (E2E fixture, delete-handler fixture fields), the step says to confirm and adapt against the real names rather than guess.
- **Risk — broadcast inside the request lifecycle:** the fire-after-commit broadcast uses fire-and-forget with an unobserved-exception guard so a hub hiccup never fails the HTTP response (best-effort by design). The two-browser E2E is the real end-to-end proof; the unit/integration tests cover auth, presence refcounting, and the store's gating deterministically.
```