# Projects and analyses that survive a restart

- **Type** — `feat`
- **Scope** — `api`
- **Landed** — 2026-08-28
- **Commits** — `04eca9b`

## What shipped

EF Core over SQLite and the first schema: projects, analyses, branch snapshots and
contributors, every date stored in UTC and every reference under its full name. Foreign
keys, WAL and a write timeout are enabled explicitly. A run and its snapshots are written
in transactional batches, and only a completed analysis is promoted as the last successful
result.

The export service goes through the SQLite backup API, so a copy taken while the
application runs opens on its own without a separate WAL file. A retention policy exists,
configurable and disabled by default. Relocating a path that has become unreachable is
handled at this layer.

## Why

An interrupted scan must corrupt neither the previous scan nor the database, which is what
the transactional batch and the "promote only when complete" rule buy: the last good
snapshot stays visible when a new analysis fails. Taking the backup through SQLite's own
API rather than copying the file removes the inconsistent-copy risk instead of documenting
it.
