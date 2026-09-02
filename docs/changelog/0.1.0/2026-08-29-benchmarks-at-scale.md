# Measuring analyses at scale

- **Type** — `perf`
- **Scope** — `api`
- **Landed** — 2026-08-29
- **Commits** — `586b001`, `ce4aa6b`

## What shipped

A reproducible benchmark over repositories of 100, 500 and 1,000 branches, measuring
topology, enrichment, persistence and rendering separately instead of reporting a single
total. The budgets are set from the first run and versioned in the repository
(`docs/BENCHMARKING.md`, `benchmarks/`), then the Windows baseline is refreshed so the
published figures match the code that shipped.

## Why

"Performance measurements precede any complex optimisation" is an execution rule of the
plan: without a versioned baseline, a regression is an impression. Splitting the phases is
what makes a regression actionable — a slowdown in enrichment and a slowdown in
persistence do not have the same cause and would not have the same fix.
