# Filtering the branches by author, and the top contributor

- **Type** — `feat`
- **Scope** — `api`, `front`, `docs`
- **Landed** — 2026-09-02
- **Commits** — `8c79e21`, `817fc4c`, `48899ce`, `ec038bd`

## What shipped

The Diagnostic tab filters on the author of each branch's tip commit, which answers "whose
branch is this" without reading the table row by row. The list of names is derived from the
capture being read, so it offers exactly the people who appear in it, and the filter
combines with the existing facets and clears from a chip like the others.

Branch snapshots also carry the top contributor — whoever wrote most of the commits the
branch adds to its baseline. It is null on a merged branch, which adds none.

## Why

Contributors were already read and stored per branch, but only the branch detail exposed
them, so a list of branches could not say who owned one. The commit-count leader now
travels with every snapshot at no query cost: the contributors are already eager-loaded for
the list endpoints.

The filter matches the **tip author** rather than the top contributor because the tip author
is populated for every branch, including merged ones. Filtering on the contributor would
silently drop the whole "Done" bucket.
