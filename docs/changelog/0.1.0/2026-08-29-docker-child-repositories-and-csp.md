# Child repositories under Docker, and the `base` tag in the CSP

- **Type** — `fix`
- **Scope** — `api`
- **Landed** — 2026-08-29
- **Commits** — `917d42c`, `439facd`

## What shipped

Two fixes on the container path. A repository mounted as a child of the allowed root is
analysed instead of being refused: the canonical check now accepts a path contained in the
root, which is what a `:ro` mount produces. And the content security policy allows the
`base` tag the Angular application emits, without which the published bundle failed to
resolve its own assets.

## Why

Both are cases where a control was stricter than the threat it was written for. A policy
that blocks the product's own bundle, or a root check that refuses the exact mount the
documentation recommends, does not add security — it moves users towards disabling the
control.
