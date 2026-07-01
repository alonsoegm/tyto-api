# Contributing to Tyto API

Thanks for contributing! This guide covers the local setup and the workflow the
CI pipeline enforces, so your PRs go green the first time.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned via [`global.json`](global.json))
- A SQL Server instance for running the app locally (see [README](README.md#local-setup))

## One-time setup

Restore the local tools and wire up the git hooks:

```bash
dotnet tool restore      # installs Husky.NET + dotnet-outdated (from .config/dotnet-tools.json)
dotnet husky install     # activates the git hooks in .husky/
dotnet restore Tyto.sln  # restores packages (generates/uses packages.lock.json)
```

## Git hooks (run automatically)

Installed via [Husky.NET](https://alirezanet.github.io/Husky.Net/):

| Hook | What it does |
|---|---|
| `pre-commit` | `dotnet format --verify-no-changes` on staged C# files — blocks unformatted code |
| `commit-msg` | Validates the message against Conventional Commits |
| `pre-push`   | `dotnet build` (warnings-as-errors) + `dotnet test` — blocks pushing a red branch |

To run a check manually: `dotnet format Tyto.sln` (auto-fixes), or
`dotnet husky run --group pre-commit`.

## Commit messages — Conventional Commits

Format: `type(optional-scope): description`

Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`,
`revert`, `style`, `test`.

```
feat(db): add filtered index for default model
fix(auth): reject expired tokens on readiness probe
docs(readme): document SQL Server connection strings
```

Commit types drive automated versioning and the changelog (`feat` → minor,
`fix` → patch, `feat!`/`BREAKING CHANGE` → major).

## Branch & PR workflow

1. Branch off `main`: `git switch -c feat/<short-name>`.
2. Make changes; keep commits scoped and conventional.
3. Push — the `pre-push` hook runs build + tests.
4. Open a PR; fill in the template. CI must be green and CODEOWNERS review approved.

## What CI checks

The [`CI`](.github/workflows/ci.yml) workflow mirrors the local build contract:

```bash
dotnet restore Tyto.sln --locked-mode
dotnet format Tyto.sln --verify-no-changes --no-restore
dotnet build   Tyto.sln -c Release --no-restore
dotnet test    Tyto.sln -c Release --no-build --settings .runsettings
```

Plus [CodeQL](.github/workflows/codeql.yml) and
[dependency/secret scanning](.github/workflows/security.yml).

## Dependencies

- After changing package references, run `dotnet restore` so `packages.lock.json`
  updates, and commit it (CI restores in `--locked-mode`).
- Check for updates with `dotnet dotnet-outdated`.
