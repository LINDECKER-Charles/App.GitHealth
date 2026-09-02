# Unreleased

Merged, not attached to a version yet. On the day a version is published these files move
as they are into `docs/changelog/<version>/`, keeping their names, and are synthesised into
the `[<version>]` section of the root [`CHANGELOG.md`](../../../CHANGELOG.md).

- **Since** — `v0.1.0` (2026-08-30)
- **Range** — `c91d6c4` … `ec038bd`, 20 commits
- **Reader's summary** — [`CHANGELOG.md`, section `[Unreleased]`](../../../CHANGELOG.md#unreleased)

## Entries

| Landed | Entry | Type | What it delivers |
|---|---|---|---|
| 2026-08-30 | [Choosing a branch instead of typing it](2026-08-30-branch-picker.md) | `feat` | A picker over the repository's references |
| 2026-08-30 | [The "Visualisation" tab](2026-08-30-visualisation-tab.md) | `feat` | Topology map, activity register, drift |
| 2026-08-30 | [A capture selector on the repository](2026-08-30-capture-selector.md) | `feat` | One capture read by every tab, carried in the URL |
| 2026-08-31 | [The application speaks English, and is ready for other locales](2026-08-31-english-interface-and-i18n.md) | `refactor` | 464 translatable messages, locale-aware formats |
| 2026-09-02 | [Several comparison baselines per repository](2026-09-02-multiple-baselines.md) | `feat` | `dev`, `test` and `main` measured side by side |
| 2026-09-02 | [Deleting a capture or a repository](2026-09-02-deleting-captures-and-repositories.md) | `feat` | Pruning a history, removing a project |
| 2026-09-02 | [Filtering the branches by author, and the top contributor](2026-09-02-author-filter-and-top-contributor.md) | `feat` | "Whose branch is this", answered at a glance |

## Watch out when releasing

- the English pass renames CI job names that double as **required status checks** — branch
  protection has to be updated in the repository settings, see
  [its entry](2026-08-31-english-interface-and-i18n.md#consequences);
- the baseline list ships an **additive migration** that backfills the existing reference as
  the primary baseline;
- the `analyses/:analysisId` route is **removed**, replaced by `?capture=` on the repository.
