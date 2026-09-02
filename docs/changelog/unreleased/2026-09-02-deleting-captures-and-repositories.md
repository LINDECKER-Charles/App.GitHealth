# Deleting a capture or a repository

- **Type** — `feat`
- **Scope** — `api`, `front`, `docs`
- **Landed** — 2026-09-02
- **Commits** — `d2b959c`, `cb96970`, `ec038bd`

## What shipped

A capture is removed from its own row in the history, behind an inline confirmation stating
how many branch measurements go with it. Deleting it hands its baseline back the previous
capture rather than leaving the view empty, and the repair happens in the same transaction
as the delete so the pointer can never dangle. Removing the capture being read returns the
view to the latest one.

A repository is removed from a danger zone at the end of the Policies tab, through a dialog
that names it and states that the Git repository is not touched. The delete takes its
baselines, runs, snapshots and contributors with it through the database cascade, and frees
the path to be added again.

A run still being analysed is refused rather than half-removed, and the server's refusal is
surfaced instead of swallowed. A queued run that outlives what it points at is now skipped
rather than logged as an unexpected failure.

## Why

Nothing could be removed once saved: a mistaken repository stayed in the rail for good, and
a history grew with no way to prune it.

Refusing to delete a running analysis is not a convenience: the worker would write its
results behind the delete. And leaving the deleted capture's id in the URL would render the
"never measured yet" empty state, which would be a lie.

The promise that the Git repository is untouched is stated at the moment of deletion rather
than in the documentation — that promise is the whole point of the application, and this is
the one moment a user might doubt it.
