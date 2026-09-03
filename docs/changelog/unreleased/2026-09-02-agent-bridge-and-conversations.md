# The agent connects to GitHealth, and the conversation is kept

- **Type** — `feat`
- **Scope** — `core`, `api`, `front`, `docs`
- **Landed** — 2026-09-02
- **Commits** — not written yet; this entry travels with the commits that land the bridge,
  the conversations and the consent

## What shipped

The assistant stopped being a screen that pastes a table into somebody else's process. It
is now a **panel**, opened from the **Assistant** button in the repository header or with
`⌘J` / `Ctrl+J`, sitting beside the branch table it is talking about. The agent it drives
no longer receives the capture — it **connects to GitHealth and queries it**.

**The bridge.** GitHealth serves a Streamable-HTTP MCP server at
`POST /agent-bridge/{token}`, on the loopback address and the port the interface is already
served on, so nothing new is listening. `GET` on the same route answers `405` — the bridge
pushes nothing on its own — and `DELETE` answers `204`. It publishes four read-only tools
over the capture already in the database:

- `get_capture` — repository, baseline, capture date, branches measured, branches readable,
  branches omitted, the policy in force and how to read a row;
- `list_branches` — the measured branches, oldest activity first, filterable on verdict,
  topology, activity, author, name fragment and the protected/excluded flags, paged with
  `skip` and `take` (50 by default, 500 at most);
- `get_branch` — every measurement held for one branch;
- `count_branches` — how many branches fall in each `verdict`, `topology`, `activity` or
  `author`, over the whole capture rather than over a page of it.

There is no tool that runs Git, reaches the file system or writes anything.

**One run, one token.** Starting a run draws a 256-bit token, binds it to that run's
capture, and closes it the moment the run settles — success, failure or stop. A twenty-minute
grace expiry is a backstop for a run that never settles, not the normal path. The token is
the whole authorisation: it names no project, so a bridge request can only ever read back
the capture the run was started against. The command line shown in the panel, and the copy
kept in the database, carry `<single-use-token>` in its place.

**The prompt is now instructions plus the question.** `AssistantPrompt.Compose` writes the
brief, the tool list and the rules, then the question. The branch table is no longer in it,
so the text handed to the agent no longer grows with the number of branches measured.

**Conversations are kept.** Two tables — `AssistantConversations` and `AssistantMessages`,
added by `20260902163530_AddAssistantConversations` — hold a thread of questions and
answers. A thread hangs off the `AnalysisRuns` row it read, `ON DELETE CASCADE`, so deleting
a capture deletes the conversations about it. Each answer keeps its status, its effort, its
redacted command line, its duration, its failure code and message when it has one, and
whether it was cut short. New routes:

| Method and route | Responsibility |
|---|---|
| `GET /api/projects/{id}/assistant/status` | Consent moment and how many threads are kept |
| `PUT /api/projects/{id}/assistant/consent` | Grant or withdraw, from either screen |
| `GET /api/projects/{id}/assistant/conversations` | The threads of one repository, every baseline |
| `DELETE /api/projects/{id}/assistant/conversations` | Empty that history, reporting how many went |
| `GET /api/assistant/conversations/{id}` | Read one thread back, messages in order |
| `DELETE /api/assistant/conversations/{id}` | Delete one thread |

`POST /api/projects/{id}/assistant/runs` takes an optional `conversationId`, which is what
turns a question into a follow-up.

**Consent is per repository and enforced by the API.** It is a moment, stored on the project
as `AssistantConsentAtUtc`. A run without it is refused with `403` and
`assistant.consent_required`, before any process starts — the panel asking first is a
courtesy, not the control. **Policies → Assistant** shows when it was granted, revokes it,
names the agent that would answer and how many conversations are kept, and offers **Delete
every conversation**.

**Reading an answer.** The panel keeps a thread, lists past conversations behind the clock
icon, starts a new one with `+`, and renders every branch name an answer writes as inline
code into a control that **opens that branch's row** — both the full reference name and its
short form are indexed, so either spelling opens the same row.

**Claude Code's flags changed.** `--permission-mode plan` is gone: plan mode withholds edits
but also refuses MCP tools, so it refuses the bridge. In its place, `--tools ""` removes
every built-in tool and `--allowedTools mcp__githealth` grants back only GitHealth's own.
Verified on Claude Code 2.1.220: the run then has no shell, no file access and no network,
and the single thing it can do is read the capture it was started for.

## Why

GitHealth already holds the measurements. Handing all of them over in one block was the
simplest thing that could work, and it was wrong in three ways at once: the prompt grew with
the repository and got capped at `MaximumBranches` rows, the agent could not ask a second
question of the data, and a count it gave was a count over a table someone had truncated for
it. A tool call fixes all three — the agent reads what the question needs, `count_branches`
counts over the whole capture, and `get_capture` states plainly how many rows were left out.

**Keeping the pasted briefing was the alternative, and it was rejected on what it costs the
answer, not on what it costs the prompt.** A pasted table is a fixed reading: whatever was
cut is unaskable. The preview did not disappear, though: `BriefingWriter` still renders the
whole capture behind **What it can query**, because a user deciding whether to allow this
should see everything that is reachable rather than a description of it. What it no longer
does is read like the prompt it used to be — it is titled *What the agent can query*, it
names the four questions the bridge answers, it says that nothing is handed over as a
document, and it drops the "how to read a row" legend, which was an instruction to the agent
and now lives in the prompt and in `get_capture` where it belongs.

**Enum names are spelt as words.** `BriefingLabel.Words` turns `BranchIsAncestorOfReference`
into `branch is ancestor of reference` and `CleanupCandidate` into `cleanup candidate`.
Lower-casing them whole was defensible while only a model read them; it is not, now that the
preview is a consent screen a person reads. The `list_branches` schema documents the new
spelling of the verdicts.

**`--permission-mode plan` was rejected because it blocks the feature.** It is the flag the
previous version used and the obvious thing to keep. Plan mode refuses MCP tools, so an
agent started that way cannot call the bridge at all. `--tools ""` plus
`--allowedTools mcp__githealth` is not a weaker replacement chosen for convenience — it is a
narrower grant: plan mode withholds edits, this removes the tools.

**The bridge sits outside `/api`, and that is deliberate.** Putting it under `/api` with the
other assistant routes is the obvious placement, and it does not work. That prefix rejects a
foreign origin and a cross-site `Sec-Fetch-Site` context, and every mutation on it must carry
a session cookie and an antiforgery token — a browser has those, a CLI has none of them, and
a POST from a command-line agent is a mutation as far as that middleware is concerned.
Loosening the guard for one route would have weakened the guard for the browser too. `/agent-bridge/{token}` answers to one thing
instead: the single-run token in its path. The loopback `Host` check still applies to it,
like it does to everything the host serves.

**Codex gets a weaker isolation, and it is said out loud rather than glossed.** The whole
`mcp_servers` table is replaced rather than added to, so the servers in the user's own
configuration are not carried into the run — that is the nearest thing Codex has to Claude's
`--strict-mcp-config`. It is not equivalent: tools this CLI gets from its own plugins and
connectors stay reachable, and no flag removes them. **An isolated `CODEX_HOME` was the
obvious answer and was rejected**, because the credentials the run needs live in that same
directory: pointing the CLI at an empty one gives it no plugins and no login either. So
GitHealth serves this agent one capture and nothing else, and does not claim that GitHealth
is the only thing the agent can reach. `AgentCatalog` says exactly that, the consent panel
says it in the user's words, and the security model says it a third time.

**Conversations are now persisted, and the previous entry argued they should not be.** That
paragraph — *"Runs are held in memory only. Persisting them would put branch names, author
names and whatever the user asked into the exportable SQLite database, which is not what
that file is for."* — is reversed here, and it deserves a reason rather than a silent
overwrite.

It was right about the cost and wrong about the alternative. Dropping the exchange after
thirty minutes bought less than it looked like: what the agent CLI writes on disk and what
its provider retains were already outside GitHealth's control, and the security model already
listed both as residual risks. What it did do was leave the user with no way to
re-read what they had been told, no way to ask a follow-up, and — the part that matters —
**nothing to delete**. A record you cannot see is not a record you control. Keeping the
thread in the same database as the captures, hanging off the capture it read, makes it
visible in a list, removable one thread at a time, removable in bulk from the policy screen,
and removable by deleting the capture. The database was already the file holding branch
names and author names; what is new is that the user can now empty part of it on purpose.

**Consent moved from the interface to the project row for the same reason.** A checkbox
ticked per question was a promise the interface made; a `403` from
`AssistantRunService.StartAsync` is one the API keeps. Per repository rather than per
question, because the decision is about a repository — some have branch names worth
protecting, most do not — and asking again every time trains a user to click through it.

**Withdrawing consent leaves the stored threads alone.** "Stop sending this repository's
captures" and "forget what was already said" are two different decisions, and collapsing
them would mean a user who wants the first quietly loses the second. **Delete every
conversation** is the second one, on its own button, with its own count.

**The panel replaced the tab because an answer names branches.** A tab meant leaving the
table to read about it, then leaving the answer to look at a row. Beside the table, a branch
name in an answer is a control that opens the row next to it.

## Consequences

**The exportable database now holds the conversations.** `GET /api/exports/database` and any
copy of the SQLite file carry, per thread: the questions as they were typed, the answers as
the agent wrote them — branch names and tip author names included — which agent answered,
the effort it was asked for, the command line with its token blanked, the durations, and
the failure messages of runs that did not answer. Contributor email addresses
are still excluded by construction, and the token never lands in the file. This is a real
addition to what a backup exposes, and it is the reason the purge exists.

**`docs/SECURITY_MODEL.md` claimed the opposite and had to be corrected.** It said runs were
"held in memory and dropped after thirty minutes: nothing about them enters the exportable
database", and it listed `--permission-mode plan` as Claude Code's containment. Both are now
false. The section has been rewritten around what the bridge exposes, for how long, and the
honest per-agent difference in tool isolation.

**The migration is additive and needs no human step.** `AssistantConsentAtUtc` is added to
`Projects` as nullable, so every existing repository starts with no consent granted and the
panel asks once. `Down` drops both tables and the column.

**Consent is asked once per repository, and existing repositories are all "not allowed"** —
including any that used the assistant before this version, since there was nothing per
repository to migrate.

**No answer streams into the panel any more.** A run in flight draws a spinner, "Reading the
N rows…" and a **Stop** button; the answer is rendered once the run has settled and its
thread has been read back from the database. The live trace the API still serves on
`GET /api/assistant/runs/{id}?from=` is no longer rendered anywhere — `AssistantStore.output`
is left computing it with no template reading it. That trace follows **standard output
only**: `AgentProcessRunner` pumps standard error without a sink, deliberately, since the two
streams share one budget and a chatty log would otherwise starve the answer. What lands on
standard error surfaces only in the failure message of a run that produced no answer, so an
agent that reports its progress there is silent while it works whichever way it reports it.

**The offline exception is unchanged in kind and smaller in volume.** This is still the one
feature that reaches a network, still opt-in, still billed to the user's own account, and
still removable with `GitHealth:Assistant:Enabled=false`. What crosses is now what the agent
asked for rather than the whole table.
