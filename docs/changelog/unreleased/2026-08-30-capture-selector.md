# A capture selector on the repository

- **Type** — `feat`, `test`
- **Scope** — `front`
- **Landed** — 2026-08-30
- **Commits** — `d422304`, `600f520`

## What shipped

The repository header carries which capture is being read, and every tab obeys it —
Diagnostic, topology map, activity register, CSV export and the command palette. It
defaults to the most recent capture and names it as such in the list.

Picking an older one is announced, and its patterns cannot be edited from that view. The
choice travels in the URL (`?capture=`), which makes it shareable and survives a reload;
the history's "Open this snapshot" now points there, replacing the `analyses/:analysisId`
route. Launching an analysis returns to the most recent capture. Drift keeps its own
two-capture range, since it compares rather than reads. `ProjectContext.snapshot` becomes
`latestSnapshot`, so nothing confuses "the latest" with "the one being looked at".

## Why

A past capture is re-read with the policy and the clock of its own day, while the most
recent one follows today's: the facts are the same, the verdicts are not. Editing patterns
from a past capture would show verdicts that never existed, so the view forbids it rather
than warning about it.

Replacing the `analyses/:analysisId` route removes a second path to the same thing — two
ways to open one capture is two behaviours to keep in step.

## Consequences

The capture labels are asserted against the machine's local time in the tests: they were
built in UTC and compared to Paris-time labels, so they only passed in that timezone and
failed on CI, which runs in UTC.
