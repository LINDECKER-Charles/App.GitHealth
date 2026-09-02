# Architecture and MVP plan

- **Type** — `docs`
- **Scope** — `docs`
- **Landed** — 2026-08-28
- **Commits** — `1609b68`, `ad997a6`, `c47d38d`, `a857a74`

## What shipped

The repository opens with its reference documents rather than with code:
`docs/ARCHITECTURE.md` describes the domain, the flows and the trust boundary, and
`docs/IMPLEMENTATION_PLAN.md` slices the MVP into nine steps, each with its expected
outcome, its work list, its test matrix and its exit checks. Step 1 is ticked as complete
in the same movement.

## Why

The product's whole promise is that it reads a Git repository and never writes to it. That
constraint has to be stated before any adapter exists, otherwise it becomes a property
someone has to rediscover in the code. Slicing the MVP into demonstrable vertical
increments — each leaving the branch buildable and tested — comes from the same intent:
every step is checkable against its exit criteria instead of against an opinion.

The plan is kept updated afterwards rather than archived, so the gap between what was
planned and what was built stays readable.
