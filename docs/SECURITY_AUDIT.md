# Security audit — GitHealth `0.2.0`

Date: 14 September 2026
Scope: the `dev` branch at `0.2.0` — the ASP.NET Core API, the Git scanner, the agent
assistant and its bridge, Angular, SQLite, the launchers, the Velopack update path, Docker
Compose and the GitHub Actions pipeline.

This is a revision, not a new audit. The `0.1.0` audit closed by recommending that it be
redone "after a forge integration, a managed clone or an automatic update, as these
features would substantially change the trust boundary". A feature did change the trust
boundary — the local agent assistant — and the update path, present but uncovered at
`0.1.0`, is now stated. It covers the whole `0.2.0` window: everything merged since the
previous audit, which is dated 30 August 2026.

## Summary

No critical or high vulnerability was identified within the audited scope. The product
still honours its local single-user model: loopback listening, `/api` mutations tied to an
anti-forgery session, shell-free Git execution and confined Docker paths.

**The `0.1.0` claim of "no outbound application communication" no longer holds, and is
replaced by a bounded one.** GitHealth's own code still opens no outbound HTTP client for
the analysis, embeds no telemetry SDK and serves no third-party web resource. Three named
exceptions now exist:

1. **The agent assistant.** `POST /api/projects/{id}/assistant/runs` starts an agent CLI
   the user installed themselves, as a child process. That process calls its own provider,
   over the user's own account. GitHealth holds no key and opens no socket of its own — the
   exception is a child process, not an HTTP client in this code. The feature ships enabled
   (see below) and is gated per repository by consent.
2. **The update check.** `src/App.GitHealth.Api/Features/Updates/VelopackUpdateService.cs`
   queries `https://github.com/LINDECKER-Charles/App.GitHealth` for releases through
   Velopack's `GithubSource`, and downloads a package on `POST /api/updates/apply`. It is
   registered only for the native launcher on Windows and macOS, and every call returns
   `Unsupported` unless `UpdateManager.IsInstalled` — a portable archive never reaches the
   network. This was already true at `0.1.0` and the previous audit did not say so.
3. **The desktop window's rendering engine.** The window is drawn by a system engine —
   WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux — that GitHealth neither
   ships nor installs. When it is unusable, `DesktopWindow` catches the native load
   failure, writes a warning on `stderr` and falls back to the system browser. It downloads
   nothing. Whether the host platform fetches a missing runtime by itself is outside this
   code and was not observed here.

Everything else is unchanged: no analysis, export, snapshot or policy operation reaches a
network.

This version can be distributed for local use. The main risks before wide distribution are
unchanged — binary signing, the immutability of build dependencies, the confidentiality of
an unencrypted SQLite database — and one is new in kind: the containment of a third-party
CLI that GitHealth starts but does not own.

## Method and evidence

- manual review of the HTTP, process, path, persistence, export and agent boundaries;
- search for private secrets and outbound network clients in the sources;
- transitive NuGet audit (`dotnet list App.GitHealth.sln package --vulnerable
  --include-transitive`): seven projects, no vulnerable package from nuget.org;
- `npm audit --omit=dev` in `src/App.GitHealth.Web` and `npm audit` in
  `tests/App.GitHealth.E2E`: 0 vulnerabilities in both lockfiles;
- **.NET tests, re-measured.** `dotnet test App.GitHealth.sln --configuration Release`
  reports **433 tests, 427 passed, 6 failed**: `App.GitHealth.Core.Tests` 112/112,
  `App.GitHealth.Git.IntegrationTests` 43/43, `App.GitHealth.Api.Tests` 272 of 278. The six
  failures are one environmental defect, not a finding: on this macOS host the temporary
  directory is `/var/folders/…`, a symlink to `/private/var/folders/…`, and the assertions
  compare the requested path against the resolved one. Five of them —
  `ProjectEndpointTests.ProjectCanBeValidatedCreatedListedAndUpdated` and four cases in
  `ProjectRelocationEndpointTests` — diff the two spellings directly; the sixth,
  `ProjectEndpointTests.ValidateAcceptsPhysicalPathBehindConfiguredRootLink`, fails earlier
  still, on a `400` from the validation endpoint, for the same reason one level up. Path
  resolution is behaving correctly; the tests encode the host's unresolved spelling. CI runs on Linux, where the two paths coincide;
- HTTP security scenarios: `tests/App.GitHealth.Api.Tests/Security/LocalRequestSecurityTests.cs`
  declares 8 `[Fact]` and 2 `[Theory]` with 6 `[InlineData]` rows — **14 executed cases**,
  the same figure as `0.1.0`, against a larger surface;
- assistant coverage: **68** `[Fact]`/`[Theory]` declarations across
  `tests/App.GitHealth.Api.Tests/Assistant/`
  (`grep -c "\[Fact\]\|\[Theory\]" tests/App.GitHealth.Api.Tests/Assistant/*.cs`), covering
  the command line, the bridge endpoint, the session registry, the JSON-RPC dispatcher, the
  event readers and conversation persistence;
- **Angular tests, re-measured.** `npm run test:ci --prefix src/App.GitHealth.Web` reports
  **41 test files, 327 tests, all passing** — against 43 at `0.1.0`;
- the end-to-end suite was **not replayed here**. It needs a published build and a browser
  download this host did not have; `tests/App.GitHealth.E2E/specs/` declares 4 tests across
  two spec files (`grep -c "^test(" tests/App.GitHealth.E2E/specs/*.ts`). The claims it
  carries are discussed under P5;
- the Docker smoke test was **not replayed**, for the same reason as at `0.1.0`: no engine
  on this host. The Compose configuration was reviewed statically;
- acceptance testing on two real repositories, replayed for this release: 4,432 and 1,021
  commits, 59 and 62 branches analysed, five branches per repository cross-checked against
  `git rev-list --left-right --count`, and both repositories verified byte-identical
  afterwards on references, index, worktree diff and reflogs
  ([`docs/release/acceptance-0.2.0.json`](release/acceptance-0.2.0.json));
- no agent run was executed against a live provider during this audit. The containment
  flags were read from `AgentCatalog.cs` and asserted by `AgentCommandLineTests`; see P7.

## OWASP coverage

| Area | Observed controls | State |
|---|---|---|
| A01 Access control | loopback `Host` everywhere; origin and Fetch Metadata on `/api` only; `/agent-bridge/{token}` authorises on its path token alone | controlled, boundary now split |
| A02 Cryptography | 256-bit session and bridge tokens; unencrypted SQLite; non-constant-time token lookup | low residual risk |
| A03 Injection | no shell, separate arguments, EF Core, neutralised CSV; fixed agent catalogue and allowlisted effort; model output parsed, never `innerHTML`; capped JSON-RPC bodies | controlled |
| A04 Design | documented local boundaries, bounded timeouts, queues and run budget | controlled |
| A05 Configuration | CSP, headers, dev-only OpenAPI, unprivileged Docker; assistant options validated on start | controlled |
| A06 Components | NuGet/npm audits, Dependabot, SBOM | controlled, to monitor |
| A07 Authentication | anti-forgery session for the browser; single-run bearer token for the agent; per-repository consent gate | consistent with the local model |
| A08 Integrity | SHA-256, SBOM and provenance; actions still not pinned by SHA | to reinforce |
| A09 Logging | Problem Details errors; a running analysis now publishes its last 60 Git commands with the first line of Git's answer | acceptable locally, deliberate |
| A10 Server-side requests | remote Git protocols still blocked; an agent child process reaches its own provider; a locally listening JSON-RPC endpoint accepts non-browser bodies | bounded, not absent |

### A01 — why the bridge sits outside `/api`

`LocalRequestSecurityMiddleware` applies exactly one control to every request: the `Host`
must be loopback. The other two are scoped by path. `IsApiRequest` —
`Path.StartsWithSegments("/api")` — gates the origin and `Sec-Fetch-Site` check; `IsApiMutation` gates the session cookie and
the anti-forgery token.

`POST /agent-bridge/{token}` is deliberately outside that prefix, and
`AssistantMcpEndpoints.cs` says why: `/api` is the browser's, and a mutation on it must
carry a session cookie and an anti-forgery token that a command-line process does not have.
Relaxing the guard for one route would have relaxed it for the browser too. So none of
origin, `Sec-Fetch-Site`, the session cookie or the anti-forgery token applies to the
bridge. What authorises it is the 256-bit token in its own URL path, plus the global
loopback `Host` check — nothing else. `AssistantMcpSessionRegistry.Find` returns null for an
unknown, closed or expired token alike, and the endpoint answers `401` with a JSON-RPC
`invalid request` envelope without distinguishing which.

The route is mapped unconditionally, including when `GitHealth:Assistant:Enabled` is false.
That is harmless rather than sloppy: no session is ever opened in that configuration, so
every request to it is answered `401`. `GET` on the route answers `405` (this bridge never
pushes), `DELETE` answers `204`.

### A02 — the bridge token

Constructed in `AssistantMcpSessionRegistry.Open` as
`RandomNumberGenerator.GetHexString(TokenBytes * 2, lowercase: true)` with `TokenBytes = 32`
— 64 lowercase hex characters, **256 bits** of cryptographic randomness. It names no project
and carries no privilege of its own; it *is* the authorisation.

It is held only in memory, in a `Dictionary<string, AssistantMcpSession>` keyed with
`StringComparer.Ordinal` inside the registry singleton. It is never written to the database,
never logged, and never rendered: `AgentCommandLine.Describe` substitutes `<single-use-token>`
before the command line is shown or stored.

**The lookup is not constant-time.** It is an ordinal hash-table probe, so a match is
distinguishable from a miss by timing in principle. Rated negligible here: the token is 256
bits, an attacker able to time the loopback socket is already running as the same user, and
the same user can read the token directly from the agent's command line (P6) at no cost.
Constant-time comparison would not change that reachability.

It is closed in the `finally` of `AssistantRunService.ExecuteAsync` — `bridge.Close(token)` —
whichever way the run settled: answered, failed, timed out or cancelled. The expiry is a
backstop only: `Grace = TimeSpan.FromMinutes(20)`, longer than the 900-second ceiling
`AssistantOptions.MaximumTimeoutSeconds` allows a run to be configured to, so it never cuts
off a legitimate agent. Expired entries are pruned when the next session opens and rejected
on lookup.

### A03 — three new injection surfaces

**The agent command line.** The agent identifier is resolved against a fixed catalogue —
`AgentCatalog.All` holds exactly `claude` and `codex` — and `AgentCatalog.Find` returning
null produces `503 assistant.agent_unavailable`. A request naming anything else never
reaches a process: the interface cannot make GitHealth run an arbitrary executable. The
effort level ends up inside a command line, so it is allowlisted against what the agent
declares by `AgentEffort.IsSupported`; an unsupported one is refused with
`400 assistant.effort_unsupported` rather than quietly downgraded. The process is built with
`UseShellExecute = false`, `CreateNoWindow = true` and one `ArgumentList` entry per
argument — no shell, no string concatenation. The bridge token travels inline, as a
`--mcp-config` JSON document for Claude Code and a `-c mcp_servers=…` override for Codex, so
it never lands in a configuration file on disk. On Windows only, an npm `.cmd`/`.bat` shim
is run through `cmd.exe /c` because `CreateProcess` refuses a shim; the shim path and the
arguments are handed over as separate `ArgumentList` entries. On Windows those entries are
then joined into one command line that `cmd.exe` re-parses with its own metacharacter rules,
so the separation is not by itself a guarantee. What makes it safe here is that none of
these arguments is repository-derived: the shim path comes from the resolved executable, the
effort from the catalogue, and the token from `RandomNumberGenerator`.

**The agent's Markdown answer.** This is untrusted model output rendered inside the
application's own origin, which is a stored-XSS surface in the ordinary sense. It is parsed
by `src/App.GitHealth.Web/src/app/core/markdown/` into a typed block and span tree, then
rendered by `ds-markdown.ts` through ordinary Angular interpolation and structural
directives. There is no `innerHTML` anywhere in `src/App.GitHealth.Web/src` — the word
appears twice, both times in a comment: `ds-markdown.ts` states that there is none and that
there must not be one, and `shell-tokenizer.ts` describes itself as rendering without one.
Links are filtered
by scheme: `markdown-inline.ts` tests `/^(https?:\/\/|mailto:)/i` and emits a `link` span
only on a match; anything else — `javascript:`, `data:`, `file:` — is emitted as literal
text spelled `[label](target)`, inert. Rendered anchors carry `target="_blank"` and
`rel="noreferrer noopener"`. The CSP restricting scripts to `'self'` is a second line behind
this, not the first.

**The JSON-RPC bodies posted to the bridge.** `AssistantMcpEndpoints.MaximumRequestBytes` is
`256 * 1024` — **256 KiB**. A larger body is discarded rather than parsed, which makes
`JsonDocument.ParseAsync` fail and produces `400` with a JSON-RPC envelope; a malformed body
takes the same path. Bodies are parsed with `System.Text.Json` into a `JsonDocument`, and a
batch array is dispatched element by element. Each call is answered from the
`AnalysisBriefing` held in the session — there is no database read behind a tool call, so no
crafted argument can widen what the call sees.

### A07 — a second authorisation scheme and a consent gate

`0.1.0` had one scheme: an in-memory session with a pair of anti-forgery tokens, for the
browser. `0.2.0` adds a second, disjoint one: the single-run bearer token described under
A02, for a non-browser client. The two never overlap — the browser never sees a bridge
token, and the agent never sees a session cookie.

Above both sits a consent gate that is not an authentication control but is what actually
decides whether data leaves the machine. `ProjectEntity.AssistantConsentAtUtc` is a nullable
moment on the project row, set through `PUT /api/projects/{id}/assistant/consent`. It is the
**API** that enforces it, in `AssistantRunService.StartAsync`: when
`ConsentGrantedAtUtc is null`, the call returns `ApiProblems.Forbidden` — **HTTP 403**, code
`assistant.consent_required` — and returns before `AssistantScratch.Create`,
`bridge.Open` and the process are reached. Nothing is created, no token is minted, nothing
is sent. The panel asking first is a courtesy; this is the control.

### A09 — the run console is deliberate, and the old row was wrong

The `0.1.0` row read "Problem Details errors with no raw Git output". That is now false.
`GET /api/analyses/{analysisId}` answers with a `progress` object whose `commands` array
carries the tail of the run: `AnalysisRunProgress.RetainedCommands = 60`, so the **last 60**
Git commands, each with a sequence number, the command line, a duration in milliseconds, the
exit code and `output` — **the first non-blank line of what Git answered**.

Both strings are bounded by `GitCommandLine`: the command line to 220 characters, the output
line to 120, with an ellipsis. The rendering omits the `-C <repository>` prefix and the
hardening flags GitHealth injects, as constant noise. Error text still goes through Problem
Details.

This is a deliberate trade, and the changelog entry for the run scene states it: a progress
bar asks to be believed, a console naming every command can be checked. The residual
exposure is that reference names, and the first line of Git's answer about them, reach the
browser over loopback for a running analysis. That data is already in the capture the same
client reads. Rated **low**; the row is restated rather than treated as a regression.

## Critical vulnerabilities

No confirmed critical vulnerability.

The hostile tests carried over from `0.1.0` still cover arguments starting with a dash,
path traversal, symbolic links, worktrees, external `gitdir`/`commondir`, nested alternates,
injected Git environments, output overruns, timeouts and process-tree cancellation.

## The agent assistant — findings

This section is new. It states what was examined and what risk remains, rather than
restating the design; `docs/SECURITY_MODEL.md` carries the design.

### The feature ships on, and consent is the gate — **noted**

`AssistantOptions.Enabled` is declared `public bool Enabled { get; init; } = true;`, and
`src/App.GitHealth.Api/appsettings.json` sets `GitHealth:Assistant:Enabled` to `true`
explicitly. **The one feature that reaches a network is therefore on by default.** An
administrator who does not want branch names leaving the machine must set it to false;
`AgentAvailabilityService.IsEnabled` then reports the feature off and no interface can
re-enable it.

This is a defensible choice only because it is not the real gate. A fresh installation with
the setting at its default still sends nothing: every run needs `AssistantConsentAtUtc` on
the specific repository, and the agent CLI must be installed. But it should be said plainly
that the shipped posture is *enabled, ungranted* rather than *disabled*, and that a
deployment which wants a hard off-switch has to set one.

### What the bridge exposes — **low**

Four read-only tools over **one** capture already in the database: `get_capture`,
`list_branches`, `get_branch`, `count_branches`. No tool runs Git, touches the file system,
names a project or writes anything. Answers are served from the `AnalysisBriefing` held in
the session, so a tool call cannot reach past the capture it was cut for.

Contributor **email addresses are excluded by construction** —
`BriefingBranch.TipAuthor` carries the display name and the type has no address field at
all, so no argument to any tool can produce one. Branch names and tip author display names
do leave the machine, which is exactly what consent is granted for.

### The token — **low**

Verified under A02. One run, one token, 256 bits, in memory only, closed on settle, 20-minute
backstop. See P6 for the residual.

### Child-process containment — **low**

The run happens in an empty temporary directory created by `AssistantScratch.Create` through
`PrivateFilePermissions.CreateTemporaryDirectory`, deleted in the run's `finally` — never in
the analysed repository, so the agent has nothing of the user's to read at its working
directory. The prompt travels on standard input, which is then closed.
`AgentProcessRunner` mirrors the Git runner: a shared output budget across stdout and
stderr, a timeout, and `Kill(entireProcessTree: true)` on overrun or cancellation.

### Per-agent containment is asymmetric, and the weaker side cannot be fixed — **moderate**

The two catalogue entries are not equally constrained, and the gap is structural.

**Claude Code** is launched with `--print --output-format stream-json --verbose
--include-partial-messages --strict-mcp-config --mcp-config <inline JSON> --tools ""
--allowedTools mcp__githealth --effort <level>`. `--tools ""` removes every built-in tool —
no shell, no file read, no network — and `--allowedTools` grants back only GitHealth's own
namespace. `--strict-mcp-config` drops the machine's own MCP servers, so the inline
declaration is the only one in force. The single thing such a run can do is read the capture
it was started for.

**Codex CLI** is launched with `exec --json -c model_reasoning_effort=<level>
-c mcp_servers={githealth={url="…",default_tools_approval_mode="approve"}}
--sandbox read-only --skip-git-repo-check --color never --output-last-message <path> -`.
`--sandbox read-only` is its own policy, and replacing the whole `mcp_servers` table keeps
the user's own declared servers out of the run. That is the nearest equivalent to
`--strict-mcp-config`, and it is weaker: tools this CLI gets from its own plugins and
connectors stay reachable, and **no flag removes them**. The isolation that would remove
them — pointing the run at a throwaway `CODEX_HOME` — would in the same move remove the
credentials the run needs, because they live in that directory. There is no configuration
that keeps one and drops the other.

GitHealth serves this agent one capture and nothing else. It cannot promise that GitHealth
is the only thing this agent can reach, and the code says so in as many words. The finding
is rated moderate rather than low because a user reading "read-only sandbox" may reasonably
infer a containment that only one of the two agents actually has. The asymmetry is documented
in `AgentCatalog.cs` and in the security model; it is not mitigated.

### What is persisted, and how to remove it — **low**, see P2

Two tables, `AssistantConversations` and `AssistantMessages`, in the same unencrypted SQLite
file as everything else. Contents and purge paths are enumerated under P2.

### The environment is deliberately not scrubbed — **accepted**

`AgentCommandLine.ConfigureEnvironment` sets only `NO_COLOR=1`, `FORCE_COLOR=0` and
`TERM=dumb`, to keep escape sequences out of the trace. Everything else in the parent
environment is inherited. This is the opposite of the treatment Git gets, where
`GIT_*`, credential helpers and protocols are neutralised — and it is deliberate: these CLIs
read their credentials from the environment, and a scrubbed environment is a logged-out
agent that cannot run at all. Accepted for a process the user already runs themselves with
the same rights.

### The run's resource budget — **low**

Defaults, all validated on start by `AssistantServiceCollectionExtensions` with
`ValidateOnStart()` and refusing to boot on a value out of range:

| Setting | Default | Allowed range |
|---|---|---|
| `RunTimeout` | 3 minutes | 10–900 seconds |
| `MaximumOutputBytes` | 4 MiB | 4 KiB – 8 MiB |
| `MaximumBranches` | 200 | 1–2000 |
| `MaximumParallelRuns` | 1 | 1–4 |

The output budget is shared between stdout and stderr, so a chatty error stream cannot
starve the answer, and exhausting it kills the process rather than only the read loop.
Branches past the cap are dropped from the briefing and their number is stated in the text.

**A second run is refused, not queued.** `AssistantRunRegistry.TryRegister` counts unfinished
runs under a lock and returns false at the cap; the caller disposes the scratch directory,
closes the bridge token and answers **HTTP 409**, code `assistant.busy`, with "Another run is
already in progress. Wait for it or stop it first." The cap defaults to one because every run
is billed to the user's own account, and a queue that fans out spends their money without
being asked. Run records are retained in memory for 30 minutes or 40 runs, whichever binds
first.

## Potential vulnerabilities

### P1 — TOCTOU race on a local path — low

*Carried forward from `0.1.0`, unchanged.* A process running as the same user can replace a
path component between its physical validation and the moment Git opens it. Re-validating
just before the analysis narrows the window, but does not eliminate it without native
handles or a per-repository sandbox.

Impact: GitHealth could read another repository reachable by the same account. The attacker
already holds the corresponding read rights; no privilege escalation was demonstrated.

Status for `0.2.0`: **accepted**, unchanged. The re-validation test is kept. Native directory
handles or a sandbox remain future work for a version intended for genuinely hostile
repositories.

### P2 — Unencrypted SQLite database, now holding conversations — low, wider than at `0.1.0`

*Re-scoped, not reworded.* At `0.1.0` this finding was about author names and addresses. The
same file now also holds the assistant conversations. Per thread, `AssistantConversations`
and `AssistantMessages` carry:

- the questions as they were typed;
- the answers as the agent wrote them, **branch names and tip author display names
  included**;
- which agent answered, under which display name, at which effort;
- the command line that produced each answer, with its bridge token replaced by
  `<single-use-token>`;
- durations in milliseconds, statuses, and the failure code and message of runs that
  produced no answer;
- a truncation flag for answers cut off by the output budget.

The bridge token is never among them. Contributor email addresses are still excluded by
construction, in the stored answers as in the bridge.

Impact: another process holding the account's rights, or a poorly protected backup, can read
all of it. **`GET /api/exports/database` and any copy of the SQLite file now carry the
conversation history as well as the author data** — the classification of an export has
widened, and the `0.1.0` recommendation to document it now has more to document.

Three purge paths mitigate this, all real deletes with no archive and no soft delete:

1. deleting a capture deletes every conversation about it —
   `AssistantConversationEntityConfiguration` binds the thread to its `AnalysisRuns` row with
   `OnDelete(DeleteBehavior.Cascade)`, and messages cascade from the conversation in turn;
2. `DELETE /api/assistant/conversations/{conversationId}` removes one thread;
3. `DELETE /api/projects/{projectId}/assistant/conversations` empties one repository's
   history at once and reports how many threads went.

Withdrawing consent deliberately does **not** purge: stopping the sending and forgetting what
was already said are two decisions on two buttons.

Status for `0.2.0`: **accepted with the purge paths as the mitigation**. Encryption at rest
is still not implemented; it is recommendation 8 below.

### P3 — Local service without user authentication — low within the model

*Carried forward from `0.1.0`, widened.* Any local process able to reach loopback can read
the API with a non-browser client. Foreign browsers are blocked by origin and Fetch
Metadata; `/api` mutations additionally require the session and the anti-forgery token.

What is new is that `/agent-bridge/{token}` is now a loopback endpoint **designed** to be
reached by a non-browser client. It is not an authentication weakening — it holds a 256-bit
secret and serves one capture — but it means the surface is no longer "browser only, by
construction".

Impact: the risk becomes high if the application is exposed on a network or placed behind a
proxy. That configuration remains explicitly out of scope.

Status: **accepted**. Keep rejecting Kestrel endpoints configured in native mode, and design
full authentication before any non-loopback exposure.

### P4 — The update path reaches GitHub from an installed native build — low

Not previously stated. In a Velopack-managed installation on Windows or macOS,
`GET /api/updates` queries the project's GitHub releases and `POST /api/updates/apply`
downloads a package and restarts. `accessToken` is null, so the query is anonymous and the
request carries no repository content — but it does reveal, to GitHub, that an installation
exists and which version it runs. An unreachable or quota-limited source is reported, not
propagated: the check returns `Unknown` rather than failing the application.

Impact: a passive observer learns that GitHealth is installed and at what version. No
repository data is involved.

Status: **accepted for `0.2.0`.** It is inherent to shipping in-app updates. It should be
named in the privacy documentation, and it strengthens rather than weakens the case for the
signing work in M3: an update channel that fetches and executes a package is exactly the
path a signature protects.

### P5 — The no-external-host assertion is blind to a child process — low, claim to restate

`observeExternalHosts` in `tests/App.GitHealth.E2E/specs/mvp-flow.spec.ts` registers
`page.on("request")` and collects any request whose protocol starts with `http` and whose
hostname is neither `127.0.0.1` nor `localhost`.

**That hook sees Playwright's browser context and nothing else.** It proves a real and useful
thing: the page loads no third-party script, font, image or beacon, issues no cross-origin
`fetch`, and the CSP holds for the duration of the MVP journey. It proves nothing at all
about the .NET process's own sockets, and it is structurally incapable of observing a child
process — an agent CLI calling its provider is a separate process with its own network stack,
outside the browser entirely.

The scenario also does not exercise the assistant: no agent CLI is installed in that
environment, and the journey never starts a run. So the assertion neither confirms nor
contradicts anything about the feature that actually reaches a network.

Status: the test is **kept, with its claim narrowed**. Do not cite it as evidence that
GitHealth makes no outbound connection. A process-level check — no outbound socket from the
GitHealth process during an analysis — would be a different test, and is recommended below.

### P6 — The bridge token is a bearer secret readable from the command line — low

New. The token is passed inline on the agent's command line, which is the right decision
against the alternative — it never lands in a configuration file, and it is redacted
everywhere it is shown or stored. The cost is that for the life of the run, the raw token is
in the child process's argument vector, which on every supported platform is readable by any
process running as the same user (`ps`, `/proc/<pid>/cmdline`, Windows WMI).

Impact: software already running as the user could read the token while a run is in flight
and use it to query the bridge for that one capture. The window is the run's duration; the
reach is one capture the same software could read straight out of SQLite anyway, with author
addresses included, which the bridge does not expose. So this widens nothing it does not
already have.

Status: **accepted, and named rather than fixed.** A file-based handoff would remove it from
`ps` and put it on disk instead, which is worse for a secret that outlives nothing. The
mitigations that do apply are already in place: one token per run, closed on settle, 256 bits,
no privilege of its own.

### P7 — Containment depends on vendor flags nothing here verifies at runtime — moderate

New. The read-only containment of a run rests entirely on flags a third party defines:
`--tools ""`, `--allowedTools` and `--strict-mcp-config` for Claude Code; `--sandbox read-only`
and the `mcp_servers` replacement for Codex CLI. GitHealth asserts that it *passes* those
flags — `AgentCommandLineTests` checks them one by one, that `claude` still carries
`--tools ""`, `--strict-mcp-config` and `--allowedTools mcp__githealth` and that `codex`
still carries `--sandbox read-only` — but nothing asserts that
the installed CLI still *honours* them.

Three ways this degrades silently. A vendor renames a flag: most CLIs reject an unknown
option, which fails loudly, so this is the benign case. A vendor changes what a flag means —
`--tools ""` coming to mean "defaults" rather than "none", or the sandbox gaining an exception
— and the run keeps succeeding while granting more than intended. A vendor adds a capability
outside the flags entirely, as Codex plugins and connectors already are. Nothing in GitHealth
would detect any of the last two. The version the Claude Code flags were verified against is
recorded in `docs/SECURITY_MODEL.md` — 2.1.220 for the tool restriction — rather than beside
the flags in `AgentCatalog.cs`, and no version is recorded for Codex CLI at all. This
revision did not re-verify against an installed CLI.

Impact: a run could read or reach more than the audited grant, with no signal to the user or
to the code. Bounded by the fact that the CLI runs as the user and with the user's own
credentials, so it gains nothing the user does not already hold — but the promise GitHealth
makes about a run would be wrong.

Status: **carried as an open risk.** Recommended treatment below: record the verified version
per agent, re-verify on upgrade, and consider a startup probe that asserts the flags are still
accepted.

## Bad practices

### M1 — GitHub actions referenced by major tag — moderate, **carried forward, still open**

Unchanged since `0.1.0` and verified again: every step in `ci.yml`, `security.yml`,
`release.yml` and `benchmark.yml` uses a mutable reference — `actions/checkout@v7`,
`actions/setup-dotnet@v6`, `actions/setup-node@v7`, `actions/upload-artifact@v7`,
`actions/download-artifact@v8`, `actions/attest@v4`, `actions/dependency-review-action@v5`,
`github/codeql-action/{init,analyze}@v4`. The single exception is
`anchore/sbom-action@v0.24.2`, pinned to a patch tag, which is narrower but still mutable.

Status for `0.2.0`: **not accepted — carried forward as open.** The `0.1.0` recommendation
stands and is now overdue: pin third-party actions to a verified SHA and let Dependabot move
them. Dependabot already tracks the actions ecosystem, so the update flow this pinning would
depend on exists; what is missing is the pinning itself.

### M2 — Docker images referenced by version, without a digest — low, **accepted for `0.2.0`**

Verified in `Dockerfile`: `node:24.20.0-alpine3.24`, `mcr.microsoft.com/dotnet/sdk:10.0.401-noble`
and `mcr.microsoft.com/dotnet/aspnet:10.0.12-noble`. All three are exact patch versions; none
is bound to an immutable digest.

Status: **accepted for `0.2.0`.** Docker is a secondary distribution path for this release,
the tags name exact patch levels rather than a floating major, and the digest pinning is best
done once the base images stabilise for a `1.0`. This is a conscious deferral, not an
oversight — it is reclassified from "record the digests when stabilising" to "accepted for
this release, required for the first release distributed as an image".

### M3 — Unsigned and unnotarised macOS binaries — moderate, **carried forward, still open**

Verified again: no `codesign`, `notarytool` or signing step appears in
`.github/workflows/release.yml` or in `eng/New-VelopackRelease.ps1`, and both
`docs/DEVOPS.md` and `CHANGELOG.md` still state that the macOS archives are neither signed
nor notarised. The Windows installer is likewise unsigned.

`0.2.0` did change the macOS packaging — the application now ships its own `.icns` rather
than Velopack's placeholder, with `eng/BuildEnvironment.ps1` failing the packaging when the
icon is missing. That is a correctness fix to the bundle, not a signing change, and it does
not move this finding.

Status: **not accepted — carried forward, and now more pressing.** P4 established that an
installed build fetches and executes update packages from GitHub. An unsigned update channel
is a materially different risk from an unsigned first install: the first is verified once by a
user who chose to download it, the second runs unattended on every release. Signing should
precede any promotion of the update path.

## Recommendations

### Before the stable version

1. Sign on Windows and macOS, then notarise the macOS artefacts — now gating the update
   channel, not only the first install (M3, P4).
2. Pin build actions by SHA (M1). Docker digests can wait for the first image release (M2).
3. Record, per agent, the CLI version the containment flags were verified against, and
   re-verify on upgrade. Consider a startup probe that fails the feature loudly if a flag is
   no longer accepted (P7).
4. Add a process-level outbound check to replace the claim P5 cannot support: assert that the
   GitHealth process opens no socket off loopback during an analysis.
5. Run the CodeQL, dependency review, SBOM and provenance workflows on the RC tag.
6. Replay the Docker smoke test on a running engine and archive the CI evidence.
7. Define the retention of author data **and of assistant conversations**, and state the
   classification of `GET /api/exports/database` now that it carries both (P2).
8. Fix the six macOS path-normalisation test failures so a local `dotnet test` is green, and
   the signal is not lost in known noise.
9. Decide, explicitly, whether `GitHealth:Assistant:Enabled` should keep defaulting to true,
   and document the answer either way.

### Continuous defence

1. Handle Dependabot alerts and dependency audits before releasing.
2. Keep the origin, anti-forgery, hostile-path and non-mutation tests, and keep the bridge
   tests alongside them: the token lifecycle, the `401` on a closed token, the `409` on a
   second run and the `403` on a missing consent are now load-bearing.
3. Review every new Git command to rule out network access, hooks and implicit writes.
4. Review every new MCP tool against the same bar as a Git command: it must not run a process,
   reach the file system, cross out of its own capture, or carry an email address.
5. Refuse any future network listener without a new threat model and authentication. The
   bridge is not a precedent for one — it opens no listener of its own and is served on the
   port the interface already holds.
6. Re-audit if the assistant gains a write tool, a second capture per run, an agent that
   cannot be constrained at all, or a hosted provider GitHealth calls directly. Any of the
   four moves the trust boundary again.

## Conclusion

The security level remains appropriate for a local, single-user product, and the controls
that mattered at `0.1.0` are intact: web CSRF, simple DNS rebinding, Git argument injection,
unbounded denial of service and Docker root escape are all still addressed.

What changed is honesty about the boundary rather than the strength of it. `0.1.0` could
claim no outbound communication; `0.2.0` cannot, and the exceptions are now named, bounded
and gated — a child process the user installed, calling a provider the user pays, about a
capture the user consented to send, over a token that dies with the run. The two findings
worth carrying into the next release are the containment asymmetry between the two agents
(moderate, structural, unmitigated) and the dependence on vendor flags nothing here verifies
(moderate, silent if it degrades).

Public distribution remains conditional on the supply-chain and signing recommendations
above, which are now the oldest open items in this document.
