# Several comparison baselines per repository

- **Type** — `feat`
- **Scope** — `api`, `front`, `docs`
- **Landed** — 2026-09-02
- **Commits** — `e28f034`, `817fc4c`, `a7f13fe`, `ec038bd`

## What shipped

A project no longer declares a single reference branch but an ordered list of up to eight —
`dev`, `test` and `main` side by side. Each baseline keeps its own analyses and its own
history, so switching between them reads a different capture rather than a re-filtered one.

The repository header carries a baseline selector when there is a choice to make, and keeps
the plain badge when there is not. The selection travels in the URL (`?baseline=`) and every
tab link merges it rather than dropping it on the way; the capture history and its cache
are keyed by baseline too, and switching drops a requested capture that belonged to the
other baseline's history. The first baseline in the list is the primary one, shown by
default.

The list is edited from the Policies tab, through the same branch picker the patterns use,
and saved on its own route so a baseline edit can never reset the thresholds or the
patterns. Running an analysis measures every declared baseline in one click, as independent
runs; the analysis queue is keyed by baseline instead of by project.

## Why

Seeing how far a branch had drifted from `dev` and from `main` at once was impossible: the
second reading overwrote the first. A baseline is identified by its reference name rather
than by a position, which is what lets the list be reordered without detaching a baseline
from the captures already taken against it. The primary one stays denormalised on the
project row, so every reader that knows nothing of the list keeps working.

Measuring each baseline as a separate run costs one scan per baseline, and buys failure
isolation — a baseline pointing at a deleted branch fails alone — and a full timeout for
each rather than one shared budget.

## Consequences

The migration is additive: it backfills the existing reference as the primary baseline and
carries its latest capture over, so repositories saved before this change keep their whole
history.
