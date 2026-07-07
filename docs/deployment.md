# Deployment runbook

Operational guide for taking Smart Task Manager from `docker compose up` (the
primary dev/demo target) to real public hosts. The authoritative design lives in
[`smart-task-manager-spec.md`](../smart-task-manager-spec.md) §10 — this file is the
"what do I actually click/set" companion.

> **Status:** v1.0.0 is tagged and the CI/CD pipeline is wired, but **no live
> environment is provisioned**. Deploys are gated off (see *Enabling deploys*) so
> CD runs stay green until you add the secrets below.

---

## 1. The one blocker you must decide first — refresh cookie & eTLD+1

The refresh-token cookie is `HttpOnly; Secure; SameSite=Strict; Path=/api/auth/refresh`
(spec §4.2). `SameSite=Strict` is **not sent on cross-site requests**, where
"cross-site" means a different registrable domain (eTLD+1). Local dev works because
everything is `localhost`. A naive cloud deploy (SPA on `*.vercel.app`, API on
`*.fly.dev`) is cross-site, so **the refresh flow silently breaks on first deploy** —
users get logged out the moment their access token expires.

Pick one before going live (spec §10 *Deployment compatibility — refresh cookie*):

| Option | Action | Trade-off |
|---|---|---|
| **A — Same eTLD+1 (recommended)** | Buy a domain (~$12/yr). Point `app.yourdomain.com` → Vercel and `api.yourdomain.com` → the Fly gateway. | Cookie keeps `SameSite=Strict`; **zero code change**. Preserves the security design verbatim. |
| **B — `SameSite=None; Secure` + CSRF** | Relax the cookie, add `GET /api/csrf-token` + a double-submit-cookie check on `/api/auth/refresh`. | Code change; reintroduces the CSRF protection `Strict` gave for free. |
| **C — Drop the cookie** | Keep the refresh token in JS memory like the access token. | No "stay logged in" — users re-auth every browser session. |

**v1 ships option A by default** (no code change). Only touch the cookie code if you
deliberately choose B or C.

---

## 2. Deploy targets (all free tiers)

| Component | Platform | Notes |
|---|---|---|
| Angular SPA | Vercel | `--prod` on production, preview URL on staging |
| .NET services | Fly.io | 5 apps; free tier is tight — deploy only what you need to demo |
| PostgreSQL | Fly.io Postgres | separate prod DB |
| Redis | Upstash | free 10k req/day |
| RabbitMQ | CloudAMQP | free `lemur` plan |

Before a production frontend build works, set the real API origin in
[`environment.prod.ts`](../frontend/task-manager-app/src/environments/environment.prod.ts)
(and `environment.staging.ts`) — they currently point at `api.example.com` placeholders.
With option A this becomes `https://api.yourdomain.com`.

---

## 3. Required GitHub secrets & variables

Settings → Secrets and variables → Actions.

### Secrets
| Secret | Used by | Value source |
|---|---|---|
| `JWT_SECRET` | all services (compose default is a dev placeholder) | random 64-byte hex |
| `FLY_API_TOKEN` | cd-staging, cd-production | `flyctl auth token` |
| `VERCEL_TOKEN` | cd-staging, cd-production | Vercel → Settings → Tokens |
| `VERCEL_PROJECT_ID` | cd-staging, cd-production | `.vercel/project.json` after `vercel link` |
| `VERCEL_ORG_ID` | cd-staging, cd-production | `.vercel/project.json` |

`GITHUB_TOKEN` (image push to GHCR) is provided automatically — no setup.

### Variables (these flip deploys on)
| Variable | Effect when `true` |
|---|---|
| `FLY_DEPLOY_ENABLED` | runs the Fly backend deploy job |
| `VERCEL_DEPLOY_ENABLED` | runs the Vercel frontend deploy job |

### OAuth provider credentials (v1.3)

These are **optional** — the app runs fine without them. The "Continue with
Google" / "Continue with GitHub" buttons simply don't appear until credentials
are set, so they are not needed for the gated-off default deploy.

| Secret | Used by | Value source |
|---|---|---|
| `OAUTH_GOOGLE_CLIENT_ID` | Identity | Google OAuth app |
| `OAUTH_GOOGLE_CLIENT_SECRET` | Identity | Google OAuth app |
| `OAUTH_GITHUB_CLIENT_ID` | Identity | GitHub OAuth app |
| `OAUTH_GITHUB_CLIENT_SECRET` | Identity | GitHub OAuth app |

How to create the apps:

- **Google:** Google Cloud Console → APIs & Services → Credentials → OAuth
  client ID (Web application). Authorized redirect URI:
  `https://<api-host>/api/auth/external/signin-google`.
- **GitHub:** Settings → Developer settings → OAuth Apps → New. Authorization
  callback URL: `https://<api-host>/api/auth/external/signin-github`. Scope
  used: `user:email`.
- `<api-host>` is the gateway's public origin (e.g. `api.yourdomain.com`),
  matching your eTLD+1 choice in §1 above.

Also set `FRONTEND_URL` (a repo/Fly environment variable, not a secret) to the
SPA's public origin so callback redirects land on the real site.

**Never set `OAUTH_FAKE_ENABLED` in staging/prod.** It's ignored outside the
Development environment anyway (hard-coded guard), but don't set it.

---

## 4. Enabling deploys

The deploy jobs are **gated off by default** so a missing-secret run can't fail CD.
To turn a target on:

1. Add the secrets above.
2. Set the matching repository **variable** to `true` (`FLY_DEPLOY_ENABLED` and/or
   `VERCEL_DEPLOY_ENABLED`).
3. For Fly, create the apps first (`flyctl apps create smart-task-manager-<svc>` and
   the `-staging` variants) so `flyctl deploy --app ...` has a target.

Until then, `build-and-push` still runs and publishes images to
`ghcr.io/<owner>/smart-task-manager-<svc>` — the deploy steps simply skip.

---

## 5. CI/CD pipeline map

| Workflow | Trigger | Does |
|---|---|---|
| `ci.yml` | push to `feature/**` `release/**` `hotfix/**` `develop`; PR to `develop`/`main` | dotnet test matrix + Angular lint/test/build |
| `e2e.yml` | PR to `develop`/`main`; manual dispatch | full compose stack + ng serve + Playwright (16 flows) |
| `cd-staging.yml` | push to `develop` | build/push `:develop` images → (gated) Vercel preview + Fly `-staging` |
| `cd-production.yml` | push tag `v*.*.*` | build/push versioned images → (gated) Vercel `--prod` + Fly prod |

Production deploys are **tag-driven**: complete the Gitflow release (merge `release/*`
→ `main`), then `git tag -a vX.Y.Z && git push origin vX.Y.Z` to ship.

---

## 6. Release checklist

1. `release/X.Y.Z` from `develop`; bump `Version` in `Directory.Build.props` + frontend `package.json`; update `CHANGELOG.md`.
2. PR `release/X.Y.Z` → `main`; CI green.
3. Merge to `main`.
4. `git tag -a vX.Y.Z -m "Release vX.Y.Z" && git push origin vX.Y.Z` → triggers `cd-production.yml`.
5. Back-merge `main` → `develop`.
