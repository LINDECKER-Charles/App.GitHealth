# The "Visualisation" tab

- **Type** — `feat`
- **Scope** — `front`
- **Landed** — 2026-08-30
- **Commits** — `d422304`

## What shipped

Three readings of the same capture, each addressable by its own URL.

The **topology map** draws every branch around the reference, its shape carrying how far
ahead and behind it is; hovering reads a branch, clicking pins its card. The **activity
register** puts time on the axis and the policy in bands, so two sliders re-read every
verdict without writing anything. The **drift between captures** compares two analyses of
the repository as a journal of what moved, grouped by what it demands: degraded, resolved,
new, removed, unchanged.

## Why

The table answers "which branches need attention"; it does not answer "what does this
repository look like" or "what changed since last week". Those are different questions and
they need different shapes, not more columns.

The register's sliders deliberately write nothing: the Policies tab keeps sole ownership of
saving, so exploring a threshold can never change the project by accident. Both sides of a
drift comparison are re-read with the policy and the clock frozen at their own capture,
which is what prevents the view from inventing a verdict change that never happened.
