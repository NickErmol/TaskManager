# Git / Branching Rules

## Gitflow
| Branch | Branch from | Merge into |
|--------|------------|------------|
| `feature/<name>` | `develop` | `develop` via PR |
| `release/<version>` | `develop` | `main` + `develop` |
| `hotfix/<name>` | `main` | `main` + `develop` |

**Never branch features off `main`.** `develop` is the integration branch.

## Commit messages
Follow Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `test:`.

## CI triggers
`.github/workflows/ci.yml` runs on push to `feature/*`, `release/*`, `hotfix/*`, `develop` and on PR to `develop`/`main`.
The Angular job is gated on `frontend/task-manager-app/` existing — it will not run until Step 2b.

## Implementation order
See `CLAUDE.md` § Implementation order table. Each step is one PR into `develop`.
Current status tracked there — check before starting a new step.
