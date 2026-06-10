# Architecture Rules

## Onion dependency rule (enforced by NetArchTest)
```
Presentation  ──►  Application  ──►  Domain
Infrastructure ──►  Application  ──►  Domain
```
- `Domain` references **no** external NuGet packages.
- `Application` does **not** reference `Infrastructure`.
- `Infrastructure` is **not** referenced by `Domain` or `Application`.

If the architecture test fails, **restructure** — never suppress or delete the test.

## Result pattern — no exceptions for domain failures
Handlers return `Result<T>` (FluentResults).
Expected failures (not found, conflict, unauthorized) → `Result.Fail("message")`.
The presentation layer maps `Result` → HTTP status via `ToHttpResult()`.
Throwing inside a handler is reserved for genuine bugs caught by `ExceptionHandlingMiddleware`.

## Outbox / Inbox placement
- **Tasks** service: MassTransit EF Core outbox (`AddEntityFrameworkOutbox<TasksDbContext>`).
- **Analytics** service: MassTransit EF Core inbox on `AnalyticsDbContext`.
- **Notifications** service: **neither**. Duplicate toasts/emails are acceptable. Do not add dedup.
