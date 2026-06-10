# Changelog

All notable changes to Smart Task Manager are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-06-10

First production release. Feature-complete v1 per `smart-task-manager-spec.md`,
delivered across the 8-step implementation playbook (spec §11).

### Added
- **Identity service** — registration, login, JWT access tokens, hashed (SHA-256)
  refresh-token rotation with reuse detection, BCrypt cost 12, `SameSite=Strict`
  refresh cookie.
- **Tasks service** — boards, tasks, comments, assignment, drag-and-drop moves with
  `RowVersion` optimistic concurrency (`If-Match`), `/api/tasks` 200-result cap,
  configurable deadline scan, MassTransit EF Core outbox.
- **Notifications service** — Redis-backed per-user notification history and
  preferences, SignalR real-time delivery (JWT via query string), email via SMTP.
- **Analytics service** — per-user stat projections built from task events via
  MassTransit EF Core inbox, race-safe atomic upserts, ngx-charts dashboard.
- **Gateway** — YARP reverse proxy with JWT auth, CORS, and SignalR negotiate
  pass-through.
- **Angular 18 SPA** — auth shell, boards, tasks, analytics, shared components.
- **Playwright E2E suite** (`tests/TaskManager.E2E.Tests`) covering all
  Definition-of-Done user flows against the full Docker Compose stack.

### Fixed
- Cross-service messaging: consumers now bind explicitly to the `task-manager`
  topic exchange (convention-based endpoints silently dropped all events).
- Notifications→Identity service-to-service calls now mint a service JWT.
- Gateway CORS allows `X-Requested-With` and `X-SignalR-User-Agent` so the
  SignalR negotiate preflight succeeds.
- Board-create form wrapped in a `FormGroup` so `(ngSubmit)` fires.

[1.0.0]: https://github.com/NickErmol/TaskManager/releases/tag/v1.0.0
