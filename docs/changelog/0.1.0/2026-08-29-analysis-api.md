# The analysis use cases, exposed over HTTP

- **Type** — `feat`, `fix`
- **Scope** — `api`
- **Landed** — 2026-08-29
- **Commits** — `0766667`, `8e16f01`

## What shipped

The API that drives the product: path validation and reference discovery, project creation
and listing, editing the baseline, the thresholds and the exclusions. An analysis is
started against a queue served by a hosted worker, answers `202 Accepted` with a tracking
URL, and publishes its progress — queued, topology, enrichment, persistence, done. Two
simultaneous analyses of the same project are deduplicated rather than run twice.

Snapshots are listed paginated, sorted and filtered server-side, a snapshot's detail
carries its contributors, the SQLite backup has its endpoint, and every expected error is
returned as Problem Details with a stable code.

The endpoints are tested in memory against a real temporary SQLite database and real Git
fixtures, not mocks.

## Why

The interface must never have to explain a technical failure it cannot name, so expected
errors become domain results and Problem Details with stable codes rather than exceptions.
Returning `202` with a tracking URL — instead of holding the request open — is what makes a
scan over a thousand branches survivable from a browser, and what lets the last successful
result keep being served while a new scan runs or fails.
