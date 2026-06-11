# Smart Task Manager — Project Specification
**Stack:** .NET 10 · Angular 18 · PostgreSQL · Redis · RabbitMQ  
**Architecture:** Microservices · API Gateway (YARP) · Event-driven · Onion (Clean) Architecture per service  
**Approach:** Spec-first, TDD — write tests before implementation; this document is the source of truth for Claude Code
---
## 1. Project overview
A multi-user task management application with Kanban boards, team assignments, deadline reminders, and a personal analytics dashboard. Users can create boards, add tasks with labels and due dates, assign them to teammates, and receive real-time notifications when tasks change.
### Goals
- Practice clean microservice boundaries and event-driven patterns
- Apply Onion (Clean) Architecture within each service: Domain → Application → Infrastructure → Presentation
- Demonstrate CQRS with source-generated Mediator, rich domain models, and the Result pattern
- Build a polished Angular SPA with reactive state management (NgRx Signals) and strict component conventions
- Keep local dev simple: one `docker compose up` starts everything
### Non-goals (out of scope for v1)
- File attachments
- OAuth / social login
- Mobile app
- Multi-tenancy
---
## 2. Solution structure
```
/smart-task-manager
├── src/
│   ├── gateway/
│   │   └── TaskManager.Gateway/
│   │       ├── Middleware/             # ExceptionHandling, CorrelationId
│   │       └── Program.cs
│   ├── services/
│   │   ├── identity/
│   │   │   └── TaskManager.Identity/
│   │   │       ├── Domain/
│   │   │       │   ├── Entities/       # AppUser, RefreshToken (rich models)
│   │   │       │   └── Interfaces/     # IUserRepository, ITokenService
│   │   │       ├── Application/
│   │   │       │   ├── Commands/       # RegisterCommand+Handler, LoginCommand+Handler, etc.
│   │   │       │   ├── Queries/        # GetUserQuery+Handler, SearchUsersQuery+Handler
│   │   │       │   ├── DTOs/
│   │   │       │   └── Behaviors/      # ValidationBehavior, LoggingBehavior
│   │   │       ├── Infrastructure/
│   │   │       │   ├── Persistence/    # IdentityDbContext, repositories, migrations
│   │   │       │   └── Services/       # TokenService, PasswordService
│   │   │       └── Presentation/
│   │   │           ├── Endpoints/      # AuthEndpoints, UserEndpoints (Minimal API groups)
│   │   │           └── Middleware/
│   │   ├── tasks/
│   │   │   └── TaskManager.Tasks/
│   │   │       ├── Domain/
│   │   │       │   ├── Entities/       # Board, TaskItem, BoardMember, Label, TaskComment
│   │   │       │   ├── ValueObjects/   # TaskStatus, TaskPriority, BoardRole, Color
│   │   │       │   ├── Interfaces/     # IBoardRepository, ITaskRepository, IUnitOfWork
│   │   │       │   └── Events/         # domain events raised by entity methods (internal)
│   │   │       ├── Application/
│   │   │       │   ├── Commands/       # one file per command+handler pair
│   │   │       │   ├── Queries/        # one file per query+handler pair
│   │   │       │   ├── DTOs/
│   │   │       │   └── Behaviors/      # ValidationBehavior, LoggingBehavior
│   │   │       ├── Infrastructure/
│   │   │       │   ├── Persistence/    # TasksDbContext, EF configs, repositories, migrations
│   │   │       │   └── Messaging/      # MassTransit publishers
│   │   │       └── Presentation/
│   │   │           ├── Endpoints/      # BoardEndpoints, TaskEndpoints (Minimal API groups)
│   │   │           └── Middleware/
│   │   ├── notifications/
│   │   │   └── TaskManager.Notifications/
│   │   │       ├── Application/
│   │   │       │   ├── EventHandlers/  # MassTransit consumers
│   │   │       │   ├── Services/       # INotificationService, IEmailService (interfaces)
│   │   │       │   └── DTOs/
│   │   │       ├── Infrastructure/
│   │   │       │   ├── Redis/          # notification history + preferences
│   │   │       │   ├── Email/          # MailKit implementation
│   │   │       │   └── Hubs/           # SignalR NotificationsHub
│   │   │       └── Presentation/
│   │   │           └── Endpoints/      # NotificationEndpoints
│   │   └── analytics/
│   │       └── TaskManager.Analytics/
│   │           ├── Domain/
│   │           │   └── ReadModels/     # TaskEventRecord, BoardStats, UserStats
│   │           ├── Application/
│   │           │   └── Projections/    # MassTransit consumers → read model updates
│   │           ├── Infrastructure/
│   │           │   └── Persistence/    # AnalyticsDbContext, migrations
│   │           └── Presentation/
│   │               └── Endpoints/      # AnalyticsEndpoints
│   └── shared/
│       └── TaskManager.Contracts/      # Integration event DTOs only
├── tests/
│   ├── TaskManager.Identity.Tests/     # xUnit · unit + integration
│   ├── TaskManager.Tasks.Tests/        # xUnit · unit + integration
│   ├── TaskManager.Notifications.Tests/
│   ├── TaskManager.Analytics.Tests/
│   ├── TaskManager.Gateway.Tests/
│   └── TaskManager.E2E.Tests/          # Playwright · end-to-end
├── frontend/
│   └── task-manager-app/
│       └── src/app/
│           ├── core/
│           │   ├── auth/               # AuthStore, AuthGuard, interceptors
│           │   ├── http/               # typed API service per microservice
│           │   └── notifications/      # SignalR service, NotificationStore
│           ├── features/               # smart container components per feature
│           │   ├── auth/
│           │   ├── boards/
│           │   ├── tasks/
│           │   └── analytics/
│           └── shared/
│               ├── components/         # dumb/presentational components only
│               └── pipes/
├── docker-compose.yml
├── docker-compose.override.yml         # local dev overrides
└── README.md
```
### .NET solution file
One `SmartTaskManager.sln` at root referencing all five .NET projects.

### Architecture principles

**Onion layers and dependency rule**
Each .NET service follows the same four-layer onion. Dependencies flow inward only:
```
Presentation  ──►  Application  ──►  Domain
Infrastructure ──►  Application  ──►  Domain
```
- **Domain** — zero external NuGet dependencies. Entities, value objects, repository interfaces, `IUnitOfWork`. No EF Core, no framework references.
- **Application** — depends on Domain only. Defines commands, queries, handlers, DTOs, pipeline behaviors, and port interfaces for infrastructure services. References `FluentValidation` and `Mediator`.
- **Infrastructure** — implements Domain/Application interfaces. References EF Core, MassTransit, MailKit, StackExchange.Redis, Mapperly. Never referenced by Domain or Application.
- **Presentation** — registers all DI, configures Minimal API endpoints, middleware. Depends on all three inner layers.

**Rich domain model**
Entities are not property bags:
- All setters are `private`; state changes only through named methods
- `private` parameterless constructor for EF Core materialization
- Static `Create()` factory method validates invariants and raises initial domain events
- Collections exposed as `IReadOnlyList<T>`, mutated through aggregate methods
- Value objects (records) for anything with validation logic (e.g. `Color`)

**Result pattern**
Handlers return `Result<T>` (FluentResults, free, MIT). Expected domain failures (not found, conflict, unauthorized) return `Result.Fail(...)` — never throw exceptions. Presentation layer maps `Result` to HTTP status codes via a shared `ToHttpResult()` extension.

**Repository + Unit of Work**
- One repository interface per aggregate root in `Domain/Interfaces/`
- `IUnitOfWork.SaveChangesAsync()` defined in Domain, implemented by DbContext in Infrastructure
- Handlers depend on repository interfaces — never on DbContext directly

**Mediator pipeline behaviors (Application layer)**
1. `ValidationBehavior<TRequest, TResponse>` — runs FluentValidation; returns `Result.Fail` with validation errors instead of throwing
2. `LoggingBehavior<TRequest, TResponse>` — logs command/query name, elapsed time, success/failure

**Minimal API endpoint groups**
No Controllers. Each service uses Minimal API `RouteGroupBuilder` extension methods, one class per aggregate (e.g. `BoardEndpoints.Map(app)`). Endpoint handlers are thin: extract user ID from headers, dispatch to `IMediator`, call `ToHttpResult()`.

**Angular: smart / dumb component split**
- **Smart (container) components** live in `features/`. They inject stores, dispatch actions, read signals. They do not receive `@Input` for domain data.
- **Dumb (presentational) components** live in `shared/components/`. They receive data via `input()` signals, emit via `output()`. No store injection, fully reusable and independently testable.

**Angular: non-negotiable conventions**
- `ChangeDetectionStrategy.OnPush` on every component
- `inject()` function — never constructor injection
- Angular 17+ signal APIs: `input()`, `output()`, `model()`, `toSignal()`, `computed()`
- `strict: true` + `strictTemplates: true` in tsconfig.json
- ESLint (`@angular-eslint`) + Prettier enforced — CI fails on lint errors
- All feature routes use `loadComponent` (lazy) — no eagerly loaded feature components
- `trackBy` on every `@for` / `*ngFor`
- No direct DOM manipulation — use Angular renderer or CDK abstractions
---
## 3. Infrastructure (docker-compose)
### Services required
| Container         | Image                        | Port  | Purpose                          |
|-------------------|------------------------------|-------|----------------------------------|
| `gateway`         | custom Dockerfile            | 5000  | YARP API gateway                 |
| `identity-svc`    | custom Dockerfile            | 5001  | Identity service                 |
| `tasks-svc`       | custom Dockerfile            | 5002  | Tasks service                    |
| `notifications-svc` | custom Dockerfile          | 5003  | Notifications + SignalR          |
| `analytics-svc`   | custom Dockerfile            | 5004  | Analytics service                |
| `postgres`        | postgres:16-alpine           | 5432  | Shared host, per-service DBs     |
| `redis`           | redis:7-alpine               | 6379  | Notification prefs + cache       |
| `rabbitmq`        | rabbitmq:3-management-alpine | 5672  | Message bus (management: 15672)  |
| `seq`             | datalust/seq:latest          | 5341  | Structured log aggregation       |
| `mailhog`         | mailhog/mailhog              | 1025/8025 | Local SMTP catcher (override only) |
| `angular-dev`     | node:22-alpine               | 4200  | Angular dev server (override only)|
### Per-service databases
Each service creates its own database on startup via EF Core migrations. Database names: `identity_db`, `tasks_db`, `analytics_db`.  
Notifications service uses Redis only (no relational DB).
### Health checks
Every service exposes `GET /health`. Docker Compose `healthcheck` uses this. Services depend on postgres/redis/rabbitmq being healthy before starting.
---
## 4. Service specifications
---
### 4.1 API Gateway — `TaskManager.Gateway`
**Technology:** .NET 10 · YARP 2.x · no EF Core
**Responsibilities:**
- Route all incoming HTTP traffic to the correct downstream service
- Validate JWT Bearer tokens on all routes except `/api/auth/**`
- Forward `X-User-Id` and `X-User-Email` headers to downstream services (extracted from JWT claims)
- Rate limiting: 100 req/min per IP (use ASP.NET Core rate limiting middleware)
- CORS policy: origin `http://localhost:4200`; methods `GET, POST, PUT, DELETE, OPTIONS`; headers `Authorization, Content-Type, X-Correlation-Id, If-Match`; **AllowCredentials** (required for the refresh cookie)
**YARP route configuration:**
| Route prefix           | Downstream cluster    | Auth required |
|------------------------|-----------------------|---------------|
| `/api/auth/**`         | `identity-cluster`    | No            |
| `/api/users/**`        | `identity-cluster`    | Yes           |
| `/api/boards/**`       | `tasks-cluster`       | Yes           |
| `/api/tasks/**`        | `tasks-cluster`       | Yes           |
| `/api/notifications/**`| `notifications-cluster` | Yes         |
| `/api/analytics/**`    | `analytics-cluster`   | Yes           |
| `/hubs/**`             | `notifications-cluster` | Yes (WS)    |
**JWT settings (appsettings.json):**
```json
{
  "Jwt": {
    "Issuer": "TaskManager.Identity",
    "Audience": "TaskManager",
    "SecretKey": "<from env var JWT_SECRET>"
  }
}
```

**Security**
- **HTTPS-only in production** — gateway listens on HTTPS; plain-HTTP requests are redirected. Local dev over HTTP is fine.
- **Response-headers middleware** sets on every response: `Strict-Transport-Security: max-age=31536000; includeSubDomains`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a CSP allowing `self` for scripts/styles and the SignalR WebSocket origin.
- **Tighter rate-limit on auth routes** — `/api/auth/login`, `/api/auth/register`, `/api/auth/refresh` use a separate policy capped at 10 req/min per IP (the global 100/min still applies to everything else). Stops credential stuffing without affecting normal app traffic.

**No business logic.** The gateway must not contain domain code.
---
### 4.2 Identity Service — `TaskManager.Identity`
**Technology:** .NET 10 · ASP.NET Core Identity · Mediator (martinothamar) · EF Core 10 · PostgreSQL · BCrypt
**Layer structure:** Domain → Application → Infrastructure → Presentation (see §2 Architecture principles)

**Documented exception to the rich-model rule** — `AppUser` inherits `IdentityUser<Guid>`, which exposes public setters on inherited properties (`Email`, `UserName`, `EmailConfirmed`, `LockoutEnd`, …). This is the *only* entity in the project exempt from the "all setters private" rule; the exemption is necessary because ASP.NET Core Identity's `UserManager` writes to those properties directly. Every other entity in every other service follows the rule strictly.

**Domain model (rich — private setters, factory methods, behavior):**
```csharp
// Domain/Entities/AppUser.cs
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = default!;
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AppUser() { } // EF Core / Identity

    public static AppUser Create(string email, string displayName)
        => new() { Email = email, UserName = email, DisplayName = displayName, CreatedAt = DateTimeOffset.UtcNow };

    public void UpdateProfile(string displayName, string? avatarUrl)
    {
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
    }
}

// Domain/Entities/RefreshToken.cs
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!; // opaque random 64-byte hex
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTimeOffset expiresAt)
        => new() { Id = Guid.NewGuid(), UserId = userId, Token = token, ExpiresAt = expiresAt, CreatedAt = DateTimeOffset.UtcNow };

    public void Revoke() => IsRevoked = true;
    public bool IsValid() => !IsRevoked && ExpiresAt > DateTimeOffset.UtcNow;
}
```

**Domain interfaces (Application layer depends on these; Infrastructure implements them):**
```csharp
// Domain/Interfaces/IUserRepository.cs
public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<List<AppUser>> SearchAsync(string query, CancellationToken ct = default);
}

// Domain/Interfaces/IRefreshTokenRepository.cs
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    void Add(RefreshToken token);
}

// Domain/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```
**Endpoints:**
| Method | Path                    | Auth | Description                                   |
|--------|-------------------------|------|-----------------------------------------------|
| POST   | `/api/auth/register`    | No   | Create account, return token pair             |
| POST   | `/api/auth/login`       | No   | Validate credentials, return token pair       |
| POST   | `/api/auth/refresh`     | No   | Exchange refresh token for new pair           |
| POST   | `/api/auth/logout`      | Yes  | Revoke current refresh token                  |
| GET    | `/api/users/me`         | Yes  | Return current user profile                   |
| PUT    | `/api/users/me`         | Yes  | Update display name / avatar URL              |
| GET    | `/api/users/{id}`       | Yes  | Get public profile of any user (for assignment UI) |
| GET    | `/api/users/search?q=`  | Yes  | Search users by display name or email (for assignment) |
**Token strategy:**
- Access token: JWT, 15-minute expiry, signed with HS256, contains `sub` (userId), `email`, `name`
- Refresh token: opaque random token (64-byte cryptographic random, hex-encoded), 7-day expiry, stored hashed (SHA-256) in `refresh_tokens` table — the plaintext token only ever leaves the server in the cookie
- On refresh: validate token exists + not revoked + not expired → issue new pair + revoke old token (rotation)
- **Password hashing**: BCrypt via `BCrypt.Net-Next` with work factor 12. Never log password values or refresh-token plaintext.
- **Refresh-token cookie attributes**: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth/refresh` — unreadable from JavaScript and not sent on cross-site requests, which removes the CSRF surface on the refresh endpoint. (Access token lives in memory on the SPA and travels via `Authorization: Bearer`, so it is not cookie-bound and not CSRF-exposed.) **Cross-domain deployment caveat:** `SameSite=Strict` blocks the refresh cookie if the SPA and API are not same eTLD+1 — see §10 *Deployment compatibility — refresh cookie* before deploying SPA and API to unrelated public domains.
- **Reuse detection**: if a refresh token that is *already revoked* is presented, treat it as token theft — revoke every refresh token belonging to that user and return 401. Forces the attacker and the legitimate user both to re-authenticate.
**Request/response DTOs:**
```csharp
record RegisterRequest(string Email, string DisplayName, string Password);
record LoginRequest(string Email, string Password);
record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
record UserDto(Guid Id, string Email, string DisplayName, string? AvatarUrl);
record UpdateProfileRequest(string DisplayName, string? AvatarUrl);
```
**Validation rules:**
- Email: valid format, unique in DB
- Password: min 8 chars, at least one digit, one uppercase
- DisplayName: 2–50 chars
**Error responses:** Use `ProblemDetails` (RFC 7807) for all errors.
---
### 4.3 Tasks Service — `TaskManager.Tasks`
**Technology:** .NET 10 · ASP.NET Core · Mediator (martinothamar) · EF Core 10 · PostgreSQL · MassTransit (RabbitMQ)

**Layer structure:** Domain → Application → Infrastructure → Presentation (see §2 Architecture principles)

**Domain — value objects:**
```csharp
// Domain/ValueObjects/
public enum TaskStatus   { Todo, InProgress, Review, Done }
public enum TaskPriority { Low, Medium, High, Critical }
public enum BoardRole    { Owner, Editor, Viewer }

public record Color
{
    public string Value { get; }
    private Color(string value) => Value = value;
    public static Result<Color> Create(string value)
    {
        if (!Regex.IsMatch(value, @"^#[0-9A-Fa-f]{6}$"))
            return Result.Fail<Color>("Color must be a valid hex string e.g. #4ade80");
        return Result.Ok(new Color(value));
    }
}
```

EF Core configuration maps `Color` as an **owned entity** of `Label` so it persists as a single column (`labels.color`) rather than a separate table.

**Domain — entities (rich model, private setters, factory methods):**
```csharp
// Domain/Entities/Board.cs
public class Board
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<BoardMember> _members = [];
    private readonly List<TaskItem> _tasks = [];
    public IReadOnlyList<BoardMember> Members => _members.AsReadOnly();
    public IReadOnlyList<TaskItem> Tasks => _tasks.AsReadOnly();

    private Board() { }

    public static Board Create(string name, Guid ownerId, string? description = null)
    {
        var board = new Board { Id = Guid.NewGuid(), Name = name, Description = description, OwnerId = ownerId, CreatedAt = DateTimeOffset.UtcNow };
        board._members.Add(BoardMember.Create(board.Id, ownerId, BoardRole.Owner));
        return board;
    }

    public Result AddMember(Guid userId, BoardRole role)
    {
        if (_members.Any(m => m.UserId == userId)) return Result.Fail("User is already a member");
        _members.Add(BoardMember.Create(Id, userId, role));
        return Result.Ok();
    }

    public Result RemoveMember(Guid userId)
    {
        if (userId == OwnerId) return Result.Fail("Cannot remove the board owner");
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) return Result.Fail("Member not found");
        _members.Remove(member);
        return Result.Ok();
    }

    public void Update(string name, string? description) { Name = name; Description = description; }
}

// Domain/Entities/TaskItem.cs
public class TaskItem
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = default!; // EF Core concurrency token

    private readonly List<TaskLabel> _labels = [];
    private readonly List<TaskComment> _comments = [];
    public IReadOnlyList<TaskLabel> Labels => _labels.AsReadOnly();
    public IReadOnlyList<TaskComment> Comments => _comments.AsReadOnly();

    private TaskItem() { }

    public static TaskItem Create(Guid boardId, string title, Guid createdBy, TaskPriority priority, DateTimeOffset? dueDate = null)
        => new() { Id = Guid.NewGuid(), BoardId = boardId, Title = title, CreatedBy = createdBy,
                   Priority = priority, Status = TaskStatus.Todo, DueDate = dueDate,
                   Position = 0, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

    public void Move(TaskStatus newStatus, int position) { Status = newStatus; Position = position; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Assign(Guid? userId) { AssignedTo = userId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Update(string title, string? description, TaskPriority priority, DateTimeOffset? dueDate)
        { Title = title; Description = description; Priority = priority; DueDate = dueDate; UpdatedAt = DateTimeOffset.UtcNow; }
    public TaskComment AddComment(Guid authorId, string body) { var c = TaskComment.Create(Id, authorId, body); _comments.Add(c); return c; }
    public void AddLabel(Guid labelId) { if (!_labels.Any(l => l.LabelId == labelId)) _labels.Add(new TaskLabel { TaskId = Id, LabelId = labelId }); }
    public void RemoveLabel(Guid labelId) => _labels.RemoveAll(l => l.LabelId == labelId);
}

// Domain/Entities/BoardMember.cs
public class BoardMember
{
    public Guid BoardId { get; private set; }
    public Guid UserId { get; private set; }
    public BoardRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    private BoardMember() { }
    public static BoardMember Create(Guid boardId, Guid userId, BoardRole role)
        => new() { BoardId = boardId, UserId = userId, Role = role, JoinedAt = DateTimeOffset.UtcNow };
}

// Domain/Entities/Label.cs
public class Label
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public string Name { get; private set; } = default!;
    public Color Color { get; private set; } = default!;

    private Label() { }
    public static Result<Label> Create(Guid boardId, string name, string colorHex)
    {
        var colorResult = Color.Create(colorHex);
        if (colorResult.IsFailed) return colorResult.ToResult<Label>();
        return Result.Ok(new Label { Id = Guid.NewGuid(), BoardId = boardId, Name = name, Color = colorResult.Value });
    }
}

// Domain/Entities/TaskComment.cs
public class TaskComment
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }

    private TaskComment() { }
    public static TaskComment Create(Guid taskId, Guid authorId, string body)
        => new() { Id = Guid.NewGuid(), TaskId = taskId, AuthorId = authorId, Body = body, CreatedAt = DateTimeOffset.UtcNow };
    public void Edit(string body) { Body = body; EditedAt = DateTimeOffset.UtcNow; }
}
```

**Optimistic concurrency on `TaskItem`**
The `RowVersion` column is mapped as an EF Core concurrency token (`builder.Property(t => t.RowVersion).IsRowVersion()`). All write endpoints that mutate a task — `PUT /api/tasks/{id}`, `POST /api/tasks/{id}/move`, `POST /api/tasks/{id}/assign`, `PUT /api/tasks/{id}/comments/{commentId}` — require an `If-Match` header carrying the base64-encoded `RowVersion` the client last observed. The endpoint returns the current `RowVersion` in an `ETag` header on every read and write. A mismatch surfaces as `DbUpdateConcurrencyException`, which the handler maps to `Result.Fail("conflict: task was modified")`; the presentation layer translates that to **409 Conflict** with the current task body so the SPA can refetch + toast.

**Domain — repository interfaces:**
```csharp
// Domain/Interfaces/IBoardRepository.cs
public interface IBoardRepository
{
    Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Board>> GetByMemberAsync(Guid userId, CancellationToken ct = default);
    void Add(Board board);
    void Remove(Board board);
}

// Domain/Interfaces/ITaskRepository.cs
public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TaskItem>> QueryAsync(TaskFilterParams filter, CancellationToken ct = default);
    void Add(TaskItem task);
    void Remove(TaskItem task);
}

// Domain/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```
**CQRS structure using Mediator (martinothamar):**
Source-generator-based mediator with near-identical API to MediatR. Register with `.AddMediator()`, dispatch via `IMediator`. All handler wiring happens at compile time.

Commands (writes):
- `CreateBoardCommand` / `UpdateBoardCommand` / `DeleteBoardCommand`
- `AddBoardMemberCommand` / `RemoveBoardMemberCommand`
- `CreateTaskCommand` / `UpdateTaskCommand` / `DeleteTaskCommand`
- `MoveTaskCommand` (change status + position)
- `AssignTaskCommand`
- `AddCommentCommand` / `EditCommentCommand` / `DeleteCommentCommand`
- `CreateLabelCommand` / `DeleteLabelCommand`
- `AddLabelToTaskCommand` / `RemoveLabelFromTaskCommand`

Queries (reads):
- `GetBoardsQuery` — boards where user is a member
- `GetBoardQuery` — single board with tasks grouped by status
- `GetTaskQuery` — single task with comments and labels
- `GetTasksQuery` — filterable: `?boardId=&assignedTo=&status=&priority=&dueBefore=`

Handler pattern — handlers return `Result<T>`, depend on repository interfaces, never DbContext:
```csharp
// Application/Commands/CreateBoardCommand.cs
public record CreateBoardCommand(string Name, string? Description, Guid UserId) : IRequest<Result<BoardDto>>;

public class CreateBoardCommandHandler : IRequestHandler<CreateBoardCommand, Result<BoardDto>>
{
    private readonly IBoardRepository _boards;
    private readonly IUnitOfWork _uow;
    private readonly IBoardMapper _mapper; // Mapperly-generated mapper

    public CreateBoardCommandHandler(IBoardRepository boards, IUnitOfWork uow, IBoardMapper mapper)
        => (_boards, _uow, _mapper) = (boards, uow, mapper);

    public async ValueTask<Result<BoardDto>> Handle(CreateBoardCommand cmd, CancellationToken ct)
    {
        var board = Board.Create(cmd.Name, cmd.UserId, cmd.Description);
        _boards.Add(board);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok(_mapper.ToDto(board));
    }
}
```

Presentation layer maps `Result<T>` to HTTP responses via a shared extension:
```csharp
// Presentation/Extensions/ResultExtensions.cs
public static IResult ToHttpResult<T>(this Result<T> result) => result switch
{
    { IsSuccess: true }                          => Results.Ok(result.Value),
    { Errors: var e } when e.Any(x => x.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                                                 => Results.NotFound(e.First().Message),
    { Errors: var e } when e.Any(x => x.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                                                 => Results.Forbid(),
    _                                            => Results.BadRequest(result.Errors.Select(e => e.Message))
};
```
**Endpoints:**
| Method | Path                                     | Description                        |
|--------|------------------------------------------|------------------------------------|
| GET    | `/api/boards`                            | List user's boards                 |
| POST   | `/api/boards`                            | Create board                       |
| GET    | `/api/boards/{id}`                       | Get board with tasks               |
| PUT    | `/api/boards/{id}`                       | Update board name/description      |
| DELETE | `/api/boards/{id}`                       | Delete board (owner only)          |
| POST   | `/api/boards/{id}/members`               | Add member                         |
| DELETE | `/api/boards/{id}/members/{userId}`      | Remove member                      |
| GET    | `/api/tasks`                             | Query tasks (filterable)           |
| POST   | `/api/tasks`                             | Create task                        |
| GET    | `/api/tasks/{id}`                        | Get task detail                    |
| PUT    | `/api/tasks/{id}`                        | Update task                        |
| DELETE | `/api/tasks/{id}`                        | Delete task                        |
| POST   | `/api/tasks/{id}/move`                   | Move task (status + position)      |
| POST   | `/api/tasks/{id}/assign`                 | Assign task to user                |
| POST   | `/api/tasks/{id}/comments`               | Add comment                        |
| PUT    | `/api/tasks/{id}/comments/{commentId}`   | Edit comment                       |
| DELETE | `/api/tasks/{id}/comments/{commentId}`   | Delete comment                     |
| GET    | `/api/boards/{id}/labels`                | List board labels                  |
| POST   | `/api/boards/{id}/labels`                | Create label                       |
| DELETE | `/api/boards/{id}/labels/{labelId}`      | Delete label                       |
| POST   | `/api/tasks/{id}/labels/{labelId}`       | Add label to task                  |
| DELETE | `/api/tasks/{id}/labels/{labelId}`       | Remove label from task             |

**Pagination policy (v1)** — `GET /api/tasks` returns at most **200 results** per call. No skip/limit/cursor params in v1; if the filtered result set exceeds the cap, the handler returns the first 200 (ordered by `UpdatedAt DESC`) and sets response header `X-Result-Truncated: true`. Pagination is a v2 concern. The SPA should display a non-blocking notice when the header is present (boards routinely exceed 200 tasks is itself an indication to revisit the v2 paging design).

**Authorization rules:**
- All task/board operations require the acting user to be a board member
- Only `Owner` role can delete boards, add/remove members, create/delete labels
- `Owner` or `Editor` can create/update/delete tasks
- `Viewer` can only read
**Events published to RabbitMQ (exchange: `task-manager`, type: topic):**
```csharp
// All in TaskManager.Contracts namespace
record TaskCreatedEvent(Guid TaskId, Guid BoardId, string Title, Guid CreatedBy, DateTimeOffset CreatedAt);
record TaskAssignedEvent(Guid TaskId, Guid BoardId, string Title, Guid AssignedTo, Guid AssignedBy, DateTimeOffset DueDate?);
record TaskStatusChangedEvent(Guid TaskId, Guid BoardId, string Title, string OldStatus, string NewStatus, Guid ChangedBy);
record TaskCompletedEvent(Guid TaskId, Guid BoardId, string Title, Guid CompletedBy, DateTimeOffset CompletedAt);
record TaskCommentAddedEvent(Guid TaskId, Guid BoardId, Guid CommentId, Guid AuthorId, string Body);
record DeadlineApproachingEvent(Guid TaskId, Guid BoardId, string Title, Guid AssignedTo, DateTimeOffset DueDate);
```
Routing keys: `task.created`, `task.assigned`, `task.status-changed`, `task.completed`, `task.comment-added`, `task.deadline-approaching`
`DeadlineApproachingEvent` is published by a background `IHostedService` that queries tasks due in the next 24 hours and runs every hour.

**Reliable publishing — MassTransit EF Core outbox**
Enable `AddEntityFrameworkOutbox<TasksDbContext>` on the bus configuration. Domain events are written to an outbox table inside the same transaction as the aggregate change; a hosted delivery service drains the outbox to RabbitMQ. This guarantees no events are lost if the process dies between `SaveChangesAsync` and the broker ack. The outbox tables (`InboxState`, `OutboxMessage`, `OutboxState`) are created by EF Core migrations — no extra infrastructure.
---
### 4.4 Notifications Service — `TaskManager.Notifications`
**Technology:** .NET 10 · ASP.NET Core · SignalR · MassTransit · Redis · MailKit
**Responsibilities:**
- Subscribe to all domain events from RabbitMQ
- Push real-time in-app notifications to connected users via SignalR
- Send email notifications (configurable per user)
- Store notification history in Redis (last 50 per user, 30-day TTL)
- Expose REST endpoints for notification history and preferences
**SignalR hub:** `NotificationsHub` at `/hubs/notifications`
- Clients join a group named after their user ID on connect
- Server method: `SendNotification(NotificationDto notification)`
- **Auth:** browsers cannot set headers on the WebSocket handshake, so the JWT is passed via query string. SPA uses `accessTokenFactory: () => authStore.accessToken()` when calling `withUrl('/hubs/notifications', { accessTokenFactory })`. The gateway forwards the WS request to this service; the service wires `JwtBearerEvents.OnMessageReceived` to lift `access_token` off `context.Request.Query` whenever the path starts with `/hubs/`, so the standard `[Authorize]` attribute on the hub works unchanged.
**NotificationDto:**
```csharp
record NotificationDto(
    Guid Id,
    string Type,         // task_assigned | task_commented | deadline_approaching | task_completed
    string Title,
    string Body,
    Guid? RelatedTaskId,
    Guid? RelatedBoardId,
    bool IsRead,
    DateTimeOffset CreatedAt
);
```
**Event → notification mapping:**
| Event                   | Notify who        | Title template                                   |
|-------------------------|-------------------|--------------------------------------------------|
| `TaskAssignedEvent`     | AssignedTo user   | "{AssignedBy} assigned you "{Title}""            |
| `TaskCommentAddedEvent` | Task assignee     | "New comment on "{Title}""                       |
| `DeadlineApproachingEvent` | AssignedTo user | ""{Title}" is due tomorrow"                    |
| `TaskCompletedEvent`    | Board members     | ""{Title}" was completed"                       |
**REST endpoints:**
| Method | Path                              | Description                          |
|--------|-----------------------------------|--------------------------------------|
| GET    | `/api/notifications`              | Last 50 notifications for current user |
| POST   | `/api/notifications/{id}/read`    | Mark one as read                     |
| POST   | `/api/notifications/read-all`     | Mark all as read                     |
| GET    | `/api/notifications/preferences`  | Get email notification preferences   |
| PUT    | `/api/notifications/preferences`  | Update preferences                   |
**NotificationPreferences model (stored in Redis hash):**
```csharp
record NotificationPreferences(
    bool EmailOnAssigned,       // default: true
    bool EmailOnComment,        // default: false
    bool EmailOnDeadline,       // default: true
    bool EmailOnCompleted       // default: false
);
```

**Redis schema**
- **Notification history** — per-user sorted set at key `notifications:user:{userId}`. Score = unix-ms timestamp; value = JSON-serialised `NotificationDto`. On write: `ZADD` the new entry, then `ZREMRANGEBYRANK 0 -51` to keep only the 50 newest, then `EXPIRE 2592000` (30 days, refreshed on every write so the key persists for active users).
- **Preferences** — per-user hash at key `prefs:user:{userId}`, fields match `NotificationPreferences`. No TTL (preferences persist for the account lifetime).
- **Read state** — a boolean flag per notification is stored *inside* the JSON value in the sorted set (set via read-modify-write); avoids a second key per notification.

**Email:** Use MailKit with SMTP settings from environment variables. For local dev, use Mailhog (`mailhog/mailhog` docker image on port 1025/8025). Simple HTML templates, no external template engine needed.
---
### 4.5 Analytics Service — `TaskManager.Analytics`
**Technology:** .NET 10 · ASP.NET Core · EF Core 10 · PostgreSQL · MassTransit
**Approach:** Pure read side. Subscribes to events and projects them into denormalised read models. No commands, no domain logic.

**Idempotent consumers** — register MassTransit's EF Core inbox on `AnalyticsDbContext`. Duplicate deliveries (from broker retries or outbox redelivery on the publisher side) are filtered by `MessageId` before reaching projection logic, so read-model counters cannot double-increment. Notifications service deliberately does *not* use an inbox: a rare duplicate toast/email is acceptable and adding a dedup store to a Redis-only service is more complication than the failure mode warrants.
**Read models (EF Core entities → `analytics_db`):**
```csharp
public class TaskEventRecord
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid BoardId { get; set; }
    public string EventType { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
public class BoardStats
{
    public Guid BoardId { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
public class UserStats
{
    public Guid UserId { get; set; }
    public int TasksCreated { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksAssigned { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
```
**Endpoints:**
| Method | Path                                        | Description                                      |
|--------|---------------------------------------------|--------------------------------------------------|
| GET    | `/api/analytics/boards/{id}/summary`        | Counts by status + overdue count                 |
| GET    | `/api/analytics/boards/{id}/completion-trend` | Tasks completed per day for last 30 days       |
| GET    | `/api/analytics/users/me/summary`           | Personal stats: created, completed, assigned     |
| GET    | `/api/analytics/users/me/activity`          | Activity timeline (last 30 events)               |
All endpoints read from pre-aggregated read models, no heavy queries at request time.
---
## 5. Testing strategy
### Approach: TDD
Write tests **before** implementation code. Each implementation step in §9 is preceded by a dedicated test-writing step. Tests must compile and fail (red) before any production code is written.

### .NET test projects
Each service has one xUnit test project in `tests/`. All test projects reference:
- `xunit` + `xunit.runner.visualstudio`
- `FluentAssertions`
- `NSubstitute` (mocking)
- `Bogus` — generates realistic fake users / boards / tasks; avoid hardcoded fixtures in integration tests
- `NetArchTest.Rules` — assert the §2 onion dependency rule in a build-failing fixture
- `Microsoft.AspNetCore.Mvc.Testing` (integration)
- `Testcontainers.PostgreSql` + `Testcontainers.Redis` + `Testcontainers.RabbitMq` (real infra via Docker)
- `Mediator` (`martinothamar/Mediator`) handlers are plain classes — no special test package needed, inject `IMediator` or call handlers directly

#### Unit tests — naming convention
`{Class}_{Method}_{Scenario}_{ExpectedResult}`  
Example: `AuthService_Login_WithInvalidPassword_ReturnsUnauthorized`

#### Integration tests
Use `WebApplicationFactory<Program>` with a real Testcontainers database spun up per test class (`IAsyncLifetime`). No mocking of infrastructure — test against real postgres/redis/rabbitmq containers.

#### Architecture tests
Each service test project includes one fixture using **NetArchTest.Rules** asserting the onion dependency rule from §2:
- `Domain` references no external NuGet packages
- `Application` does not reference `Infrastructure`
- `Infrastructure` is not referenced by `Domain` or `Application`

These tests fail the build if a layer leak is introduced — the only practical guard against gradual erosion of the architecture.

#### What to test per service
| Service       | Unit tests                                      | Integration tests                                   |
|---------------|-------------------------------------------------|-----------------------------------------------------|
| Identity      | Token generation logic, password validation     | All 8 endpoints, refresh token rotation             |
| Tasks         | All MediatR command/query handlers              | All 22 endpoints, authorization rules, event publishing |
| Notifications | Event→notification mapping, preference logic    | SignalR hub connection, Redis read/write, email sending (Mailhog) |
| Analytics     | Event projection logic                          | All 4 endpoints, read model accuracy               |
| Gateway       | JWT extraction, header forwarding logic         | Route forwarding, rate limiting, CORS               |

### Angular tests
- **Unit tests:** Jest (via `jest-preset-angular`), co-located `*.spec.ts` files
- **E2E tests:** Playwright in `tests/TaskManager.E2E.Tests/` — covers all Definition of Done scenarios in §10
- Every store, service, component, interceptor, and pipe must have a spec file created before the implementation file

### CI rule
`dotnet test` and `ng test --watch=false` must pass with zero failures before any feature is considered done. Tests are not optional cleanup — they are the starting point.

---
## 6. Shared contracts — `TaskManager.Contracts`
A plain .NET class library, no NuGet publishing needed. Referenced by `Tasks`, `Notifications`, and `Analytics` projects directly via `<ProjectReference>`.
Contains only:
- Event record types (all listed in §4.3)
- No domain logic, no EF models, no HTTP models

### Note on free libraries
This project uses only **free, open-source (MIT)** libraries:
| Library | Replaces | Purpose |
|---|---|---|
| **Mapperly** (`riok/mapperly`) | AutoMapper | Source-generator DTO mapping, zero runtime overhead |
| **Mediator** (`martinothamar/Mediator`) | MediatR | Source-generator CQRS dispatch, near-identical API |
| **FluentResults** (`altmann/FluentResults`) | Exception-driven flow | `Result<T>` for expected domain failures |
| **FluentValidation** | Data Annotations | Request validation in Application/Behaviors |
---
## 7. Frontend specification — Angular 18
**Technology:** Angular 18 · standalone components · NgRx Signals (`signalStore`) · Angular Material 3 · TailwindCSS · RxJS
### Module structure
```
src/app/
├── core/
│   ├── auth/              # AuthStore (signal store), AuthGuard, AuthInterceptor
│   ├── http/              # typed API services per microservice
│   └── notifications/     # SignalR service, NotificationStore
├── features/
│   ├── auth/              # login, register pages (standalone routed components)
│   ├── boards/            # board list, board detail (Kanban), board settings
│   ├── tasks/             # task detail dialog, task form
│   └── analytics/         # personal dashboard with charts (ngx-charts)
├── shared/
│   ├── components/        # avatar, badge, priority-chip, label-chip, empty-state
│   └── pipes/             # relative-time, truncate
└── app.routes.ts
```
### Implementation phasing
The auth shell — `core/auth/` (AuthStore, AuthGuard, AuthInterceptor, RefreshInterceptor), `core/http/api-base.ts`, and the `features/auth/` login + register components — is built **in Step 2b alongside the Identity service**, not deferred to Step 7. This way each backend service has a usable browser entry point as it lands. Step 7 fills in the remaining `features/` (boards, tasks, analytics) and `shared/components/`.

### Routes
| Path                   | Component              | Guard      |
|------------------------|------------------------|------------|
| `/login`               | `LoginComponent`       | —          |
| `/register`            | `RegisterComponent`    | —          |
| `/boards`              | `BoardListComponent`   | `AuthGuard`|
| `/boards/:id`          | `BoardDetailComponent` | `AuthGuard`|
| `/analytics`           | `AnalyticsDashboard`   | `AuthGuard`|
| `**`                   | redirect to `/boards`  | —          |
### AuthStore (NgRx Signals)
```typescript
interface AuthState {
  user: UserDto | null;
  accessToken: string | null;
  isLoading: boolean;
  error: string | null;
}
// Actions: login, register, logout, refreshToken, loadProfile
// accessToken stored in memory only (not localStorage)
// refreshToken stored in httpOnly cookie (set by backend)
```
### Board detail (Kanban) component
- Four columns: `Todo`, `In Progress`, `Review`, `Done`
- Each task card shows: title, assignee avatar, priority chip, label chips, due date (red if overdue)
- Drag-and-drop reordering via Angular CDK `DragDropModule`
- On drop: optimistic UI update → call `POST /api/tasks/{id}/move` → rollback on error
### Notification bell
- Bell icon in top nav with unread count badge
- Connects to SignalR hub on app init (after login)
- Dropdown shows last 10 notifications
- Clicking a notification navigates to the related task
### HTTP interceptors
- `AuthInterceptor`: attaches `Authorization: Bearer <token>` to all requests except `/api/auth/**`
- `RefreshInterceptor`: on 401 response, attempts token refresh once, retries request, on second failure logs out. **Concurrent 401 handling:** only one refresh call may be in flight at a time. The interceptor holds a module-level `refreshInFlight$: ReplaySubject<string> | null`. The first 401 sets it to a new subject and calls `/api/auth/refresh`; subsequent 401s while it's non-null subscribe to the same subject and retry once the token arrives. On refresh failure all queued requests propagate the error and the store logs out. This avoids N concurrent 401s triggering N refresh calls and walking the user's refresh-token rotation chain.
- `ErrorInterceptor`: catches unhandled HTTP errors, maps to user-facing messages, logs to console in dev

### TypeScript & compiler configuration
```json
// tsconfig.json
{
  "compilerOptions": {
    "strict": true,
    "strictTemplates": true,
    "noImplicitOverride": true,
    "noPropertyAccessFromIndexSignature": true,
    "forceConsistentCasingInFileNames": true
  }
}
```

### Component conventions
Every component follows these rules without exception:
```typescript
// ✅ correct
@Component({
  selector: 'tm-task-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,   // always OnPush
  template: `...`
})
export class TaskCardComponent {
  // inject() — never constructor injection
  private readonly tasksStore = inject(TasksStore);

  // Signal inputs/outputs (Angular 17+)
  readonly task = input.required<TaskDto>();
  readonly priorityChange = output<TaskPriority>();

  // Derived state with computed()
  readonly isOverdue = computed(() =>
    this.task().dueDate != null && new Date(this.task().dueDate!) < new Date()
  );
}
```

### Smart / dumb component pattern
```typescript
// SMART — lives in features/, connects to store
@Component({ ... changeDetection: ChangeDetectionStrategy.OnPush })
export class BoardDetailComponent {
  private readonly boardsStore = inject(BoardsStore);
  readonly board = this.boardsStore.currentBoard;   // signal from store
  // no @Input for domain data — gets data from store
}

// DUMB — lives in shared/components/, pure presentational
@Component({ ... changeDetection: ChangeDetectionStrategy.OnPush })
export class TaskCardComponent {
  readonly task = input.required<TaskDto>();           // all data via inputs
  readonly moved = output<MoveTaskRequest>();          // all events via outputs
  // no store injection — independently testable
}
```

### Lazy routing
```typescript
// app.routes.ts — all feature components lazy-loaded
export const routes: Routes = [
  { path: 'login',    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'boards',   loadComponent: () => import('./features/boards/board-list.component').then(m => m.BoardListComponent), canActivate: [authGuard] },
  { path: 'boards/:id', loadComponent: () => import('./features/boards/board-detail.component').then(m => m.BoardDetailComponent), canActivate: [authGuard] },
  { path: 'analytics', loadComponent: () => import('./features/analytics/analytics-dashboard.component').then(m => m.AnalyticsDashboardComponent), canActivate: [authGuard] },
  { path: '**', redirectTo: 'boards' }
];
```

### Barrel files
Each feature and shared folder exports via `index.ts`:
```typescript
// features/boards/index.ts
export { BoardListComponent } from './board-list.component';
export { BoardDetailComponent } from './board-detail.component';
```

### ESLint + Prettier
```json
// .eslintrc.json — key rules
{
  "rules": {
    "@angular-eslint/prefer-on-push-component-change-detection": "error",
    "@angular-eslint/no-empty-lifecycle-method": "error",
    "@typescript-eslint/no-explicit-any": "error",
    "@typescript-eslint/explicit-function-return-type": "warn"
  }
}
```
CI (`ng lint`) must pass with zero errors.

### Environment configuration
```typescript
// environment.ts                    (local dev)
export const environment = {
  apiUrl: 'http://localhost:5000',
  production: false,
  environment: 'local',
};

// environment.staging.ts            (Vercel preview / develop branch)
export const environment = {
  apiUrl: 'https://api-staging.<your-domain>',  // gateway URL on Fly.io staging
  production: false,
  environment: 'staging',
};

// environment.prod.ts               (Vercel production / main branch)
export const environment = {
  apiUrl: 'https://api.<your-domain>',
  production: true,
  environment: 'production',
};
```
`angular.json` declares matching `configurations` blocks (`staging`, `production`) with `fileReplacements` so `--configuration staging` and `--configuration production` swap the right file at build time. The CI workflows in §10 invoke these.
---
## 8. Cross-cutting concerns
### Logging
All services use Serilog, sink to Seq (`http://seq:5341`). Log format includes `ServiceName`, `TraceId`, `UserId`. Minimum level: `Information` in production, `Debug` in development.

**Conventions** — always use Serilog message templates: `_logger.LogInformation("Board {BoardId} created by {UserId}", boardId, userId)`. Never string-interpolate into the log call (e.g. `LogInformation($"Board {boardId} ...")`), as that defeats structured search in Seq. Enrich per-request context with `LogContext.PushProperty("CorrelationId", id)` inside middleware so every downstream log carries it without parameter plumbing.
### Correlation IDs
Gateway generates `X-Correlation-Id` (UUID) on each request and forwards it downstream. Each service logs it and passes it in messages published to RabbitMQ.
### Health checks
Each service registers:
- Self: `AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy())`
- Dependencies: postgres, redis, or rabbitmq as appropriate
- Endpoint: `GET /health` (returns 200 with JSON detail)

**NuGet packages** — `AspNetCore.HealthChecks.NpgSql` (Postgres dependency check), `AspNetCore.HealthChecks.Redis` (Notifications + any service caching), `AspNetCore.HealthChecks.Rabbitmq` (Tasks + Notifications + Analytics — anything publishing or consuming). Register via `services.AddHealthChecks().AddNpgSql(connectionString).AddRedis(...).AddRabbitMQ(...)`.
### Error handling
Each service has a global `ExceptionHandlingMiddleware` that catches unhandled exceptions and returns `ProblemDetails` JSON. Never expose stack traces in production.
### Database migrations
Run on startup via `dbContext.Database.MigrateAsync()` in `Program.cs`. Each service owns its migration history.

### Date and time
All date/time values are `DateTimeOffset` end-to-end — entity properties, DTOs, API request/response shapes, integration-event records. Stored in PostgreSQL as `timestamptz`. The server is authoritative for "now": services use `DateTimeOffset.UtcNow` (or an injected `IClock` in handlers that need to be test-time-shifted). The Angular SPA receives ISO-8601 strings with offsets and converts to the user's local time **only at display**, never at storage or comparison. Never use `DateTime` in new code; if a third-party API forces `DateTime`, convert at the boundary.

### Secrets
All sensitive configuration — `JWT_SECRET`, database connection strings, SMTP credentials, RabbitMQ credentials, Redis URL — comes from environment variables, never from committed config files. `appsettings.json` contains only non-secret defaults; `.env` files are gitignored. In CI/CD the secrets flow from GitHub Actions Secrets → Fly.io secrets (`flyctl secrets set`) and Vercel environment variables. If a secret is ever committed by accident, rotate it before reverting.

**Standard environment variable names** (consistent across local, staging, production):
| Variable | Used by | Notes |
|---|---|---|
| `JWT_SECRET` | All services | HS256 signing key, ≥ 64 bytes random |
| `IDENTITY_DB_CONNECTION` | Identity | Postgres connection string for `identity_db` |
| `TASKS_DB_CONNECTION` | Tasks | Postgres connection string for `tasks_db` |
| `ANALYTICS_DB_CONNECTION` | Analytics | Postgres connection string for `analytics_db` |
| `REDIS_URL` | Notifications | StackExchange.Redis configuration string |
| `RABBITMQ_URL` | Tasks, Notifications, Analytics | AMQP URI (e.g. `amqp://guest:guest@rabbitmq:5672`) |
| `SMTP_HOST` / `SMTP_PORT` / `SMTP_USER` / `SMTP_PASS` | Notifications | MailKit settings; locally points at Mailhog (`mailhog:1025`, no auth) |
| `SEQ_URL` | All services | Serilog sink (`http://seq:5341` locally) |

### Service network isolation
In production, only the gateway is internet-facing. The four downstream services (`identity`, `tasks`, `notifications`, `analytics`) are reachable **only via the gateway** — on Fly.io that means private 6PN networking with no public ports allocated. This matters because services trust the `X-User-Id` and `X-User-Email` headers the gateway sets from validated JWT claims; if a service were directly reachable, those headers could be spoofed by any caller. Local dev exposes service ports for convenience — production must not.
---
## 9. Development setup
### Prerequisites
- .NET 10 SDK
- Node.js 22 + npm
- Docker Desktop
### Start everything
```bash
docker compose up -d          # starts infra (postgres, redis, rabbitmq, seq, mailhog)
cd src/gateway && dotnet run
cd src/services/identity && dotnet run
cd src/services/tasks && dotnet run
cd src/services/notifications && dotnet run
cd src/services/analytics && dotnet run
cd frontend/task-manager-app && ng serve
```
Or with Docker:
```bash
docker compose --profile full up --build
```
### Useful local URLs
| Service           | URL                            |
|-------------------|--------------------------------|
| Angular app       | http://localhost:4200          |
| API gateway       | http://localhost:5000          |
| RabbitMQ UI       | http://localhost:15672 (guest/guest) |
| Seq logs          | http://localhost:5341          |
| Mailhog UI        | http://localhost:8025          |
---
## 10. Version control & CI/CD
### Branching strategy — Gitflow
```
main        ──────────────────────────────────────────►  (production, tagged)
               ↑  merge + tag v0.1        ↑  merge + tag v1.0
hotfix/*  ──╮  │                          │
             ╰──┤                          │
develop     ────┼──────────────────────────┼──────────►  (integration)
              ↑ ↑ ↑                      ↑ ↑
release/*  ───╯ │ │                      ╯ │
feature/*  ─────╯ ╰──────────────────────╯
```

| Branch | Branched from | Merges into | Purpose |
|---|---|---|---|
| `main` | — | — | Production only; every commit is a tagged release |
| `develop` | `main` | — | Integration branch; always contains latest delivered work |
| `feature/<name>` | `develop` | `develop` | One branch per feature/fix; merged via PR |
| `release/<version>` | `develop` | `main` + `develop` | Release stabilisation; only bugfixes allowed |
| `hotfix/<name>` | `main` | `main` + `develop` | Urgent production fixes |

**Rules:**
- `main` and `develop` are **protected** — no direct pushes, require PR + passing CI
- Feature branches are short-lived and deleted after merge
- Release branches are tagged on merge to `main` (e.g. `v1.0.0`) following **SemVer**
- Commit messages follow **Conventional Commits**: `feat:`, `fix:`, `test:`, `chore:`, `docs:`

### Workflow files
```
.github/
└── workflows/
    ├── ci.yml            # lint + test — all feature/release/hotfix branches and PRs
    ├── cd-staging.yml    # build images + deploy to staging — on push to develop
    └── cd-production.yml # build images + deploy to production — on push to main
```

### Container registry — GHCR
All Docker images pushed to **GitHub Container Registry** (`ghcr.io/<owner>/smart-task-manager-<service>`).  
Auth uses the built-in `GITHUB_TOKEN` — no extra credentials needed.  
Tags: `:latest` (develop → staging), `:<semver>` e.g. `:1.0.0` (main → production).

---

### `ci.yml` — lint + test on feature, release, hotfix branches and PRs

```yaml
name: CI
on:
  push:
    branches:
      - 'feature/**'
      - 'release/**'
      - 'hotfix/**'
      - develop
  pull_request:
    branches:
      - develop
      - main

jobs:
  test-dotnet:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        project:
          - tests/TaskManager.Identity.Tests
          - tests/TaskManager.Tasks.Tests
          - tests/TaskManager.Notifications.Tests
          - tests/TaskManager.Analytics.Tests
          - tests/TaskManager.Gateway.Tests
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test ${{ matrix.project }} --configuration Release --logger "github"

  test-angular:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: frontend/task-manager-app/package-lock.json
      - run: cd frontend/task-manager-app && npm ci
      - run: cd frontend/task-manager-app && npm run lint
      - run: cd frontend/task-manager-app && npm run test:ci
      - run: cd frontend/task-manager-app && npm run build -- --configuration production
```

---

### `cd-staging.yml` — deploy to staging on push to `develop`

```yaml
name: CD Staging
on:
  push:
    branches: [develop]

env:
  IMAGE_PREFIX: ghcr.io/${{ github.repository_owner }}/smart-task-manager

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    strategy:
      matrix:
        service:
          - { name: gateway,       context: src/gateway/TaskManager.Gateway }
          - { name: identity,      context: src/services/identity/TaskManager.Identity }
          - { name: tasks,         context: src/services/tasks/TaskManager.Tasks }
          - { name: notifications, context: src/services/notifications/TaskManager.Notifications }
          - { name: analytics,     context: src/services/analytics/TaskManager.Analytics }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - uses: docker/build-push-action@v5
        with:
          context: ${{ matrix.service.context }}
          push: true
          tags: |
            ${{ env.IMAGE_PREFIX }}-${{ matrix.service.name }}:develop
            ${{ env.IMAGE_PREFIX }}-${{ matrix.service.name }}:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy-staging-frontend:
    runs-on: ubuntu-latest
    needs: build-and-push
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: frontend/task-manager-app/package-lock.json
      - run: cd frontend/task-manager-app && npm ci && npm run build -- --configuration staging
      - uses: vercel/action@v1
        with:
          vercel-token: ${{ secrets.VERCEL_TOKEN }}
          vercel-project-id: ${{ secrets.VERCEL_PROJECT_ID }}
          vercel-org-id: ${{ secrets.VERCEL_ORG_ID }}
          working-directory: frontend/task-manager-app/dist/task-manager-app
          # no --prod flag → Vercel staging preview URL

  deploy-staging-backend:
    runs-on: ubuntu-latest
    needs: build-and-push
    strategy:
      matrix:
        app:
          - smart-task-manager-gateway-staging
          - smart-task-manager-identity-staging
          - smart-task-manager-tasks-staging
          - smart-task-manager-notifications-staging
          - smart-task-manager-analytics-staging
    steps:
      - uses: superfly/flyctl-actions/setup-flyctl@master
      - run: flyctl deploy --app ${{ matrix.app }} --remote-only
        env:
          FLY_API_TOKEN: ${{ secrets.FLY_API_TOKEN }}
```

---

### `cd-production.yml` — deploy to production on push to `main` (after release merge)

```yaml
name: CD Production
on:
  push:
    branches: [main]

env:
  IMAGE_PREFIX: ghcr.io/${{ github.repository_owner }}/smart-task-manager

jobs:
  get-version:
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.tag.outputs.version }}
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - id: tag
        run: echo "version=$(git describe --tags --abbrev=0)" >> $GITHUB_OUTPUT

  build-and-push:
    runs-on: ubuntu-latest
    needs: get-version
    permissions:
      contents: read
      packages: write
    strategy:
      matrix:
        service:
          - { name: gateway,       context: src/gateway/TaskManager.Gateway }
          - { name: identity,      context: src/services/identity/TaskManager.Identity }
          - { name: tasks,         context: src/services/tasks/TaskManager.Tasks }
          - { name: notifications, context: src/services/notifications/TaskManager.Notifications }
          - { name: analytics,     context: src/services/analytics/TaskManager.Analytics }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - uses: docker/build-push-action@v5
        with:
          context: ${{ matrix.service.context }}
          push: true
          tags: |
            ${{ env.IMAGE_PREFIX }}-${{ matrix.service.name }}:latest
            ${{ env.IMAGE_PREFIX }}-${{ matrix.service.name }}:${{ needs.get-version.outputs.version }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy-production-frontend:
    runs-on: ubuntu-latest
    needs: build-and-push
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: frontend/task-manager-app/package-lock.json
      - run: cd frontend/task-manager-app && npm ci && npm run build -- --configuration production
      - uses: vercel/action@v1
        with:
          vercel-token: ${{ secrets.VERCEL_TOKEN }}
          vercel-project-id: ${{ secrets.VERCEL_PROJECT_ID }}
          vercel-org-id: ${{ secrets.VERCEL_ORG_ID }}
          working-directory: frontend/task-manager-app/dist/task-manager-app
          vercel-args: '--prod'        # promotes to production URL

  deploy-production-backend:
    runs-on: ubuntu-latest
    needs: build-and-push
    strategy:
      matrix:
        app:
          - smart-task-manager-gateway
          - smart-task-manager-identity
          - smart-task-manager-tasks
          - smart-task-manager-notifications
          - smart-task-manager-analytics
    steps:
      - uses: superfly/flyctl-actions/setup-flyctl@master
      - run: flyctl deploy --app ${{ matrix.app }} --remote-only
        env:
          FLY_API_TOKEN: ${{ secrets.FLY_API_TOKEN }}
```

---

### Release process (Gitflow)
```bash
# 1. Cut a release branch from develop
git checkout develop && git pull
git checkout -b release/1.0.0

# 2. Bump version in csproj / package.json, fix any last bugs
# 3. Open PR: release/1.0.0 → main (CI runs automatically)
# 4. Merge PR → main, then tag
git checkout main && git pull
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0         # triggers cd-production.yml

# 5. Back-merge to develop
git checkout develop
git merge main
git push origin develop
```

Hotfix follows the same pattern but branches from `main` and merges into both `main` and `develop`.

### PR preview environments
Vercel automatically creates a **preview URL** for every PR to `develop`  
(`https://smart-task-manager-<hash>.vercel.app`) without the `--prod` flag.

### Deployment targets (all free tiers)
| Component | Platform | Staging | Production |
|---|---|---|---|
| Angular SPA | **Vercel** | Preview URL per PR / develop push | `--prod` flag on main merge |
| .NET services | **Fly.io** | `-staging` apps | main apps |
| PostgreSQL | **Fly.io Postgres** | Shared staging DB | Separate prod DB |
| Redis | **Upstash Redis** | Free 10 000 req/day | Free 10 000 req/day |
| RabbitMQ | **CloudAMQP** | Free `lemur` plan | Free `lemur` plan |
| Logs | Local: Seq | Structured JSON stdout | Structured JSON stdout |

> **Note:** Running all 5 services on Fly.io's free tier simultaneously is tight. For a demo/portfolio project, deploy only what you need to show. `docker compose up` remains the primary development target.

### Deployment compatibility — refresh cookie
The refresh-token cookie (§4.2) is set with `SameSite=Strict`. That value is **incompatible with cross-site requests** between the SPA and the API. "Cross-site" here means different registrable domains (eTLD+1), e.g. `task-manager.vercel.app` and `task-manager-api.fly.dev`. Local dev is fine because the gateway proxies everything under one origin (`http://localhost:5000`), and the SPA dev server lives on `localhost:4200` — both `localhost`, same registrable domain.

When you deploy SPA + API to real public hosts, pick one of these before going live:

| Option | What it costs | Trade-off |
|---|---|---|
| **A. Same eTLD+1** | A custom domain (~$12/yr). Point `app.yourdomain.com` at Vercel and `api.yourdomain.com` at the Fly.io gateway. | Cookie keeps `SameSite=Strict`. **Recommended** — preserves the security pass design verbatim. |
| **B. Relax cookie to `SameSite=None; Secure` + add CSRF protection** | One extra endpoint (`GET /api/csrf-token`), double-submit-cookie check on `/api/auth/refresh`. | Restores CSRF protection that `Strict` was providing; spec gets a little more complex. |
| **C. Drop the cookie entirely** | Move refresh token into JS memory like the access token. | No CSRF surface at all, but user is logged out every browser session — no "stay logged in". |

**v1 ships with the current `SameSite=Strict` design.** This subsection exists so the next reviewer knows the constraint and doesn't ship a broken refresh flow on first deploy.

### Required GitHub repository secrets
| Secret | Used by | Value source |
|---|---|---|
| `JWT_SECRET` | All services | Random 64-byte hex string |
| `FLY_API_TOKEN` | cd-staging, cd-production | `flyctl auth token` |
| `VERCEL_TOKEN` | cd-staging, cd-production | Vercel → Settings → Tokens |
| `VERCEL_PROJECT_ID` | cd-staging, cd-production | `.vercel/project.json` after `vercel link` |
| `VERCEL_ORG_ID` | cd-staging, cd-production | `.vercel/project.json` |

### Branch protection rules (GitHub Settings → Branches)
| Branch | Rules |
|---|---|
| `main` | Require PR, require CI pass, no direct push, require linear history |
| `develop` | Require PR, require CI pass, no direct push |

### Dockerfile conventions
Each .NET service uses a **multi-stage Dockerfile**:
```dockerfile
# Stage 1 — build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/services/identity/TaskManager.Identity/TaskManager.Identity.csproj", "TaskManager.Identity/"]
COPY ["src/shared/TaskManager.Contracts/TaskManager.Contracts.csproj", "TaskManager.Contracts/"]
RUN dotnet restore "TaskManager.Identity/TaskManager.Identity.csproj"
COPY . .
RUN dotnet publish "TaskManager.Identity/TaskManager.Identity.csproj" -c Release -o /app/publish

# Stage 2 — runtime (minimal image)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskManager.Identity.dll"]
```
- Use `aspnet:10.0` (runtime only) for the final image — not `sdk`
- Run as non-root user: `USER app` before `ENTRYPOINT`
- `.dockerignore` excludes `bin/`, `obj/`, `tests/`, `.git/`

---
## 11. Claude Code prompts — implementation order
Use these prompts in sequence. Each feature step is preceded by a test step. **Do not write production code until its test step is complete and all tests are red.**

Every service test step below also adds a `NetArchTest.Rules` architecture fixture asserting the §2 onion dependency rule for that service (Domain → no NuGet, Application → no Infrastructure, Infrastructure → not referenced inward). This is not repeated in each step; treat it as a standing requirement.

### Step 1 — Scaffold solution + infra
```
Using the spec in smart-task-manager-spec.md, scaffold the full .NET solution:
- Create SmartTaskManager.sln
- Create all 5 .NET project folders with .csproj files and correct package references
  (Tasks service includes: Mediator (martinothamar/Mediator); all services include: Serilog, Mapperly)
- Create all 6 test projects in tests/ with .csproj files and correct package references
  (xunit, FluentAssertions, NSubstitute, Testcontainers, Microsoft.AspNetCore.Mvc.Testing)
- Add all test projects to SmartTaskManager.sln
- Create TaskManager.Contracts with all event record types from §4.3
- Create docker-compose.yml with all services from §3 including health checks
- Create docker-compose.override.yml for local dev (Angular dev server, debug ports)
- Create `.github/workflows/{ci,cd-staging,cd-production}.yml` exactly as shown in §10. Branch protection on `main`/`develop` (§10) requires CI to exist before a PR can be merged, so these land in Step 1 even though there is nothing to test yet — they will run as services come online in later steps.
```

### Step 2a — Identity service tests (write first, expect red)
```
Write all tests for TaskManager.Identity in TaskManager.Identity.Tests per §4.2 and §5.
Do NOT implement any production code yet — tests must compile but fail.

Unit tests (NSubstitute mocks, no DB):
- TokenService: access token claims, 15-min expiry, HS256 signing
- TokenService: refresh token rotation — new pair issued, old token revoked
- Validation: RegisterRequest rules (email format, password complexity, display name length)

Integration tests (Testcontainers PostgreSQL, WebApplicationFactory):
- POST /api/auth/register — happy path returns 200 with AuthResponse
- POST /api/auth/register — duplicate email returns 409
- POST /api/auth/register — invalid password returns 400 with ProblemDetails
- POST /api/auth/login — correct credentials returns token pair
- POST /api/auth/login — wrong password returns 401
- POST /api/auth/refresh — valid token returns new pair
- POST /api/auth/refresh — revoked token returns 401
- POST /api/auth/logout — revokes refresh token
- GET /api/users/me — returns profile for authenticated user
- PUT /api/users/me — updates display name
- GET /api/users/{id} — returns public profile
- GET /api/users/search?q= — returns matching users
```

### Step 2b — Identity service implementation
```
Implement TaskManager.Identity per §4.2 until all tests from Step 2a are green.
Follow Onion Architecture from §2 — no layer may violate the dependency rule.

Domain layer (no external NuGet):
- AppUser and RefreshToken rich entities (private setters, Create() factory, behavior methods)
- IUserRepository, IRefreshTokenRepository, IUnitOfWork interfaces

Application layer:
- Commands: RegisterCommand, LoginCommand, RefreshTokenCommand, LogoutCommand + handlers returning Result<T>
- Queries: GetCurrentUserQuery, GetUserByIdQuery, SearchUsersQuery + handlers
- DTOs mapped with Mapperly (IBoardMapper pattern)
- ValidationBehavior wired to FluentValidation validators for all request DTOs
- LoggingBehavior for all handlers

Infrastructure layer:
- IdentityDbContext implementing IUnitOfWork
- Repository implementations for IUserRepository, IRefreshTokenRepository
- TokenService implementing ITokenService (JWT + refresh token generation)
- EF Core migrations

Presentation layer:
- AuthEndpoints and UserEndpoints as Minimal API RouteGroupBuilder extensions
- Each endpoint: extract X-User-Id header → dispatch via IMediator → call ToHttpResult()
- ExceptionHandlingMiddleware returning ProblemDetails
- Serilog + health checks per §8

Angular auth shell (concurrently — see §7 *Implementation phasing*):
- Scaffold the Angular 18 project (`ng new`), Angular Material 3, Tailwind, ESLint + Prettier, Jest via jest-preset-angular
- Build `core/auth/` (AuthStore signal store, AuthGuard, AuthInterceptor, RefreshInterceptor — including the concurrent-401 queue pattern from §7), `core/http/api-base.ts`, `core/notifications/` is stubbed but inactive
- Build `features/auth/` (LoginComponent, RegisterComponent — smart components, reactive forms)
- Empty placeholder `BoardListComponent` behind `AuthGuard` so the post-login redirect target exists
- Acceptance: register → login → see placeholder boards page works end-to-end in a browser against the running Identity service via the gateway
```

### Step 3a — Tasks service tests (write first, expect red)
```
Write all tests for TaskManager.Tasks in TaskManager.Tasks.Tests per §4.3 and §5.
Do NOT implement production code yet.

Unit tests (NSubstitute, no DB):
- Each Mediator command handler: happy path + validation failure + authorization failure
- Each Mediator query handler: returns correct projection
- DeadlineApproachingEvent background service: publishes event for tasks due within 24 h

Integration tests (Testcontainers PostgreSQL + RabbitMQ, WebApplicationFactory):
- All 22 endpoints: happy path, 404, and authorization boundary (Owner/Editor/Viewer rules)
- POST /api/tasks/{id}/move — verifies new status + position, publishes TaskStatusChangedEvent
- POST /api/tasks/{id}/assign — publishes TaskAssignedEvent
- POST /api/tasks/{id}/comments — publishes TaskCommentAddedEvent
- DELETE /api/boards/{id} — only Owner succeeds; Editor gets 403
- **Optimistic concurrency**: PUT /api/tasks/{id} with a stale `If-Match` header returns 409, body contains the current task (with updated RowVersion). Two parallel PUTs — second one gets 409 deterministically.
- **Pagination cap**: seed 201 tasks on a board; GET /api/tasks?boardId=… returns 200 results, `X-Result-Truncated: true` header set.
```

### Step 3b — Tasks service implementation
```
Implement TaskManager.Tasks per §4.3 until all tests from Step 3a are green.
Follow Onion Architecture from §2.

Domain layer:
- Value objects: TaskStatus, TaskPriority, BoardRole enums; Color record with validation
- Rich entities: Board, TaskItem, BoardMember, Label, TaskComment (all per §4.3 domain model spec)
- TaskItem has a `byte[] RowVersion` concurrency token; PUT / move / assign / edit-comment endpoints require `If-Match` header. `DbUpdateConcurrencyException` from `SaveChangesAsync` maps to 409 Conflict with current task body in the response. See §4.3 *Optimistic concurrency on TaskItem*.
- Repository interfaces: IBoardRepository, ITaskRepository, IUnitOfWork

Application layer:
- All commands + handlers returning Result<T>, depending on repository interfaces only
- All queries + handlers returning Result<T>
- `GetTasksQuery` handler enforces the 200-result cap (§4.3 *Pagination policy*) and signals truncation via `Result<(IReadOnlyList<TaskDto>, bool truncated)>`; the endpoint sets `X-Result-Truncated: true` on the response when truncated
- ValidationBehavior + LoggingBehavior pipeline behaviors
- IEventPublisher interface (MassTransit implemented in Infrastructure)

Infrastructure layer:
- TasksDbContext (implements IUnitOfWork), EF entity configs, migrations
- Map Color as an EF Core owned entity of Label (single column labels.color)
- Repository implementations
- MassTransit publisher for all 6 event types per §4.3
- Enable MassTransit EF Core outbox on TasksDbContext so events are persisted in the same transaction as aggregate changes (no events lost on crash)

Presentation layer:
- BoardEndpoints, TaskEndpoints as Minimal API groups
- Authorization checks (read X-User-Id header, verify board membership via query)
- Background IHostedService for DeadlineApproachingEvent (runs every hour)
- Serilog + health checks per §8
```

### Step 4a — Notifications service tests (write first, expect red)
```
Write all tests for TaskManager.Notifications in TaskManager.Notifications.Tests per §4.4 and §5.
Do NOT implement production code yet.

Unit tests:
- EventMapper: each event type maps to the correct NotificationDto (type, title template, recipient)
- PreferencesService: default values applied when no Redis key exists

Integration tests (Testcontainers Redis + RabbitMQ, WebApplicationFactory):
- Consuming TaskAssignedEvent stores notification in Redis and broadcasts via SignalR
- Consuming DeadlineApproachingEvent stores notification and sends email to Mailhog SMTP
- GET /api/notifications — returns last 50 for authenticated user
- POST /api/notifications/{id}/read — marks notification read
- POST /api/notifications/read-all — marks all read
- GET /api/notifications/preferences — returns defaults for new user
- PUT /api/notifications/preferences — persists to Redis
```

### Step 4b — Notifications service implementation
```
Implement TaskManager.Notifications per §4.4 until all tests from Step 4a are green:
- MassTransit consumers for all events
- SignalR NotificationsHub
- Redis notification history (last 50, 30-day TTL)
- Email sending with MailKit
- REST endpoints for history and preferences
- Serilog + health checks per §8
```

### Step 5a — Analytics service tests (write first, expect red)
```
Write all tests for TaskManager.Analytics in TaskManager.Analytics.Tests per §4.5 and §5.
Do NOT implement production code yet.

Unit tests:
- Each event consumer projects to the correct read model fields

Integration tests (Testcontainers PostgreSQL + RabbitMQ, WebApplicationFactory):
- Consuming TaskCreatedEvent increments BoardStats.TotalTasks and UserStats.TasksCreated
- Consuming TaskCompletedEvent increments BoardStats.CompletedTasks and UserStats.TasksCompleted
- GET /api/analytics/boards/{id}/summary — returns correct counts
- GET /api/analytics/boards/{id}/completion-trend — returns 30-day series
- GET /api/analytics/users/me/summary — returns personal stats
- GET /api/analytics/users/me/activity — returns last 30 events in order
```

### Step 5b — Analytics service implementation
```
Implement TaskManager.Analytics per §4.5 until all tests from Step 5a are green:
- EF Core read models and DbContext
- MassTransit consumers projecting events to read models
- Enable MassTransit EF Core inbox on AnalyticsDbContext to deduplicate consumed messages by MessageId
- All 4 REST endpoints
- Serilog + health checks per §8
```

### Step 6a — Gateway tests (write first, expect red)
```
Write all tests for TaskManager.Gateway in TaskManager.Gateway.Tests per §4.1 and §5.
Do NOT implement production code yet.

Unit tests:
- JwtHeaderForwardingMiddleware: extracts sub → X-User-Id and email → X-User-Email from valid JWT
- JwtHeaderForwardingMiddleware: missing/invalid JWT on protected route returns 401

Integration tests (WebApplicationFactory with stubbed downstream):
- Unauthenticated request to /api/auth/** is forwarded (no 401)
- Unauthenticated request to /api/boards/** returns 401
- Valid JWT request to /api/boards/** is forwarded with X-User-Id header set
- Exceeding 100 req/min from same IP returns 429
- OPTIONS request from http://localhost:4200 returns correct CORS headers
```

### Step 6b — Gateway implementation
```
Implement TaskManager.Gateway per §4.1 until all tests from Step 6a are green:
- YARP route configuration for all 7 route prefixes
- JWT validation + X-User-Id / X-User-Email header forwarding
- Rate limiting (100 req/min per IP)
- CORS for localhost:4200
- Health check endpoint
```

### Step 7a — Angular unit tests for remaining features (write first, expect red)
```
The Angular project, auth shell (AuthStore, interceptors, login/register) already exist from
Step 2b. This step adds the remaining feature tests. Write Jest unit tests for every file
before implementing them. Tests must exist and fail before any logic is written.

Specs already written in Step 2b (do not re-create):
- AuthStore, AuthInterceptor, RefreshInterceptor (including concurrent-401 queue)

Create new spec files for:
- BoardsApiService: createBoard(), getBoards(), getBoard() call correct endpoints
- TasksApiService: createTask(), moveTask(), assignTask() call correct endpoints (and send If-Match header on mutations)
- NotificationStore: unread count computed from notifications list
- BoardListComponent: renders boards; empty state shown when none
- BoardDetailComponent: renders four columns; drag-drop calls moveTask(); 409 from moveTask() triggers refetch + toast
- TaskDetailComponent: form pre-populates from task; If-Match header carries current RowVersion
- NotificationBellComponent: badge shows unread count; dropdown lists last 10
- AnalyticsDashboardComponent: renders charts from analytics summary endpoints
```

### Step 7b — Angular implementation (remaining features)
```
Implement the remaining Angular features per §7 until all tests from Step 7a are green, then
verify manually in a browser. The auth shell (project setup, core/auth, core/http, features/auth,
ErrorInterceptor) is already in place from Step 2b. Enforce all conventions from §7.

Already done in Step 2b (do not re-implement):
- Project setup (Angular 18, strict, Material 3, Tailwind, ESLint, Jest)
- core/auth: AuthStore, AuthGuard, AuthInterceptor, RefreshInterceptor (with concurrent-401 queue)
- core/http/api-base.ts
- features/auth: LoginComponent, RegisterComponent

Core additions:
- core/notifications/: SignalR NotificationService (uses accessTokenFactory per §4.4 SignalR auth), NotificationStore
- core/http: BoardsApiService, TasksApiService (sends If-Match header on mutating calls), NotificationsApiService, AnalyticsApiService

Features (all lazy-loaded via loadComponent):
- boards/: BoardListComponent (smart), BoardDetailComponent (smart + CDK drag-drop, handles 409 by refetching + toasting)
- tasks/: TaskDetailComponent (smart dialog), TaskFormComponent
- analytics/: AnalyticsDashboardComponent (smart, ngx-charts)

Shared components (all dumb, OnPush, signal inputs/outputs):
- TaskCardComponent, AvatarComponent, PriorityChipComponent, LabelChipComponent
- NotificationBellComponent, EmptyStateComponent

All components: ChangeDetectionStrategy.OnPush, inject() only, signal inputs, trackBy on every list
```

### Step 8 — End-to-end tests
```
Implement Playwright E2E tests in tests/TaskManager.E2E.Tests/ covering every item in the
Definition of Done (§12). Tests run against the full stack started via docker compose.
Each test scenario must pass before v1 is declared done.
```
---
## 12. Definition of done (v1)
- [ ] User can register, login, and stay logged in across page refreshes
- [ ] User can create a board and invite another user by email
- [ ] Tasks can be created, edited, assigned, and moved between columns via drag-and-drop
- [ ] Assignee receives a real-time in-app notification when assigned a task
- [ ] Assignee receives an email when a deadline is within 24 hours
- [ ] Analytics dashboard shows personal task stats and completion trend chart
- [ ] `docker compose up` starts all infra; all 5 services start and pass health checks
- [ ] All services log to Seq with correlation IDs

---
## 13. v1.1 addenda

### 13.1 Labels & filtering UI (Feature 1)
The label backend (§4.3) gains its SPA surface:
- **Label manager dialog** on board detail — create/delete board labels; colors come
  from a fixed 12-swatch palette (no free-form color input; guarantees chip contrast).
- **Label picker** in the task dialog — attach/detach against the existing
  `POST/DELETE /api/tasks/{id}/labels/{labelId}` routes. These routes intentionally
  do not require `If-Match`: label membership is a set operation where last-write-wins
  is harmless. Note that label changes still bump the task's `RowVersion` (the domain
  touches `UpdatedAt`), so the task dialog tracks the freshest `RowVersion` returned
  by each toggle and uses it for a subsequent save.
- **Filter bar** on board detail: free-text (title match), label multi-select
  (OR within labels), assignee (`any | me | unassigned`), priority. Kinds compose
  with AND. Filtering is client-side (the §4.3 200-task cap makes it instant) and
  persists to query params (`?q=&labels=&assignee=&priority=`) so filtered views are
  shareable. Dragging while filtered maps the drop index onto the unfiltered column
  so hidden cards keep their relative order.

### 13.2 Subtasks / checklists (Feature 2)
`TaskItem` gains a `ChecklistItem` child collection (`Id, TaskItemId, Title 1–200, IsDone,
Position, CreatedAt`; rich domain model — private setters + factory + `SetDone`/`Rename`).
New `checklist_items` table, cascade-deleted with the task.

Endpoints under `/api/tasks` (each returns the full fresh `TaskDto`, like the label routes):
- `POST /api/tasks/{id}/checklist` — body `{ title }`; appends at end.
- `PUT /api/tasks/{id}/checklist/{itemId}` — body `{ title?, isDone? }`; carries the
  *desired* state (idempotent setter), not a blind toggle.
- `DELETE /api/tasks/{id}/checklist/{itemId}`.

**Documented concurrency exception** (alongside the `AppUser` setter exception in §5):
checklist mutations do **not** require `If-Match` and do **not** advance
`TaskItem.RowVersion` — the domain `AddChecklistItem`/`RemoveChecklistItem` and the item
`SetDone`/`Rename` deliberately leave `UpdatedAt` untouched. They are an independent child
collection where last-write-wins is harmless: two members toggling different items must
never 409 each other, which is exactly the collaborative use the feature exists for. An
integration test asserts the task's ETag (xmin) is unchanged across a checklist write. No
integration events are published (checklist changes have no analytics meaning). Reordering
is out of scope; `Position` is insertion order. Authorization mirrors other task mutations
(Owner/Editor required); the update handler authorizes *before* resolving the item so a
non-editor cannot probe item IDs.

**SPA:** the task dialog gains an inline editor (add, toggle, rename-on-blur, delete) that
mutates immediately and refetches the board on close; cards show a `done/total` progress
chip (green at 100%) when a checklist exists.
