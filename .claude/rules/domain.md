# Domain Model Rules

## Private setters everywhere — except AppUser
Every entity has **private setters + factory methods + behavior methods**.
`AppUser : IdentityUser<Guid>` is the single documented exception — ASP.NET Core Identity writes inherited properties directly.

## DateTimeOffset everywhere — never DateTime
Entity properties, DTOs, API surface, integration-event records: all `DateTimeOffset`.
PostgreSQL stores `timestamptz`. Frontend converts to local at display time only.
Seeing `DateTime` (without `Offset`) in new code is a bug.

## TaskItem optimistic concurrency
`TaskItem` has `byte[] RowVersion`. Mutating endpoints (`PUT`, `move`, `assign`, comment edit) require `If-Match` header with base64-encoded `RowVersion`.
`DbUpdateConcurrencyException` → `Result.Fail("conflict…")` → HTTP 409 with current task body.

## `/api/tasks` hard cap
Handler enforces max 200 results ordered by `UpdatedAt DESC`. If exceeded, sets `X-Result-Truncated: true` response header. No pagination in v1.

## Refresh token rules
- Stored **hashed** (SHA-256) in `refresh_tokens` table. Plaintext only leaves in the cookie.
- Reuse detection: if a **revoked** token is presented, revoke **all** tokens for that user and return 401.
- BCrypt cost **12** — never lower it.
- Refresh cookie: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth/refresh`.
