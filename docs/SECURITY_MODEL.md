# Security model

## Purpose and trust boundary

GitHealth helps a local user examine potentially untrusted repositories. It must neither
modify those repositories nor expose their content or author identities outside the
machine. The API and the interface belong to the same process and the same origin.

The browser, the GitHealth process and the child Git commands all run with the rights of
the current account. Malicious software that already holds those same rights can read
the files the account can reach, and is therefore not stopped by GitHealth.

## Protected assets

- references, objects, index, worktree and reflogs of the analysed repositories;
- author names and addresses present in the history;
- the SQLite database, the policies, the snapshots and the stored assistant conversations;
- the single-run tokens the agent bridge is reachable with;
- the local machine's compute capacity;
- the integrity of the distributed archives.

## Untrusted inputs

- repository paths and symbolic links;
- reference names, authors, messages and the repository's Git configuration;
- output and duration of Git processes;
- HTTP requests issued by another site or a local process;
- the JSON-RPC bodies an agent CLI posts to the bridge, and the text it writes back as an
  answer;
- launcher configuration, environment variables and Docker mounts.

## HTTP controls

The native launcher and Compose listen on loopback only. Every application request must
carry a loopback `Host`. The `/api` routes reject a foreign origin and a cross-site
`Sec-Fetch-Site` context.

One route sits outside `/api` on purpose: `POST /agent-bridge/{token}`, the tool bridge an
agent CLI reads a capture through. That prefix is the browser's — a mutation on it must
carry the session cookie and the anti-forgery token, and a command-line process has neither.
Relaxing the guard for one route would have relaxed it for the browser too, so the bridge
authorises on something else entirely: the single-run token in its path, and nothing else.
The loopback `Host` check still applies to it. It is served on the port the interface is
already bound to, so the feature opens no new listener.

An HTML navigation, or the `GET /api/session` bootstrap used by the Angular development
server, creates a random in-memory session and a pair of anti-forgery tokens. The session
and anti-forgery cookies are `HttpOnly`, `SameSite=Strict` and `Secure` over HTTPS.
Angular only reads the dedicated `XSRF-TOKEN` cookie and echoes it back in `X-XSRF-TOKEN`
for mutating requests. A session with no activity expires after twelve hours.

Every response receives a CSP restricted to the same origin, along with
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`
and a restrictive `Permissions-Policy`. `base-uri` is `'self'`: the application declares
`<base href="/">`, and forbidding it entirely made every deep link unreadable on reload.
The threat it targets — an injected `<base>` pointing at another origin — remains
blocked. Angular's critical CSS inlining is disabled for the same reason: the `onload`
handler it generates is an inline script, which `script-src 'self'` rejects. OpenAPI is
only published in development. `/health` stays deliberately public on loopback, for smoke
tests.

These protections do not turn GitHealth into a network service. Do not add a LAN
listener, a reverse proxy or non-loopback origins without designing real authentication
and distributed session storage.

## Git isolation

Commands are launched directly with `ProcessStartInfo.ArgumentList`, without a shell and
with standard input closed. Values coming from repositories stay separate arguments,
including when they start with a dash or contain command characters.

Each process gets:

- a 30-second timeout by default, configurable only between 1 and 120 seconds;
- a shared output budget of 4 MiB by default, bounded between 1 KiB and 16 MiB;
- a concurrency of four commands by default, bounded between one and eight;
- cancellation of the whole process tree on overrun or shutdown.

GitHealth neutralises credential helpers, protocols, maintenance, garbage collection, the
`GIT_TRACE*` variables and the main `GIT_*` variables able to redirect objects, the index,
the worktree, SSH or the global configuration. `GIT_OPTIONAL_LOCKS=0`,
`GIT_NO_LAZY_FETCH=1` and `GIT_TERMINAL_PROMPT=0` keep the scan non-interactive and
read-only.

## Paths and container

In native mode, the user chooses the repositories their own account can reach. Under
Docker, the canonical path, the worktree, the Git directory, its `commondir` and every
object database — including nested alternates — must stay physically under
`/repositories`. Symbolic link components are resolved before that check.

Compose mounts `/repositories` read-only, runs the process with an unprivileged UID,
makes the container filesystem non-writable and reserves only `/data` and `/tmp`. The
`no-new-privileges` option is enabled.

The database, its WAL/SHM files and its instance lock are created with private
permissions where the system allows it. A data directory created by GitHealth is private;
the permissions of a pre-existing parent directory are never modified. A SQLite backup is
always requested explicitly by the user.

## Privacy and outbound communication

The application code creates no outbound HTTP client, contains no telemetry SDK and
embeds no third-party web resource. The CSP also restricts the browser's connections to
the same origin. The Playwright scenario fails if it observes an HTTP request to any host
other than loopback.

Author names and addresses are stored in SQLite and can appear in the CSV or in the
backup the user requests. Those files must be protected like business data. Once the
assistant has been used, that database also holds the questions asked and the answers given
— see below.

### The agent assistant

One feature deliberately sends data off the machine, and it is the only one. The
**Assistant** panel starts an agent CLI the user installed themselves; that process calls its
own provider, over the user's own account. GitHealth holds no key and opens no connection of
its own — the exception is a child process, not an HTTP client in this code.

#### Permission, per repository

Sending a repository's captures has to be allowed once, on that repository. The permission is
a moment stored on the project as `AssistantConsentAtUtc`, and it is the **API** that
enforces it: a run started without it is refused with `403` and `assistant.consent_required`
before any process is created. The panel asking first is a courtesy; it is not the control.
The permission is withdrawn from **Policies** → **Assistant**, and withdrawing it deliberately
leaves the stored conversations alone — "stop sending" and "forget what was already said" are
two decisions, on two buttons.

#### What the agent can reach, and for how long

The agent is no longer handed the capture as text. GitHealth opens a Streamable-HTTP MCP
server at `POST /agent-bridge/{token}` and the agent queries it. What that bridge exposes is
exactly four read-only tools over **one** capture already in the database:

- `get_capture` — repository name, baseline, capture date, branches measured, readable and
  omitted, the thresholds and patterns in force, and how to read a row;
- `list_branches` — the measured branches, filterable and paged, each row carrying its
  reference name, ahead, behind, relationship, last-commit day, topology, activity,
  GitHealth's verdict and reason, the protected and excluded flags, and the tip author's
  **name**;
- `get_branch` — the same fields for one named branch;
- `count_branches` — counts grouped by verdict, topology, activity or author.

Contributor email addresses are excluded by construction and appear in none of them. There is
no tool that runs Git, reaches the file system, names a project or writes anything: a tool
call is answered from the capture held in the session, with no database read behind it, so a
call cannot widen what it sees.

The whole of that capture is rendered in the panel, behind **What it can query**, so the
permission is granted against what is reachable rather than against a description of it. That
preview names the four questions the bridge answers and says plainly that nothing is handed
over as a document: it describes a surface the agent may interrogate, not a text that is
sent.

#### The token

One run gets one token: 256 bits of cryptographic randomness, drawn when the run starts,
bound to that run's capture. It carries no privilege of its own and names no project — it is
the authorisation in full, which is why it is worth nothing to anybody else. It is closed the
moment the run settles, whether it answered, failed or was stopped, so a capture is reachable
only while the agent started to read it is still running. A twenty-minute expiry is a backstop
for a run that never settles, not the normal path; it is longer than the longest allowed run,
so it never cuts off a legitimate agent.

The token travels inline on the agent's command line — as a `--mcp-config` document for
Claude Code, as a `-c mcp_servers=…` override for Codex — and therefore never lands in a
configuration file. The command line is shown in the interface and stored with the
conversation, in both cases with the token replaced by `<single-use-token>`. The shape of the
command is what makes this feature auditable and is kept whole; the secret is the one part of
it worth hiding.

#### Containment of the child process

- it is started in an empty temporary directory created for the run and deleted after it,
  never in the analysed repository, so it has nothing to read on disk;
- the prompt travels on standard input and is instructions plus the question — the branch
  table is no longer in it;
- output is bounded, the run is capped by a timeout, and the process tree is killed on
  cancellation or overrun;
- only identifiers from a fixed catalog resolve to an executable. A request naming anything
  else is refused, so this endpoint is not a way to run an arbitrary command;
- the effort level ends up inside a command line, so it is allowlisted against what the agent
  declares; an unsupported one is refused rather than downgraded.

**Tool isolation differs by agent, and the difference is real.**

- **Claude Code** runs with `--tools ""`, which removes every built-in tool — no shell, no
  file read, no network — and `--allowedTools mcp__githealth`, which grants back only
  GitHealth's own. `--strict-mcp-config` drops the machine's own MCP servers, so the inline
  declaration is the only one in force. Verified on Claude Code 2.1.220: the single thing such
  a run can do is read the capture it was started for. This replaces `--permission-mode plan`,
  and is a *narrower* grant than it was: plan mode withholds edits rather than removing the
  tools, and it refuses MCP tools outright, so it cannot coexist with the bridge at all.
- **Codex CLI cannot be constrained that far, and GitHealth does not pretend otherwise.** It
  runs with `--sandbox read-only` and `--skip-git-repo-check`, and the whole `mcp_servers`
  table is *replaced* rather than added to, so servers declared in the user's own
  configuration are not carried into the run. That is the nearest equivalent to
  `--strict-mcp-config`, and it is not equivalent: tools this CLI gets from its own plugins
  and connectors stay reachable, and no flag removes them. Pointing it at an isolated
  `CODEX_HOME` would hide them — and would hide the credentials the run needs in the same
  move. So GitHealth serves this agent one capture and nothing else, but it cannot promise
  that GitHealth is the only thing the agent can reach.

`GitHealth:Assistant:Enabled=false` removes the feature from an installation; no interface
can re-enable it. The environment handed to the agent is deliberately **not** scrubbed, since
that is where its credentials live — a consequence accepted for a process the user already
runs themselves.

#### What is persisted, and how to remove it

Conversations are kept, in the local SQLite database. This reverses an earlier decision to
hold runs in memory only; the reasoning is in
[the changelog entry](changelog/unreleased/2026-09-02-agent-bridge-and-conversations.md#why),
and the short form is that a record the user cannot see is also a record they cannot delete.

Two tables hold them — `AssistantConversations` and `AssistantMessages` — and a thread hangs
off the `AnalysisRuns` row it read, `ON DELETE CASCADE`. Per thread, the database therefore
carries:

- the questions as they were typed;
- the answers as the agent wrote them, **branch names and tip author names included**;
- which agent answered and at which effort;
- the command line that produced each answer, with its token blanked;
- durations, statuses, and the failure messages of runs that produced no answer.

The bridge token is never among them. Contributor email addresses are still excluded by
construction.

**This is an addition to what a backup exposes.** `GET /api/exports/database` and any copy of
the SQLite file carry all of the above. Three ways remove it, and all three are real deletes
with no archive and no soft delete behind them:

- deleting a capture deletes every conversation about it;
- deleting one thread from the panel's conversation list;
- **Policies** → **Assistant** → **Delete every conversation**, which empties one
  repository's history at once and reports how many went.

## Supply chain

CI builds and tests .NET, Angular and the end-to-end journey. A separate workflow runs
CodeQL, dependency review and the NuGet/npm audits. Dependabot tracks actions, NuGet
packages, npm packages and Docker images.

Publishing generates a SHA-256 checksum and an SPDX SBOM. For a public repository — or a
private repository with GitHub Enterprise Cloud and an explicit opt-in — it also adds
GitHub provenance and SBOM attestations. These artefacts allow the archive to be
verified, but do not replace code signing or macOS notarisation.

## Residual risks

- software running as the same user can reach the repositories and SQLite without going
  through the API;
- a vulnerability in Git or in the runtime stays exploitable until it is patched;
- the macOS archives are neither signed nor notarised;
- an export copied off the machine escapes GitHealth's controls — including the assistant
  conversations it now contains, which is what the purge above is for;
- an agent run leaves the machine by design: what its provider retains, and what the CLI
  itself logs on disk, are outside GitHealth's control;
- the read-only containment of a run relies on flags the agent CLI honours; a change of
  behaviour on its side would not be detected here, and the flags were verified against the
  versions installed at the time — Claude Code 2.1.220 for the tool restriction;
- Codex CLI keeps the tools its own plugins and connectors give it for the duration of a run.
  GitHealth serves it one capture; it does not control what else that process can reach;
- a bridge token is a bearer secret for the length of one run. Anything already running as
  the same user could read it from the agent's command line — the same software that could
  read the SQLite database directly;
- an agent's answer is untrusted text. It is parsed into a typed tree and rendered through
  Angular bindings, never `innerHTML`, and a link whose target is not `http`, `https` or
  `mailto` stays inert text;
- local references can be stale, because no `fetch` is ever automatic.
