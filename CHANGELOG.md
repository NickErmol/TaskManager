# Changelog

All notable changes to Smart Task Manager are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-07-08

"OAuth / social login" release.

### Added
- **OAuth / social login** (§13.6) — "Continue with Google" and "Continue with
  GitHub" on the login and register screens via a server-side OAuth 2.0
  authorization-code flow hosted by the Identity service. Both providers use
  ASP.NET's in-box generic OAuth handler (no new packages) and register only when
  their client id/secret are configured, so the buttons appear only where
  credentials are set. A provider-asserted **verified** matching email auto-links
  to an existing account (and revokes that account's prior refresh tokens as an
  account-pre-hijacking guard); an unknown verified email creates a passwordless
  confirmed account. Token delivery reuses the existing refresh-cookie flow
  unchanged. Sign-in/sign-up only — no connected-accounts management UI. Flips the
  §1 non-goal. No database migration (`AspNetUserLogins` already existed).

### Fixed
- Identity honors `X-Forwarded-Host`/`Proto` behind the gateway so OAuth redirect
  URIs target the public origin instead of the internal container address.

## [1.2.0] - 2026-07-07

"File Attachments" release, plus a full visual refresh of the SPA.

### Added
- **File attachments on tasks** (§13.5) — upload/download/delete (≤10 MB, common
  types validated by extension, content type, and magic bytes), stored in MinIO
  via the Tasks service. Uploads are proxied (browser → gateway → Tasks → MinIO,
  MinIO stays private), capped at 20 per task, and downloads always ship
  `Content-Disposition: attachment`. Attachments ride in `TaskDto`, so live board
  sync works through the existing `TaskUpserted` broadcast; `AttachmentAdded` /
  `AttachmentRemoved` contract events feed the per-board activity feed.
- **Violet brand redesign** — custom Material 3 theme (violet primary, magenta
  tertiary), Inter font, branded sticky nav, gradient auth screens, board cards
  with accent bar and initials avatar, compact-density filter bar.

### Fixed
- Attachment upload over plain-HTTP MinIO no longer 500s (removed
  `DisablePayloadSigning`, which AWSSDK.S3 rejects over http).
- Task reads now include attachments (`GET` returned an empty list and download
  404'd even though rows persisted).
- Oversize uploads return 400 instead of 500 (`BadHttpRequestException` from the
  multipart body-length limit is now surfaced with its own status code).
- Board quick-create input no longer stays red after a successful submit.

## [1.1.0] - 2026-06-11

"Live Collaboration" release. Four features extending v1.0, each landing a v1.1
addendum in `smart-task-manager-spec.md` (§13.1–§13.4).

### Added
- **Labels & filtering UI** (§13.1) — label manager dialog (12-swatch palette),
  label picker in the task dialog, and a board filter bar (free-text, label
  multi-select, assignee tri-state, priority) composed AND-across / OR-within-labels,
  persisted to shareable query params. Client-side filtering (the 200-task cap makes
  it instant).
- **Subtasks / checklists** (§13.2) — `ChecklistItem` child collection on tasks
  (`checklist_items` table), inline editor in the task dialog (add, toggle,
  rename, delete), and a `done/total` progress chip on cards. Checklist writes take
  no `If-Match` and don't advance the task `RowVersion`, so concurrent member toggles
  never conflict.
- **Real-time collaborative boards** (§13.3) — a Tasks-hosted SignalR `BoardHub`
  (`/hubs/board`) with membership-gated join, best-effort fire-after-commit
  `TaskUpserted`/`TaskDeleted` broadcast (not via the outbox), and in-memory
  connection-refcounted presence. The SPA applies frames only when strictly newer
  than the local copy (dropping stale frames and its own optimistic echo) and shows
  presence avatars of other viewers.
- **Per-board activity feed** (§13.4) — a collapsible live audit panel on board
  detail, served by a membership-enforced
  `GET /api/analytics/boards/{id}/activity`. Two new contract events
  (`TaskUpdatedEvent`, `TaskDeletedEvent`) plus `ActorId`/`TaskTitle` enrichment of
  the Analytics `task_events` projection; actor names resolved client-side via the
  Identity user lookup.

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

[1.3.0]: https://github.com/NickErmol/TaskManager/releases/tag/v1.3.0
[1.2.0]: https://github.com/NickErmol/TaskManager/releases/tag/v1.2.0
[1.1.0]: https://github.com/NickErmol/TaskManager/releases/tag/v1.1.0
[1.0.0]: https://github.com/NickErmol/TaskManager/releases/tag/v1.0.0
