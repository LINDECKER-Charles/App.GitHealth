# Resolving Git outside the `PATH`

- **Type** — `feat`
- **Scope** — `api`
- **Landed** — 2026-08-30
- **Commits** — `54dc006`

## What shipped

Git is resolved in order: the configured path first — `--git-path <path>` or the
`GitHealth:Git:ExecutablePath` setting — then the `PATH`, then the platform's standard
installation locations. `GET /api/runtime` exposes availability, the path retained and the
diagnostic, and without Git the interface shows a banner naming the locations tried and the
`--git-path` option.

## Why

An installed application cannot assume Git is on the `PATH`: it is launched from a desktop
shortcut, not from a shell that has sourced a profile. Without this, the failure surfaced
at the first scan as a generic error, at the moment furthest from its cause.

Reporting the resolution through `/api/runtime` rather than only logging it is what lets
the interface state the cause up front — the user is told before doing anything, not after
the first attempt fails.
