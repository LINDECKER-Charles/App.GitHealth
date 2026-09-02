# Choosing a branch instead of typing it

- **Type** — `feat`
- **Scope** — `front`
- **Landed** — 2026-08-30
- **Commits** — `c91d6c4`

## What shipped

In the Policies tab, "Choose…" opens a picker listing the repository's references,
searchable and navigable from the keyboard, marking the ones an existing pattern already
covers. A picked branch is added as an exact pattern. The text field stays for globs such
as `refs/heads/release/*`. If the repository is out of reach, the list falls back to the
last capture and says so.

## Why

The text field required knowing the exact form of a reference — a full name, with the
right prefix — which is precisely the knowledge the product is supposed to save. The field
is kept rather than replaced because no list can offer a wildcard pattern.

Marking already-covered references matters more than it looks: adding a second pattern
that matches the same branch is silent, and produces a policy nobody can read afterwards.
