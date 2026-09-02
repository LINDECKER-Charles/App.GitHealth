# Asking a local agent about a capture

- **Type** — `feat`
- **Scope** — `core`, `api`, `front`, `docs`
- **Landed** — 2026-09-02
- **Commits** — `9daefad`, `ae7fbfe`, `58a1beb`, `8dca00b`, `b97bf0c`, `11156dc`

## What shipped

An **Assistant** tab per repository. GitHealth looks for the command-line agents already
installed on the machine — Claude Code and Codex CLI — and lets one of them read the capture
that has already been taken, then answer a question about it in plain language.

The screen states what it is doing in that order:

1. the agents found, each with the version it answered and the path it was found at. An
   agent that is absent says where the search looked and what setting points at it, rather
   than being a greyed-out button;
2. **what would leave this machine** — the briefing, readable in full before anything is
   sent: repository name, baseline, capture date, the policy in force and one row per
   branch (ahead, behind, relationship, last commit, topology, activity, GitHealth's own
   verdict and its reason, protected/excluded flags, tip author);
3. a consent checkbox, naming what is sent and to whom it is billed;
4. how hard the agent should think — **Quick**, **Balanced**, **Thorough**, **Very
   thorough** or **Maximum** — next to the agent it applies to;
5. the question, with three suggestions worth one click;
6. the answer, rendered from the Markdown the agent writes — headings, lists, tables, code,
   quotes and links — with the command that was run readable underneath and a **Stop**
   button while it runs.

New routes: `GET /api/assistant/agents` (with `?refresh=true` for an agent installed since
the app was opened), `GET /api/projects/{id}/assistant/briefing`,
`POST /api/projects/{id}/assistant/runs`, `GET /api/assistant/runs/{id}?from=` and
`POST /api/assistant/runs/{id}/cancel`.

The run request carries an `effort`; `GET /api/assistant/agents` publishes the levels each
agent accepts and its default.

New settings under `GitHealth:Assistant` — `Enabled`, `RunTimeout`, `MaximumOutputBytes`,
`MaximumBranches`, `MaximumParallelRuns`, and `Agents:<id>:ExecutablePath` to point at a CLI
the search does not find.

## Why

GitHealth already holds the facts; reading fifty branches and turning them into a decision
is the part that stays manual. An agent is good at exactly that — provided it is reading
measurements rather than guessing at a repository.

**The agent is given the briefing and nothing else.** It runs in an empty scratch directory,
never in the analysed repository, with the prompt on standard input. Both CLIs are launched
in their own read-only mode — `--permission-mode plan` for Claude Code, `--sandbox read-only`
for Codex — and Claude Code additionally with `--strict-mcp-config`, so the machine's own MCP
servers are not reached. This is what keeps the promise in the README true even though the
process running is somebody else's: GitHealth still touches nothing in the repository.

**The catalog decides what runs.** The interface sends an agent identifier, and only an
identifier the catalog knows resolves to an executable. Nothing lets a caller name a command.

**The briefing is shown, not described.** Consent to a summary of what is sent would be
consent to a promise; the panel shows the exact text instead. Contributor email addresses are
deliberately absent from it — they are the most identifying thing GitHealth holds and add
nothing to a reading of the branches.

**Runs are held in memory only.** Persisting them would put branch names, author names and
whatever the user asked into the exportable SQLite database, which is not what that file is
for. They are dropped after thirty minutes.

**Detection leans on the standard directories more than Git's does.** A desktop process
started from the Finder or the Explorer inherits the system's minimal `PATH`, not the one the
user's shell builds — so the CLI installed last week is very often invisible in `PATH` as
this process sees it. `~/.local/bin`, `/opt/homebrew/bin`, `~/.<agent>/local` and the npm
prefix are searched explicitly.

**The effort ladder is shared, not mapped.** Claude Code takes `--effort`, Codex takes a
`model_reasoning_effort` configuration override — but both accept exactly `low`, `medium`,
`high`, `xhigh` and `max`. That was verified against the installed CLIs rather than assumed,
so a level shown in the interface is the level the CLI receives, with nothing lost in
translation. The level ends up inside a command line, so it is allowlisted against what the
agent declares; an unsupported one is refused rather than downgraded, because a run at the
wrong effort costs the user real money. Its position is a declared slot rather than an
append, since Codex reads its overrides as options of `exec` and would silently ignore one
placed after the marker that ends its command.

**The answer is parsed, not injected.** It is Markdown written by a language model, so it is
read into a typed tree of blocks and spans and rendered through Angular bindings, which
escape what they print. There is no `innerHTML` on this path and there must not be one. That
also avoided a parser dependency: the grammar covered is what an agent actually writes, and
a link whose target is not `http`, `https` or `mailto` stays visible as the text it was
written as — neither dropped nor made clickable. The live trace is left raw, because for
Codex it is a log rather than prose.

**Polling, not streaming.** The trace is read back on a 700 ms poll carrying only what
appeared since the last offset, which is the transport every other long-running screen in the
application already uses. Server-sent events would suit token-by-token output better and are
the obvious next step, but they would be a second transport for one screen.

## Consequences

**The offline promise gains an exception, and it is the only one.** Every other feature works
with no network; this one calls the agent's provider. It is opt-in per question, it is billed
to the user's own account, and an administrator can remove it outright for an installation
with `GitHealth:Assistant:Enabled=false` — after which no interface can turn it back on.

**Branch names and author names are personal data leaving the machine.** The consent text
says so. On a repository whose branch names or authors are confidential, the setting above is
the answer.

**Docker gets no assistant.** The container has no agent CLI, so the catalog reports both as
unavailable and the tab explains it. Nothing is installed to fix that.

**One run at a time by default.** `MaximumParallelRuns` is 1: these calls cost the user
money, so GitHealth never fans out on its own. A second run is refused with `409`, not queued.
