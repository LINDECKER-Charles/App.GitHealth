# The project journey and the dashboard

- **Type** — `feat`
- **Scope** — `front`
- **Landed** — 2026-08-29
- **Commits** — `a8aaa2a`

## What shipped

The journey a user can complete without knowing a single Git command: a home screen with
the recent projects, adding a repository by typed path or — in native mode — through a
folder browser, the allowed root stated plainly in Docker mode, the detected baseline
offered but changeable, and the provenance of the data shown as local or remote with its
scan time.

Starting an analysis and following its progress by polling, then the branch table: name and
reference space, ahead and behind, merge state, last activity and age, main author when it
can be determined, recommendation and its rationale. Sorting, search, combinable filters
and pagination come with it, the useful filters travel in the URL, and the empty, loading,
error and unreachable-repository views are treated as first-class states.

## Why

The product exists to turn Git facts into a decision, so the table has to be readable
before it is complete: a thousand rows stay navigable without rendering everything in the
DOM, long and non-ASCII names must not break the layout, and no information is ever carried
by colour alone. An analysis error never erases the previous result — a failed scan
degrades the freshness of the answer, not the answer itself.
