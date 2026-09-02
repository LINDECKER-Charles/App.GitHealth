# The analysis domain, independent of Git

- **Type** — `feat`
- **Scope** — `core`
- **Landed** — 2026-08-28
- **Commits** — `40dfaa3`

## What shipped

The metrics and the recommendations exist as pure domain rules, with no knowledge of Git,
SQLite or HTTP: `Project`, `GitRef`, `CommitId`, `BranchComparison`, the activity
thresholds and their validation, the excluded and protected patterns, the scanner
contracts and the domain errors. Topology, activity and recommendation are modelled
separately, and an injectable clock carries every age computation.

The test matrix pins the cases that decide a verdict: ahead and behind both zero, ahead
only, behind only with an ancestor, diverged, no common ancestor as an explicit state
rather than a generic error, thresholds exactly reached versus crossed, a protected branch
never proposed for cleanup, an inactive unmerged branch flagged for review and never for
deletion.

## Why

A recommendation is an interpretation of Git facts, and the two must not be computed in
the same place: the facts are captured once, the interpretation can be replayed with other
thresholds. Keeping the domain free of adapters is what later makes the policy preview
possible — projecting an edited policy onto an existing snapshot without re-reading the
repository.

A Git object identifier is handled without assuming its length, so the domain survives a
repository using a hash other than 40-character SHA-1.
