# Design: Completing Step 3 — Tasks Service

**Date:** 2026-06-10
**Branch:** `feature/tasks-service` (PR target: `develop`)
**Status:** Approved

## Context

`smart-task-manager-spec.md` (§4.3, §5, §11 Steps 3a/3b) is the source of truth for the
Tasks service. This document does not restate it; it records only the decisions the spec
leaves open, plus how we recover the test-first process.

Current state on the branch: the Domain layer is implemented and committed (`b05ab00`) —
entities, value objects, repository interfaces per §4.3. `tests/TaskManager.Tasks.Tests`
exists but contains no test files. The playbook calls for Step 3a (full test suite, red)
before any production code; the Domain commit deviated from that.

## Decisions

### 1. Process recovery

Keep the existing Domain commit — it matches §4.3 and reverting buys nothing. From here on,
strict 3a → 3b:

1. One commit delivering the complete Step 3a test suite, red where production code is missing.
2. Implementation commits (Application → Infrastructure → Presentation) until green.

### 2. Step 3a test suite layout

Mirrors the structure of `tests/TaskManager.Identity.Tests`:

```
tests/TaskManager.Tasks.Tests/
  Architecture/OnionDependencyRuleTests.cs     NetArchTest fixture for the Tasks assemblies
  Unit/
    BoardCommandHandlerTests.cs                happy / validation / authorization per handler
    TaskCommandHandlerTests.cs
    CommentCommandHandlerTests.cs
    LabelCommandHandlerTests.cs
    QueryHandlerTests.cs                       projection correctness, 200-result cap signal
    DeadlineApproachingServiceTests.cs         publishes event for tasks due within 24 h
  Integration/
    TasksWebAppFactory.cs                      Testcontainers PostgreSQL + RabbitMQ,
                                               WebApplicationFactory<Program>,
                                               MassTransit test harness for event assertions
    BoardEndpointsTests.cs                     happy / 404 / Owner-Editor-Viewer boundaries
    TaskEndpointsTests.cs                      incl. move/assign event publication
    CommentEndpointsTests.cs                   incl. TaskCommentAddedEvent publication
    ConcurrencyTests.cs                        stale If-Match → 409 with current body;
                                               two parallel PUTs → second gets 409
    TruncationCapTests.cs                      201 tasks seeded → 200 returned +
                                               X-Result-Truncated: true
```

Unit tests use NSubstitute against repository interfaces — no DB. Integration tests cover
all 22 endpoints per §11 Step 3a.

### 3. Step 3b implementation order

1. **Application** — commands/queries + handlers returning `Result<T>` (FluentResults),
   DTOs, `ValidationBehavior` + `LoggingBehavior`, `IEventPublisher` interface.
   `GetTasksQuery` returns `Result<(IReadOnlyList<TaskDto>, bool truncated)>`.
2. **Infrastructure** — `TasksDbContext` implementing `IUnitOfWork`; EF configs with
   `Color` as owned entity of `Label` (single column) and `RowVersion` concurrency token
   on `TaskItem`; repository implementations; MassTransit publisher for all 6 event types;
   EF Core **outbox** on `TasksDbContext`; migrations.
3. **Presentation** — `BoardEndpoints` / `TaskEndpoints` route groups; authorization via
   `X-User-Id` header + board-membership check; hourly `IHostedService` for
   `DeadlineApproachingEvent`; Serilog + health checks per §8.

### 4. Verification strategy (no local Docker)

Docker is not installed on this dev machine. Locally we run `dotnet build` plus the unit
and architecture tests. Integration tests (Testcontainers) are verified by CI on push to
`feature/tasks-service` — same arrangement the Identity service used. Red-state
confirmation for 3a therefore applies locally to unit/architecture tests; integration
tests will fail-fast at compile time until 3b lands, which is acceptable evidence of red.

### 5. Branch / PR shape

All of Step 3 remains on `feature/tasks-service` and lands as a single PR into `develop`,
per the §11 playbook. Conventional Commits throughout (`test(tasks): …`, `feat(tasks): …`).

## Out of scope

Notifications consumers (Step 4), Analytics inbox (Step 5), gateway routes (Step 6),
and any Angular work (Step 7).
