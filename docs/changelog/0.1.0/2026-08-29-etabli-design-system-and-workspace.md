# The Établi design system and the unified workspace

- **Type** — `feat`, `docs`
- **Scope** — `front`, `docs`
- **Landed** — 2026-08-29
- **Commits** — `053070a`, `b012801`, `5b5fd51`

## What shipped

The Établi design system enters the application: spacing, typography and colour tokens
declined in light and dark, IBM Plex fonts and Lucide glyphs served locally from
`public/ds/`, and a `--status-merged-*` semantic family on the plum ramp so a merged branch
reads as finished rather than as a warning.

The interface is then rebuilt as one workspace instead of a sequence of pages: a repository
rail on the left, tabs inside a project shell, a side panel for a branch's detail, a `⌘K`
command palette reaching a branch, a repository or an action, an add-repository dialog with
its directory browser, and an opening sequence — all dismissable from the keyboard. Dark
theme is remembered. `docs/ART_DIRECTION.md` records the editorial and visual direction
that goes with it.

## Why

Serving fonts and glyphs from the application rather than from a CDN keeps the product
usable offline and keeps its rendering identical to what the tests see; it also makes the
redistributed licences a fact to be documented rather than a dependency to be trusted.

The workspace exists because the journey is not linear: a diagnosis is read, argued with,
filtered and returned to. Pages forced a round trip for each of those; a rail plus tabs
keeps the repository, the capture and the filters in place while the reader moves.
