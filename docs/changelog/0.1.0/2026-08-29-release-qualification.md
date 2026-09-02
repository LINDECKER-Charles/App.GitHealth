# Qualifying the release on real repositories

- **Type** — `test`, `ci`, `fix`, `chore`
- **Scope** — `infra`, `ci`, `docs`
- **Landed** — 2026-08-29
- **Commits** — `63de23d`, `564ec74`, `2d70f50`, `94a4789`, `177eede`

## What shipped

An acceptance harness that runs the release against real repositories
(`tests/Infrastructure/Invoke-RealRepositoryAcceptance.ps1` and its helpers): it replays
the main scenario, compares the metrics against the reference Git commands and diffs refs,
reflogs, index and worktree before and after the run. The workflow automates that
qualification, the production container is confined (unprivileged, read-only mounts,
dropped capabilities), and the version is prepared as `0.1.0-rc.1` with its release
document.

## Why

Fixtures prove correctness on the topologies we thought of; a real repository is what finds
the ones we did not. The before/after diff on refs, reflogs, index and worktree is the
acceptance criterion that matters most, because a read-only product that writes once is a
product that cannot be trusted again.

## Consequences

`0.1.0-rc.1` was never tagged nor published: the version was renamed to `0.1.0` before
release — see [the first stable version](2026-08-31-first-stable-version.md).
