# 0.2.0 — an assistant, a visible run, and several ways to read a repository

- **Released** — 2026-09-14
- **Tag** — [`v0.2.0`](https://github.com/LINDECKER-Charles/App.GitHealth/releases/tag/v0.2.0)
- **Range** — `c91d6c4` … `v0.2.0`, 62 commits without the merges
- **Reader's summary** — [`CHANGELOG.md`](../../../CHANGELOG.md#020---2026-09-14)
- **Acceptance report** — [`docs/release/0.2.0.md`](../../release/0.2.0.md)

Two weeks after the MVP, the product stops being one table read one way. A repository now
declares several baselines rather than a single reference branch, so `dev`, `test` and
`main` are measured side by side instead of overwriting one another; the capture being read
is named in the header, carried in the URL and obeyed by every tab; a "Visualisation" tab
answers the two questions the table never could — what does this repository look like, and
what moved since last week; the branches filter on the author of their tip commit and carry
their top contributor; a capture or a whole repository can be deleted; and a reference is
picked from a list rather than typed from memory, with the text field kept for the glob
patterns no list can offer. The analysis itself stopped being a bar that fills: launching
one hands the Diagnostic tab to the run, which names every reference as it reads it, draws
the topology as it lands, and lists every Git command it runs in a `git · read only`
console. That console is the point — the promise that GitHealth only ever reads used to ask
to be believed, and it is now something a reader checks.

The other half of this version is the **Assistant**: a panel beside the branch table where
an agent already installed on the machine — Claude Code or Codex CLI — answers a question
about the capture in plain language, and says what it is doing while it does it. It is not
handed the table. GitHealth opens a single-use, read-only door onto the capture and the
agent queries it, so it reads what the question needs rather than a page someone truncated
for it. Permission is asked once per repository, stored on the project row and enforced by
the API rather than by the screen; the thread of questions and answers is kept in the local
database, listed, reopenable and deletable one thread at a time or all at once. That has a
price. This is the first and only feature that reaches a network, it is billed to the
user's own account with the agent's provider, it can be removed from an installation
outright, and the conversations it keeps travel in the exportable SQLite file alongside the
captures.

Underneath both, the application was translated: every label, empty state, toast, error
message, log template, build script and workflow job name is written in English, and every
user-facing message carries an explicit id — 797 of them by the time this version shipped —
so another locale is a catalogue rather than a rewrite. The macOS bundle also carries
GitHealth's own icon, in place of the grey placeholder `0.1.0` shipped with. It is still
a `0.x` — the public contract is not frozen,
the routes and the settings below moved in this version and may move again, and a later
minor version may still break them.

## Entries

| Landed | Entry | Type | What it delivers |
|---|---|---|---|
| 2026-08-30 | [Choosing a branch instead of typing it](2026-08-30-branch-picker.md) | `feat` | A picker over the repository's references |
| 2026-08-30 | [The "Visualisation" tab](2026-08-30-visualisation-tab.md) | `feat` | Topology map, activity register, drift |
| 2026-08-30 | [A capture selector on the repository](2026-08-30-capture-selector.md) | `feat` | One capture read by every tab, carried in the URL |
| 2026-08-31 | [The application speaks English, and is ready for other locales](2026-08-31-english-interface-and-i18n.md) | `refactor` | 464 translatable messages, locale-aware formats |
| 2026-09-02 | [A journal behind the changelog, version by version](2026-09-02-changelog-journal.md) | `docs` | One folder per version, one file per implementation |
| 2026-09-02 | [Several comparison baselines per repository](2026-09-02-multiple-baselines.md) | `feat` | `dev`, `test` and `main` measured side by side |
| 2026-09-02 | [Deleting a capture or a repository](2026-09-02-deleting-captures-and-repositories.md) | `feat` | Pruning a history, removing a project |
| 2026-09-02 | [Filtering the branches by author, and the top contributor](2026-09-02-author-filter-and-top-contributor.md) | `feat` | "Whose branch is this", answered at a glance |
| 2026-09-02 | [Watching an analysis run, reference by reference](2026-09-02-analysis-run-scene.md) | `feat` | Live ledger, topology drawing and `git` console |
| 2026-09-02 | [Asking a local agent about a capture](2026-09-02-local-agent-assistant.md) | `feat` | An installed CLI reads the capture and answers |
| 2026-09-02 | [The agent connects to GitHealth, and the conversation is kept](2026-09-02-agent-bridge-and-conversations.md) | `feat` | A tool bridge, stored threads, consent per repository |
| 2026-09-03 | [Watching the agent work](2026-09-03-watching-the-agent-work.md) | `feat` | The steps of a run, shown while it runs and never stored |
| 2026-09-03 | [An effort refused the same way on every machine, and an agent free to exit early](2026-09-03-assistant-run-corrections.md) | `fix` | An effort judged against the catalogue, not the machine |
| 2026-09-14 | [GitHealth carries its own icon on macOS](2026-09-14-macos-application-icon.md) | `fix` | An `.icns` in the bundle, in place of the packager's default |
| 2026-09-14 | [Dependency refresh, and the two majors the interface could not take](2026-09-14-dependency-updates.md) | `build` | The .NET line to 10.0.12, TypeScript 7 and Node 26 held back |

## Breaking changes and migrations

- **The CI job names that double as status checks were renamed.** The English pass turned
  "Vérifier le socle" into "Verify the baseline", "Auditer les dépendances" into "Audit
  dependencies" and "Examiner les dépendances" into "Review dependencies" — the last two
  stay two distinct contexts. Nothing broke at release, because `dev` and `main` carry no
  required status check and the repository carries no ruleset; the names matter the day
  someone enables them, and those are the contexts to name. See
  [its entry](2026-08-31-english-interface-and-i18n.md#consequences).
- **The baseline list shipped an additive migration.** It backfills the existing reference
  as the primary baseline and carries its latest capture over, so a repository saved before
  this version keeps its whole history and needs no human step. See
  [its entry](2026-09-02-multiple-baselines.md#consequences).
- **The `analyses/:analysisId` route was removed.** A capture is opened through `?capture=`
  on the repository instead, which is what the history's "Open this snapshot" now points at.
  A bookmark on the old route no longer resolves. See
  [its entry](2026-08-30-capture-selector.md).
- **`IRepositoryScanner.ScanAsync` changed signature.** It takes an
  `IProgress<RepositoryScanEvent>` where it took an `IProgress<RepositoryScanStage>`, and the
  stage-only overload is gone. An out-of-tree implementation has to be recompiled. See
  [its entry](2026-09-02-analysis-run-scene.md#consequences).
- **The offline promise gained its first exception.** The assistant is the only feature that
  reaches a network: it calls the agent's provider, it is opt-in per repository, it is billed
  to the user's own account, and an administrator can remove it from an installation with
  `GitHealth:Assistant:Enabled=false`, after which no interface can turn it back on. The
  README and the architecture now carry that exception. See
  [its entry](2026-09-02-local-agent-assistant.md#consequences) and
  [the entry that moved consent onto the project row](2026-09-02-agent-bridge-and-conversations.md#consequences).
- **The assistant shipped an additive migration** adding `Projects.AssistantConsentAtUtc`
  and the two conversation tables. Every repository that existed before this version starts
  with no consent granted — including one that used the earlier assistant, since there was
  nothing per repository to migrate — so the panel asks once before the first question. See
  [its entry](2026-09-02-agent-bridge-and-conversations.md#consequences).
- **`GitHealth:Assistant:MaximumOutputBytes` means something else, and its default rose to
  4 MiB.** It bounded what an agent printed as an answer; it now bounds a whole exchange,
  every event and every capture row a tool call sends back through the stream. An
  installation that pinned the old value in `appsettings.json` has to raise it, or a run
  reading a large capture is stopped mid-answer. See
  [its entry](2026-09-03-watching-the-agent-work.md#consequences).
- **Assistant conversations land in the exportable SQLite file.** `GET /api/exports/database`
  and any copy of the database now carry, per thread, the questions as they were typed, the
  answers as the agent wrote them — branch names and tip author names included — which agent
  answered, the effort it was asked for, the command line with its token blanked, the
  durations and the failure messages. Contributor email addresses stay excluded by
  construction and the token never lands in the file. This reverses the decision documented
  in the first assistant entry, and it is a real addition to what a backup exposes:
  **Policies → Assistant → Delete every conversation** is how a history is emptied before a
  copy leaves the machine. See
  [its entry](2026-09-02-agent-bridge-and-conversations.md#consequences).

## Known limitations at release

Nothing is fetched from a forge, Git has to be installed separately, the product remains
local and single-user, there is no in-app update on Linux, and neither the Windows installer
nor the macOS archives and `.pkg` are signed or notarised — the icon is fixed, the signing is
not. The assistant adds limitations of its own: it needs an agent command line already
installed, so it is unavailable under Docker; it is the one feature that reaches a network;
only one run goes at a time, and a second question is refused rather than queued; an answer
does not stream in, it arrives whole; and its conversations travel in the exportable database
until they are deleted. See [`KNOWN_LIMITATIONS.md`](../../KNOWN_LIMITATIONS.md).
