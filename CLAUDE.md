# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## The spec is the source of truth

`smart-task-manager-spec.md` at the repo root is the single source of truth for the entire system: architecture, endpoints, entities, conventions, deployment, branching strategy. **Read it before making any non-trivial change.** Section numbers (§4.3, §11, etc.) referenced in this document point there.

The spec has gone through three refinement passes; the current state on `main` is the authoritative version. When the spec and existing code disagree, the spec wins unless the disagreement is intentional and documented in a PR.

## Common commands

```powershell
# Restore + build everything (run from repo root)
dotnet restore SmartTaskManager.sln
dotnet build SmartTaskManager.sln --no-restore

# Run a single service locally (use after `docker compose up -d` for infra)
dotnet run --project src/services/identity/TaskManager.Identity
dotnet run --project src/services/tasks/TaskManager.Tasks
dotnet run --project src/services/notifications/TaskManager.Notifications
dotnet run --project src/services/analytics/TaskManager.Analytics
dotnet run --project src/gateway/TaskManager.Gateway

# Test — one project
dotnet test tests/TaskManager.Identity.Tests
# Test — one test by fully-qualified name (or substring filter)
dotnet test tests/TaskManager.Identity.Tests --filter "FullyQualifiedName~AuthService_Login_WithInvalidPassword"

# Start infrastructure (postgres / redis / rabbitmq / seq / mailhog)
docker compose up -d postgres redis rabbitmq seq mailhog

# Bring the whole stack up (services included once they have working images)
docker compose up -d --build
```

Local URLs: gateway `http://localhost:5000`, RabbitMQ UI `http://localhost:15672` (guest/guest), Seq logs `http://localhost:5341`, Mailhog UI `http://localhost:8025`.

## Branching workflow (Gitflow)

| Branch | From | Into |
|---|---|---|
| `feature/<name>` | `develop` | `develop` via PR |
| `release/<version>` | `develop` | `main` + `develop` |
| `hotfix/<name>` | `main` | `main` + `develop` |

**Never branch features off `main`.** `develop` is the integration branch. Commit messages follow Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, `test:`).

CI (`.github/workflows/ci.yml`) runs on push to `feature/*`, `release/*`, `hotfix/*`, `develop` and on PR to `develop`/`main`. The Angular job is gated on `frontend/task-manager-app/` existing.

## Non-obvious architectural decisions

These are decisions that took multiple PRs of refinement and would be easy to violate by accident:

### Mediator is `martinothamar/Mediator`, **not** MediatR
Different NuGet package (`Mediator.SourceGenerator` + `Mediator.Abstractions`), different DI call (`AddMediator`), source-generated dispatch. API surface is near-identical to MediatR so it's easy to write the wrong `using` and accidentally pull in MediatR. The spec rejected MediatR explicitly for the free-MIT-only constraint.

### Result pattern, **not** exceptions
Handlers return `Result<T>` (FluentResults). Expected domain failures (not found, conflict, unauthorized) are `Result.Fail(...)` — **never thrown**. The presentation layer maps `Result` to HTTP status codes via a shared `ToHttpResult()` extension. Throwing inside a handler is reserved for genuine bugs, which get caught by `ExceptionHandlingMiddleware` and returned as `ProblemDetails`.

### Onion dependency rule (enforced by tests)
```
Presentation  ──►  Application  ──►  Domain
Infrastructure ──►  Application  ──►  Domain
```
Each service test project includes a `NetArchTest.Rules` fixture asserting:
- `Domain` references no external NuGet packages
- `Application` does not reference `Infrastructure`
- `Infrastructure` is not referenced by `Domain` or `Application`

If your build fails the architecture test, you violated the rule — don't suppress it, restructure.

### `DateTimeOffset` everywhere, never `DateTime`
Entity properties, DTOs, API surface, integration-event records — all `DateTimeOffset`. PostgreSQL stores `timestamptz`. The frontend converts to local at *display* only. If you see `DateTime` in new code, it's a bug. A grep of the spec for `\bDateTime\b(?!Offset)` returns zero hits — code should match.

### `AppUser` is the documented exception to "all setters private"
ASP.NET Core Identity's `UserManager` writes to inherited properties (`Email`, `UserName`, `EmailConfirmed`, `LockoutEnd`) directly. `AppUser : IdentityUser<Guid>` therefore inherits public setters. **This is the only entity in the project exempt from the rich-domain-model rule.** Every other entity has private setters + factory methods + behavior methods.

### Outbox on Tasks, Inbox on Analytics, neither on Notifications
- Tasks publishes events via MassTransit's EF Core **outbox** (`AddEntityFrameworkOutbox<TasksDbContext>`) so events survive a crash between `SaveChangesAsync` and broker ack.
- Analytics consumes via MassTransit's EF Core **inbox** on `AnalyticsDbContext` so duplicate deliveries can't double-increment stats.
- Notifications deliberately has **neither**. A duplicate toast/email is acceptable, and adding a dedup store to a Redis-only service is more complication than the failure mode warrants. Don't "fix" this.

### `TaskItem` has `byte[] RowVersion` + `If-Match`
Optimistic concurrency on `TaskItem` is required by the spec. Mutating endpoints (`PUT /api/tasks/{id}`, `move`, `assign`, comment edit) take an `If-Match` header carrying the base64-encoded `RowVersion` the client last saw. `DbUpdateConcurrencyException` from `SaveChangesAsync` maps to `Result.Fail("conflict...")` → HTTP 409 with the current task body. SPA refetches + toasts.

### `/api/tasks` is hard-capped at 200 results
No pagination in v1. Handler enforces the cap; if exceeded, returns first 200 ordered by `UpdatedAt DESC` and sets response header `X-Result-Truncated: true`. SPA shows a non-blocking notice when the header is present.

### Refresh cookie is `SameSite=Strict` (local-first)
`HttpOnly; Secure; SameSite=Strict; Path=/api/auth/refresh`. This eliminates CSRF on the refresh endpoint without a CSRF token. **It also breaks cross-eTLD+1 deployments** (e.g. SPA on Vercel + API on Fly.io). See spec §10 *Deployment compatibility — refresh cookie* before deploying to real public hosts; options are documented there.

### SignalR JWT auth via query string
Browsers can't set `Authorization` headers on the WS handshake. The SPA passes the token via `accessTokenFactory`. Server-side: `JwtBearerEvents.OnMessageReceived` lifts `access_token` off `context.Request.Query` whenever the path starts with `/hubs/`. The standard `[Authorize]` on the hub then works.

### Refresh-token reuse detection
If a refresh token that is **already revoked** is presented, treat as theft: revoke **every** refresh token for that user and return 401. Forces both attacker and legitimate user to re-authenticate. Don't simplify this away.

### Refresh tokens stored hashed (SHA-256)
Plaintext refresh tokens only ever leave the server in the cookie. The `refresh_tokens` table stores SHA-256 hashes. Lookup is by hash; comparing plaintext to plaintext is a bug.

### BCrypt cost 12
Set explicitly via `BCrypt.Net-Next` — defaults are too low for 2026 hardware. Don't lower this.

### Notifications Redis schema
- History: per-user sorted set `notifications:user:{userId}`. Score = unix-ms timestamp; value = JSON-serialised `NotificationDto`. On write: `ZADD` → `ZREMRANGEBYRANK 0 -51` (keep newest 50) → `EXPIRE 2592000` (30 days, refreshed each write).
- Preferences: per-user hash `prefs:user:{userId}`, no TTL.
- Read state is stored inside the JSON in the sorted set (read-modify-write), not a separate key.

### Source-of-truth env var names
See spec §8 *Secrets* for the canonical list (`JWT_SECRET`, `IDENTITY_DB_CONNECTION`, `TASKS_DB_CONNECTION`, `ANALYTICS_DB_CONNECTION`, `REDIS_URL`, `RABBITMQ_URL`, `SMTP_*`, `SEQ_URL`). Use these names verbatim across local, staging, prod.

## Implementation order

Section 11 of the spec is the implementation playbook. Each step is one PR into `develop`:

| Step | Branch | Status |
|---|---|---|
| 1 — Scaffold solution + infra | `feature/scaffold-solution` | done (PR #4) |
| 2 — Identity service + Angular auth shell | `feature/identity-service` | next |
| 3 — Tasks service | `feature/tasks-service` | pending |
| 4 — Notifications service | `feature/notifications-service` | pending |
| 5 — Analytics service | `feature/analytics-service` | pending |
| 6 — Gateway (YARP routes + auth) | `feature/gateway-service` | pending |
| 7 — Remaining Angular features | `feature/angular-remaining-features` | pending |
| 8 — Playwright E2E tests | `feature/e2e-tests` | pending |

The Angular auth shell (`core/auth/`, `core/http/`, `features/auth/`) ships in **Step 2**, not deferred to Step 7 — this is the "running app at each stage" decision. Step 7 fills in `features/boards/`, `features/tasks/`, `features/analytics/`, `shared/components/`.

Test-first per step: each `<step>a` writes failing tests; the matching `<step>b` makes them green. Don't write production code until the test step is in place and red.

## Layout

```
src/
  gateway/TaskManager.Gateway/                       YARP, no EF Core
  services/{identity,tasks,notifications,analytics}/TaskManager.<svc>/
    Domain/         entities, value objects, repository interfaces, IUnitOfWork
    Application/    commands+handlers, queries+handlers, DTOs, Behaviors (ValidationBehavior, LoggingBehavior)
    Infrastructure/ DbContext+UoW impl, repositories, EF configs, external services (TokenService, etc.), Messaging/ for MassTransit
    Presentation/   Minimal API endpoints (RouteGroupBuilder extensions, one class per aggregate), middleware
  shared/TaskManager.Contracts/Events/   integration event records only — no domain logic, no EF, no HTTP models
tests/TaskManager.<svc>.Tests/           xUnit + FluentAssertions + NSubstitute + Microsoft.AspNetCore.Mvc.Testing + Bogus + NetArchTest.Rules + service-specific Testcontainers
tests/TaskManager.E2E.Tests/             Microsoft.Playwright, no service refs
frontend/task-manager-app/               Angular 18 (created in Step 2b, not yet in repo)
docker-compose.yml + docker-compose.override.yml
.github/workflows/{ci,cd-staging,cd-production}.yml
SmartTaskManager.sln
Directory.Build.props                    centralises TargetFramework=net10.0, Nullable, ImplicitUsings, LangVersion=latest, AnalysisLevel=latest
global.json                              pins SDK to 10.0.203
NuGet.config                             pins repo to nuget.org only — overrides any private feeds in the dev's global config
```

`appsettings.Development.json` is **committed** for non-secret local-dev defaults (per spec §9). `appsettings.Local.json` is gitignored for per-developer overrides.

## Gotchas observed during scaffolding

- `dotnet new sln` in .NET 10 produces `.slnx` by default. Spec uses `.sln`. Pass `--format sln` to override.
- A globally-configured private NuGet feed (Azure DevOps) returning 401 will break restore even when the package is on nuget.org. The repo-local `NuGet.config` is `<clear />` + nuget.org to defend against this.
- Mapperly 4.2.1 pulls in `Scriban 5.4.6` transitively. Scriban has open vulnerability advisories. Builds emit ~50 NU1902/NU1903/NU1904 warnings. Not a runtime concern (Scriban only runs inside the source generator at build time). Bump Mapperly when a newer release lands.
- On Windows, git emits `LF will be replaced by CRLF` warnings for every text file. Harmless.
