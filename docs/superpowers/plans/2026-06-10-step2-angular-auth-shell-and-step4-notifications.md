# Step 2 (Angular auth shell) + Step 4 (Notifications service) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish Step 2's outstanding frontend half (Angular 18 auth shell) and implement Step 4 (Notifications service), each as one PR into `develop`.

**Architecture:** Part A scaffolds `frontend/task-manager-app` (Angular 18, standalone, NgRx Signals, Material 3, Tailwind, Jest) and builds `core/auth` + `features/auth` per spec §7. Part B implements `TaskManager.Notifications` per §4.4: MassTransit consumers → EventMapper → Redis history + SignalR broadcast + MailKit email, REST endpoints for history/preferences. Onion layering (no Domain layer — the service owns no entities; Application holds interfaces + logic, Infrastructure holds Redis/SMTP/MassTransit/SignalR adapters).

**Tech Stack:** Angular 18, @ngrx/signals 18, @angular/material 18, tailwindcss 3, jest-preset-angular, angular-eslint, prettier · .NET 10, MassTransit 8 + RabbitMQ, StackExchange.Redis, MailKit, SignalR, Mediator (martinothamar), FluentResults, Serilog.

**Environment constraints (from memory):** No Docker on this machine — Testcontainers integration tests must *compile* locally but are verified in CI. Local verification = unit tests + arch tests + `dotnet build` + Jest + `ng build`. The Step 2b browser acceptance ("register → login → boards placeholder via gateway") cannot run locally (no gateway until Step 6, no Docker); it is deferred to CI/Step 6 and noted in the PR.

**Documented spec deviations (put in PR description for Part B):**
1. `TaskCommentAddedEvent` gains `string Title, Guid? AssigneeId` — §4.4 requires notifying the task assignee with the task title, but the §4.3 record carries neither. Tasks service knows both at publish time.
2. `TaskCompletedEvent` gains `IReadOnlyList<Guid> BoardMemberIds` — §4.4 notifies "board members"; the event must carry them (event-carried state transfer; Notifications has no board DB).
3. New env var `IDENTITY_URL` (e.g. `http://identity:8080`) — Notifications resolves recipient emails via Identity `GET /api/users/{id}` behind an `IUserDirectory` abstraction (events must not carry email PII onto the bus). Calls set `X-User-Id` to the recipient id — legitimate inside the §8 trusted private network.
4. No self-notification: an event whose recipient equals the actor (assignee == author, completedBy in members, assignedTo == assignedBy) produces no notification for that user.

---

## Part A — Angular auth shell (branch `feature/angular-auth-shell` off `develop`)

### Task A1: Scaffold the Angular project + toolchain

**Files:** entire `frontend/task-manager-app/` via CLI, then config edits.

- [ ] Branch: `git checkout develop && git pull && git checkout -b feature/angular-auth-shell`
- [ ] `cd frontend && npx --yes @angular/cli@18 new task-manager-app --style=scss --ssr=false --standalone --skip-git --skip-tests=false --routing` (use `npx -p @angular/cli@18 ng new ...` form if needed)
- [ ] Add Material 18 (`ng add @angular/material@18 --skip-confirmation`), `npm i @ngrx/signals@^18`, Tailwind 3 (`npm i -D tailwindcss@^3 postcss autoprefixer` + `npx tailwindcss init` + content globs + `@tailwind` directives in `styles.scss`)
- [ ] Replace Karma with Jest: `npm rm karma karma-* @types/jasmine jasmine-core`, `npm i -D jest @types/jest jest-preset-angular@^14`, add `setup-jest.ts`, `jest.config.js` (preset `jest-preset-angular`, `setupFilesAfterEach` per preset docs), tsconfig.spec.json types `jest`
- [ ] ESLint+Prettier: `ng add @angular-eslint/schematics@18 --skip-confirmation`, `npm i -D prettier eslint-config-prettier`, add §7 rules (`prefer-on-push-component-change-detection: error`, `no-empty-lifecycle-method: error`, `no-explicit-any: error`, `explicit-function-return-type: warn`)
- [ ] tsconfig: ensure `strict`, `strictTemplates`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `forceConsistentCasingInFileNames` all true
- [ ] Environments: `src/environments/environment.ts` (apiUrl `http://localhost:5000`, environment `local`), `environment.staging.ts`, `environment.prod.ts` per §7; `angular.json` `staging`/`production` configurations with `fileReplacements`
- [ ] package.json scripts: `"lint": "ng lint"`, `"test": "jest"`, `"test:ci": "jest --ci"`, `"build": "ng build"` (CI calls `npm run lint`, `npm run test:ci`, `npm run build -- --configuration production`)
- [ ] Verify: `npm run lint` ✓, `npm run test:ci` ✓ (default app spec under Jest), `npm run build -- --configuration production` ✓
- [ ] Commit `feat(frontend): scaffold Angular 18 app with Material, Tailwind, Jest, ESLint`

### Task A2: Models + api-base

**Files:** Create `src/app/core/http/api-base.ts`, `src/app/core/models/auth.models.ts`, barrels.

```typescript
// core/models/auth.models.ts
export interface UserDto { id: string; email: string; displayName: string; avatarUrl: string | null; }
export interface AuthResponse { accessToken: string; refreshToken: string; user: UserDto; }
export interface LoginRequest { email: string; password: string; }
export interface RegisterRequest { email: string; displayName: string; password: string; }

// core/http/api-base.ts
import { environment } from '../../../environments/environment';
export const API_BASE = environment.apiUrl;
export const apiUrl = (path: string): string => `${API_BASE}${path}`;
```

- [ ] Commit `feat(frontend): add auth models and api-base`

### Task A3: Auth shell specs first (red)

**Files:** Create `core/auth/auth.store.spec.ts`, `auth.interceptor.spec.ts`, `refresh.interceptor.spec.ts`, `error.interceptor.spec.ts`, `auth.guard.spec.ts`.

Key behaviors to spec (use `HttpClientTestingModule`/`provideHttpClientTesting` + `HttpTestingController`):
- AuthStore: `login()` POSTs `/api/auth/login` with `withCredentials: true`, stores token+user in memory, sets isLoading during flight, error message on 401; `register()` POSTs `/api/auth/register`; `logout()` POSTs `/api/auth/logout` and clears state; `refreshToken()` POSTs `/api/auth/refresh` and swaps the access token; `loadProfile()` GETs `/api/users/me`.
- authInterceptor: adds `Authorization: Bearer <t>` to non-`/api/auth/**` requests when token present; leaves `/api/auth/**` untouched.
- refreshInterceptor: on 401 → one refresh call → retried request carries new token; **concurrent 401s share one in-flight refresh** (assert exactly one POST `/api/auth/refresh` for two simultaneous 401s); refresh failure → both errors propagate + store logs out.
- errorInterceptor: maps HttpErrorResponse to user-facing message (e.g. rethrows with friendly text), passes through success.
- authGuard: token present → true; absent → `UrlTree` to `/login`.

- [ ] Run `npx jest src/app/core/auth --ci` — all red (modules missing)
- [ ] Commit `test(frontend): failing specs for auth store, guard, interceptors`

### Task A4: Implement core/auth (green)

**Files:** Create `core/auth/auth.store.ts`, `auth.guard.ts`, `auth.interceptor.ts`, `refresh.interceptor.ts`, `error.interceptor.ts`, `core/auth/index.ts`.

```typescript
// auth.store.ts — NgRx signalStore, token in memory only (§7)
export interface AuthState { user: UserDto | null; accessToken: string | null; isLoading: boolean; error: string | null; }
export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>({ user: null, accessToken: null, isLoading: false, error: null }),
  withComputed((s) => ({ isAuthenticated: computed(() => s.accessToken() !== null) })),
  withMethods((store, http = inject(HttpClient), router = inject(Router)) => ({
    async login(req: LoginRequest): Promise<void> { /* patchState isLoading; firstValueFrom(http.post<AuthResponse>(apiUrl('/api/auth/login'), req, { withCredentials: true })) → patchState user/token; navigate /boards; catch → error */ },
    async register(req: RegisterRequest): Promise<void> { /* same shape against /api/auth/register */ },
    async refreshToken(): Promise<string> { /* POST /api/auth/refresh withCredentials → patch token, return it; on error clear state + throw */ },
    async logout(): Promise<void> { /* POST /api/auth/logout (withCredentials) ignore errors → clear state → navigate /login */ },
    async loadProfile(): Promise<void> { /* GET /api/users/me → patch user */ },
  })),
);
```

```typescript
// refresh.interceptor.ts — concurrent-401 queue (§7, verbatim pattern)
let refreshInFlight$: ReplaySubject<string> | null = null;
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  if (req.url.includes('/api/auth/')) return next(req);
  return next(req).pipe(catchError((err: HttpErrorResponse) => {
    if (err.status !== 401) return throwError(() => err);
    if (!refreshInFlight$) {
      refreshInFlight$ = new ReplaySubject<string>(1);
      const subj = refreshInFlight$;
      store.refreshToken().then(t => { subj.next(t); subj.complete(); })
        .catch(e => subj.error(e)).finally(() => { refreshInFlight$ = null; });
    }
    return refreshInFlight$.pipe(take(1),
      switchMap(t => next(req.clone({ setHeaders: { Authorization: `Bearer ${t}` } }))),
      catchError(e => { void store.logout(); return throwError(() => e); }));
  }));
};
```

authInterceptor: functional, skip `/api/auth/**`, clone with bearer if `store.accessToken()`. authGuard: functional `CanActivateFn` returning `true | router.createUrlTree(['/login'])`. errorInterceptor: catch → console.error in dev → rethrow Error with friendly message.

- [ ] `npx jest src/app/core/auth --ci` → green
- [ ] Commit `feat(frontend): auth store, guard, interceptors with concurrent-401 queue`

### Task A5: features/auth + routes + shell

**Files:** Create `features/auth/login.component.ts|.spec.ts`, `register.component.ts|.spec.ts`, `features/auth/index.ts`, `features/boards/board-list.component.ts` (placeholder), modify `app.routes.ts`, `app.config.ts`, `app.component.*`.

- Specs first: LoginComponent renders form, invalid email disables submit, submit calls `AuthStore.login`; RegisterComponent validates password (min 8, 1 digit, 1 upper) + displayName 2–50, calls `register`. Red → implement → green.
- Both: `tm-` selector, standalone, OnPush, `inject()`, reactive forms, Material fields, Tailwind layout.
- BoardListComponent placeholder: OnPush, "Boards coming in Step 7" empty state.
- Routes per §7 lazy table (`/login`, `/register`, `/boards` + authGuard, `**` → boards). `app.config.ts`: `provideHttpClient(withInterceptors([authInterceptor, refreshInterceptor, errorInterceptor]))`, `provideRouter(routes)`, `provideAnimationsAsync()`.

- [ ] `npm run lint && npm run test:ci && npm run build -- --configuration production` all green
- [ ] Commit `feat(frontend): login/register pages, routes, boards placeholder`

### Task A6: PR

- [ ] Push, open PR `feature/angular-auth-shell` → `develop`: title `feat(frontend): Angular auth shell (Step 2 frontend half)`; body notes browser acceptance deferred (no gateway until Step 6) and that this completes Step 2.
- [ ] Watch CI (`gh pr checks --watch`); fix failures. Merge when green.

---

## Part B — Notifications service (branch `feature/notifications-service` off `develop`)

### Task B0: Contract extensions (deviation #1/#2)

**Files:** Modify `src/shared/TaskManager.Contracts/Events/TaskCommentAddedEvent.cs`, `TaskCompletedEvent.cs`; update Tasks publishers `src/services/tasks/.../Application/Handlers/*` (comment add, move-to-Done) and any Tasks tests asserting event shape.

```csharp
public record TaskCommentAddedEvent(Guid TaskId, Guid BoardId, Guid CommentId, Guid AuthorId, string Body, string Title, Guid? AssigneeId);
public record TaskCompletedEvent(Guid TaskId, Guid BoardId, string Title, Guid CompletedBy, DateTimeOffset CompletedAt, IReadOnlyList<Guid> BoardMemberIds);
```

- [ ] Fix Tasks handlers/tests to populate new fields; `dotnet build` + `dotnet test tests/TaskManager.Tasks.Tests --filter Category!=Integration` (or unit folder) green
- [ ] Commit `feat(contracts): carry recipients on comment/completed events for notifications`

### Task B1 (Step 4a): Tests first — red

**Files:** Create under `tests/TaskManager.Notifications.Tests/`:
- `Architecture/OnionDependencyRuleTests.cs` — mirror Tasks fixture: `TaskManager.Notifications.Application` must not depend on `...Infrastructure`, `...Presentation`, `StackExchange.Redis`, `MassTransit`, `MailKit`, `Microsoft.AspNetCore.SignalR`.
- `Unit/EventMapperTests.cs` — for each of the 4 §4.4 events: correct `Type` (`task_assigned|task_commented|deadline_approaching|task_completed`), title template, recipient list; self-notification suppressed (assignee==author ⇒ empty; completedBy excluded from members; assignedTo==assignedBy ⇒ empty); `TaskCreatedEvent`/`TaskStatusChangedEvent` ⇒ no notifications.
- `Unit/PreferencesServiceTests.cs` — store returns null ⇒ defaults `(true,false,true,false)`; stored value round-trips; update persists via store.
- `Unit/NotificationDispatcherTests.cs` — dispatch stores + broadcasts always; emails only when the matching preference is true (assigned→EmailOnAssigned, etc.) and email lookup succeeds; lookup failure does not prevent store/broadcast.
- `Integration/NotificationsWebAppFactory.cs` (Testcontainers Redis + RabbitMQ, fake `IUserDirectory`, MailKit pointed at a Mailhog `GenericContainer` or substituted `IEmailSender` recorder), `Integration/ConsumerTests.cs` (TaskAssignedEvent → Redis entry + SignalR client receives; DeadlineApproachingEvent → Redis + email recorded/Mailhog), `Integration/NotificationEndpointsTests.cs` (GET last-50 order, POST {id}/read, POST read-all, GET/PUT preferences with defaults).

Tests reference Application types that don't exist yet — they must fail to compile? No: per project convention, write interfaces' *call sites* in tests, then `dotnet test` fails on compile. Acceptable red = compile errors at this stage per prior steps; to keep "compile but fail," create empty Application skeleton types (records + interfaces with no impl) in the same commit if prior steps did so — check PR #6/#7 convention and match it.

- [ ] `dotnet test tests/TaskManager.Notifications.Tests` → red
- [ ] Commit `test(notifications): failing tests for Step 4a`

### Task B2 (Step 4b): Application layer

**Files:** Create `src/services/notifications/TaskManager.Notifications/Application/`:
- `DTOs/NotificationDto.cs` (§4.4 record verbatim), `DTOs/NotificationPreferences.cs` (defaults true/false/true/false via `static NotificationPreferences Default`)
- `Interfaces/INotificationStore.cs` — `Task AddAsync(Guid userId, NotificationDto n, CancellationToken)`, `Task<IReadOnlyList<NotificationDto>> GetLatestAsync(Guid userId, CancellationToken)`, `Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken)`, `Task MarkAllReadAsync(Guid userId, CancellationToken)`
- `Interfaces/IPreferencesStore.cs` — `Task<NotificationPreferences?> GetAsync(Guid userId, ...)`, `Task SetAsync(Guid userId, NotificationPreferences p, ...)`
- `Interfaces/IUserDirectory.cs` — `Task<(string Email, string DisplayName)?> GetUserAsync(Guid id, ...)`
- `Interfaces/IEmailSender.cs` — `Task SendAsync(string to, string subject, string htmlBody, ...)`
- `Interfaces/INotificationBroadcaster.cs` — `Task BroadcastAsync(Guid userId, NotificationDto n, ...)`
- `EventMapper.cs` — pure static: `IReadOnlyList<(Guid Recipient, NotificationDto Dto)> Map(object @event)` per §4.4 table + deviation #4
- `PreferencesService.cs` — get-with-defaults / update
- `NotificationDispatcher.cs` — store → broadcast → (pref-gated) email per recipient

### Task B3: Infrastructure layer

**Files:** Create `Infrastructure/`:
- `Redis/RedisNotificationStore.cs` — `ZADD notifications:user:{id}` (score unix-ms) → `ZREMRANGEBYRANK key 0 -51` → `EXPIRE key 2592000`; GetLatest = `ZRANGE key 0 49 REV` deserialize; MarkRead = read-modify-write of the JSON member (remove old member, add updated, same score)
- `Redis/RedisPreferencesStore.cs` — `HSET/HGETALL prefs:user:{id}`, no TTL
- `Email/MailKitEmailSender.cs` — SMTP from `SMTP_HOST/SMTP_PORT/SMTP_FROM` env, simple inline HTML
- `Http/IdentityUserDirectory.cs` — typed HttpClient, base `IDENTITY_URL`, GET `/api/users/{id}` with `X-User-Id: {id}`
- `Messaging/` — 4 MassTransit consumers (`TaskAssignedEventConsumer`, `TaskCommentAddedEventConsumer`, `TaskCompletedEventConsumer`, `DeadlineApproachingEventConsumer`) each delegating to `NotificationDispatcher`; no inbox/outbox (CLAUDE.md rule)
- `DependencyInjection.cs` — `AddNotificationsInfrastructure(IConfiguration)`: Redis multiplexer from `REDIS_URL`, MassTransit RabbitMQ from `RABBITMQ_URL`, register all adapters

### Task B4: Presentation layer + Program.cs

**Files:** Create `Presentation/`:
- `Hubs/NotificationsHub.cs` — `[Authorize]`, `OnConnectedAsync` adds connection to group `Context.UserIdentifier`; broadcaster impl `SignalRNotificationBroadcaster` (Infrastructure or Presentation-adjacent; put in Infrastructure with `IHubContext<NotificationsHub>` — hub class itself in Presentation; if that creates a layering knot, hub in Presentation + broadcaster in Presentation registered against Application interface)
- `Endpoints/NotificationEndpoints.cs` — 5 routes per §4.4, `X-User-Id` via copied `HttpContextExtensions.GetUserId`, `ToHttpResult()` mapping copied from Tasks (`ResultExtensions.cs`)
- `Middleware/ExceptionHandlingMiddleware.cs` — ProblemDetails (copy Tasks pattern)
- `Program.cs` — Serilog, JWT bearer with `JwtBearerEvents.OnMessageReceived` lifting `access_token` from query when path starts with `/hubs/` (JWT_SECRET env), `AddSignalR`, infrastructure DI, health checks (`AddRedis`, `AddRabbitMQ` per §8), `MapHub<NotificationsHub>("/hubs/notifications")`, endpoints

- [ ] `dotnet test tests/TaskManager.Notifications.Tests --filter "FullyQualifiedName!~Integration"` → green; full solution `dotnet build` green
- [ ] Commit `feat(notifications): implement Notifications service (Step 4b)`

### Task B5: PR

- [ ] Push, open PR `feature/notifications-service` → `develop`: `feat(notifications): implement Notifications service (Step 4 of 8)`; body documents deviations #1–#4 and CI-only integration tests
- [ ] `gh pr checks --watch`; fix; merge when green. Update memory file with new status.

## Self-review notes
- Spec coverage: §4.4 responsibilities all mapped (consume events ✓ B3, SignalR ✓ B4, email ✓ B3, Redis history ✓ B3, REST ✓ B4); §7 auth-shell items all in A1–A5; §11 Step 2b acceptance deferred (documented); standing NetArchTest requirement ✓ B1.
- Types consistent: `NotificationDto`/`NotificationPreferences` shapes from §4.4 used across B1–B4; `AuthResponse`/`UserDto` from §4.2 mirrored in A2.
- Known judgment calls flagged as deviations #1–#4 (to be restated in PR bodies).
