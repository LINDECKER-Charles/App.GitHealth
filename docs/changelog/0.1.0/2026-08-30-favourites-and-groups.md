# Favourite repositories and groups in the rail

- **Type** — `feat`
- **Scope** — `api`, `front`
- **Landed** — 2026-08-30
- **Commits** — `3b91fed`, `39f8e29`

## What shipped

The rail pins favourite repositories at the top and arranges the others into named groups,
each collapsible in one click. The arrangement — favourite flag, group, order — is
persisted server-side and therefore follows the SQLite backup; only the collapsed state
stays local to the browser. A rail with no favourite and no group keeps its original flat
list.

## Why

The rail becomes unreadable at the exact moment the product becomes useful: after scanning
a folder full of repositories. Ordering is data about the user's work, not a display
detail, so it belongs in the database and travels with the backup. The collapsed state does
not: it is about the window in front of you right now.

Keeping the flat list when nothing is organised means the feature costs nothing to whoever
does not use it.
