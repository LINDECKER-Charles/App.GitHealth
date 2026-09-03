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
| 2026-09-02 | [Watching an analysis run, reference by reference](2026-09-02-analysis-run-scene.md) | `feat` | Live ledger, topology drawing and `git` console |
| 2026-09-02 | [Asking a local agent about a capture](2026-09-02-local-agent-assistant.md) | `feat` | An installed CLI reads the capture and answers |
| 2026-09-02 | [The agent connects to GitHealth, and the conversation is kept](2026-09-02-agent-bridge-and-conversations.md) | `feat` | A tool bridge, stored threads, consent per repository |
| 2026-09-03 | [Watching the agent work](2026-09-03-watching-the-agent-work.md) | `feat` | The steps of a run, shown while it runs and never stored |

## Watch out when releasing

- the English pass renames CI job names that double as **required status checks** — branch
  protection has to be updated in the repository settings, see
  [its entry](2026-08-31-english-interface-and-i18n.md#consequences);
- the baseline list ships an **additive migration** that backfills the existing reference as
  the primary baseline;
- the `analyses/:analysisId` route is **removed**, replaced by `?capture=` on the repository;
- `IRepositoryScanner.ScanAsync` takes an **`IProgress<RepositoryScanEvent>`** in place of the
  stage-only channel, see [its entry](2026-09-02-analysis-run-scene.md#consequences);
- the assistant is the **first feature that reaches a network**. It is opt-in per repository
  and removable with `GitHealth:Assistant:Enabled=false`, but the "works offline" line in the
  README and the architecture now carries an exception, see
  [its entry](2026-09-02-local-agent-assistant.md#consequences);
- the assistant ships an **additive migration** adding `Projects.AssistantConsentAtUtc` and the
  two conversation tables. Every existing repository starts with no consent granted, so the
  panel asks once before the first question;
- `GitHealth:Assistant:MaximumOutputBytes` **means something else** and its default rises to
  4 MiB: it bounds a whole exchange with the agent rather than the answer it prints. An
  installation that pinned the old value has to raise it, see
  [its entry](2026-09-03-watching-the-agent-work.md#consequences);
- assistant conversations now **land in the exportable SQLite file** — questions, answers,
  branch names, which agent answered and the redacted command lines. That reverses a decision
  documented in the previous assistant entry, and it changed what a backup exposes, see
  [its entry](2026-09-02-agent-bridge-and-conversations.md#consequences).
