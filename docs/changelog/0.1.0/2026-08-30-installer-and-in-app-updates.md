# Installer and in-app updates

- **Type** — `feat`, `fix`
- **Scope** — `api`, `front`
- **Landed** — 2026-08-30
- **Commits** — `8ceb894`, `f042c57`, `2197c58`

## What shipped

Velopack produces a per-user installer without a UAC prompt, plus delta packages, from the
GitHub release feed already published: `App.GitHealth-win-x64-Setup.exe` and
`App.GitHealth-<rid>-Setup.pkg` install under `%LocalAppData%\App.GitHealth` with Desktop
and Start menu shortcuts. An "Update" button appears in the top bar only when a newer
version has been published, and pre-releases are excluded from what is offered.

The database stays in `%LOCALAPPDATA%\GitHealth`, outside the installation folder, so it
survives an update and an uninstall. The `IUpdateService` abstraction leaves Docker, browser
mode and Linux on a silent implementation, where the user's package manager owns updates.

`POST /api/updates/apply` has two success answers — `202` when the host is about to restart,
`200` carrying the status when nothing was applicable, an unreachable release source for
instance. The interface handles both: the failure surfaces through the workspace banner
instead of leaving the button disabled on "Updating…" until restart.

## Why

An unreachable release feed is reported, never propagated: offline, the application stays
fully usable and simply does not offer an update.

Pre-releases are excluded because the support policy only covers the latest published
version — left enabled, a future `0.2.0-rc.1` would have been pushed to every installation
running `0.1.0`.

Keeping the database outside the installation folder is what makes an update boring, which
is the only acceptable behaviour for a tool that holds someone's measurement history.
