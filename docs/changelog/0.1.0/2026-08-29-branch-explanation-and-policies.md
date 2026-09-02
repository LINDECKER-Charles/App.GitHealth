# Explaining a branch and configuring the policies

- **Type** — `feat`
- **Scope** — `front`, `api`
- **Landed** — 2026-08-29
- **Commits** — `a364b56`

## What shipped

Every recommendation becomes verifiable. The branch detail states the definition of ahead,
behind and activity, lists the contributors with their commit counts and says whether
mailmap was applied, reports an attribution made impossible by a merge instead of guessing,
and shows the SHAs used and the capture time.

The policies become editable: the active/ageing/inactive thresholds, and the exclusion and
protection patterns with a preview of what they match. Quick filters cover merged,
inactive, diverged and to-review. A CSV export of the filtered view is added, distinct from
the SQLite backup, and a project's analysis history becomes browsable.

## Why

A verdict nobody can check is a verdict nobody will act on. Displaying the definition next
to the number, and the SHAs next to the comparison, is what makes the recommendation
arguable rather than authoritative.

Changing a policy recomputes the interpretation without touching the Git facts, and a
historical analysis stays attached to the policies in force when it was captured — so
tightening a threshold today cannot rewrite what last week's snapshot said.
