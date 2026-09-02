# A shortened activity scale for branches with no own commits

- **Type** — `feat`
- **Scope** — `core`
- **Landed** — 2026-08-29
- **Commits** — `60cc1dd`

## What shipped

A branch that adds nothing to its baseline — merged into it, or pointing at the same commit
— follows its own activity scale: ageing after 7 days, inactive after 30, instead of the
project's general thresholds. It is never recommended as "Keep", and a merged branch whose
deadline is still running gets the dedicated "Done" recommendation
(`RecommendationKind`), shown in purple rather than as a warning.

The classifier states the reason with a shared "no own commits" prefix, which the scale
tests match on (`MergedBranchScaleTests`).

## Why

The general thresholds answer "is this branch still alive". For a branch with no commits of
its own the question is different: it is finished, and what remains is whether anyone has
cleaned it up. Judging it on the same scale as active work made merged branches look
healthy for weeks, which is exactly the noise the product is meant to remove.

"Done" is a separate verdict rather than a variant of "delete" because it names a state
reached, not an action still owed.
