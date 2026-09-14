# Known limitations of `0.2.0`

`0.2.0` adds the assistant, and with it the feature that moves the trust boundary
furthest: it hands a repository's measurements to a process that calls someone else's
service. That section is listed first for that reason. It is not the only thing here that
reaches the network — a managed installation also asks GitHub whether a newer release
exists, which is under **Packaging and platforms** — but it is the only one that sends
anything about your repository. Everything below is unchanged in kind from `0.1.0`.

## The assistant

- The assistant is the only feature that sends anything *about a repository* off the
  machine. No analysis, export, snapshot or policy operation reaches a network, and
  GitHealth opens no outbound connection of its own for the assistant either: what leaves
  is a child process the user already runs themselves.
- GitHealth holds no API key and has no account. It starts an agent CLI installed on the
  machine — Claude Code or Codex CLI — and the call that agent makes is billed to the
  user's own account with their own provider.
- Sending a repository's captures must be allowed once, per repository. The API enforces
  it, not the panel: a run started without it is refused with `403` and
  `assistant.consent_required` before any process is created. Repositories saved before
  `0.2.0` all start as not allowed.
- `GitHealth:Assistant:Enabled` defaults to `true`. Setting it to `false` removes the
  feature from the installation, and no interface can turn it back on.
- Branch names and tip author names leave the machine when a question is asked.
  Contributor email addresses do not: they are excluded from the briefing and from every
  bridge tool by construction.
- The database is not encrypted at rest. It is an ordinary SQLite file, readable by
  anything running with the user's rights, and `0.2.0` widened what it holds.
- Conversations are kept in that same local database, so they travel in
  `GET /api/exports/database` and in any copy of that file. Per thread, that is: the
  questions as they were typed, the answers as the agent wrote them — branch names and tip
  author names included — which agent answered and at which effort, the command line with
  its bridge token blanked, the durations, the statuses, a truncation flag for answers cut
  off by the output budget, and the failure messages of runs that produced no answer. Three things remove them, all real deletes: deleting the capture
  the thread hangs off, deleting one thread from the panel's history, and
  **Policies → Assistant → Delete every conversation**.
- Withdrawing consent does not delete anything. "Stop sending this repository's captures"
  and "forget what was already said" are two separate buttons.
- One run at a time. `MaximumParallelRuns` defaults to `1`, and a second run is refused
  with `409` and `assistant.busy` rather than queued: these calls cost the user money, so
  nothing fans out on its own.
- Docker has no assistant. The image installs `ca-certificates`, `curl` and `git` and
  nothing else, so neither CLI is present; the catalog reports both unavailable and the
  panel explains it. Nothing is installed to fix that.
- The agent reads a capture already in the database. It never reads the repository, so a
  repository that has never been analysed has nothing to ask about, and an answer is only
  ever as current as the capture it was started against.
- The capture the agent can query is capped at `MaximumBranches`, 200 by default, taken
  oldest activity first. `get_capture` states how many branches were left out, but they
  cannot be reached by any tool call.
- A run is bounded by `RunTimeout`, three minutes by default, and by
  `MaximumOutputBytes`, 4 MiB by default. Since both CLIs now narrate themselves as JSON,
  that second budget covers the whole exchange — every event and every capture row a tool
  call sends back — not just the answer. An installation that pinned it at the old 512 KiB
  should raise it, or a run reading a large capture is stopped mid-answer.
- Tool isolation differs by agent, and the difference is real. Claude Code runs with
  `--tools ""`, `--allowedTools mcp__githealth` and `--strict-mcp-config`, which on the
  version those flags were verified against leaves it no shell, no file access and no
  network beyond the bridge — and holds only as long as the installed CLI keeps honouring
  them, which nothing here checks at runtime. Codex CLI runs with
  `--sandbox read-only` and its `mcp_servers` table replaced rather than added to, but
  tools it gets from its own plugins and connectors stay reachable and no flag removes
  them. GitHealth serves it one capture; it does not control what else that process can
  reach.
- The environment handed to the agent is deliberately not scrubbed — that is where its
  credentials live. What the provider retains, and what the CLI itself writes to disk, are
  outside GitHealth's control.
- The bridge token is a bearer secret for the length of one run and travels on the agent's
  command line. Anything already running as the same user could read it there — the same
  software that could read the SQLite database directly.
- The steps shown while an agent works are never stored. A conversation reopened later
  holds the question and the answer; nothing in the database, the backup or the export
  knows what the agent did to get there.
- A desktop process inherits the system's minimal `PATH`, not the one the user's shell
  builds, so a CLI installed last week is often invisible to the search. `~/.local/bin`,
  `/opt/homebrew/bin`, `~/.<agent>/local` and the npm prefix are looked at explicitly; a CLI
  installed anywhere else needs `GitHealth:Assistant:Agents:<id>:ExecutablePath`.

## What GitHealth does to a repository

- GitHealth only analyses repositories already present on the machine; it does not clone
  and does not handle forge credentials.
- No `fetch`, `pull` or `remote prune` is ever run. The analysis uses `for-each-ref`,
  `rev-list`, `merge-base`, `log`, `cat-file` and `rev-parse` and nothing else. Remote
  references may be stale until the user updates them deliberately.
- No delete, merge, checkout or push operation is offered. Recommendations must be
  verified before any action taken outside GitHealth.
- GitHealth does not bundle Git: a Git installed on the machine is still required.
  `--git-path` points to an executable located outside the `PATH` and the standard
  locations.
- GitHealth does not detect open pull requests and does not replace the retention
  policies of GitHub, GitLab or Azure DevOps.

## What the measurements cannot say

- A branch's activity is approximated by the date of its tip commit. Git stores neither
  the intent behind its creation nor the shared history of every checkout.
- After a merge, attributing commits to their branch of origin can become impossible. The
  interface says so explicitly.
- `.mailmap` normalises the identities known to the repository. Without that file, one
  person using several addresses can appear several times.
- The author filter matches the **tip author**, not the top contributor, because the tip
  author is populated for every branch. The top contributor counts the commits a branch
  adds to its baseline, so it is null on a merged branch, which adds none.

## Scale

- Enrichment dominates an analysis and scales almost linearly: one `git log` per branch
  tip. On the Windows baseline in `docs/benchmarks/windows-initial.md`, 1,000 branches
  took a median 76.4 s in that phase alone — about 76 ms per branch, over 98 % of the
  total. Several thousand branches therefore take several minutes.
- The interface fetches a capture in pages of 200 and stops after 50 pages, so a capture
  is read up to 10,000 branches and no further. Past that, the views say the list was cut.
- Once fetched, the whole filtered list is rendered as DOM rows. There is no row
  virtualisation yet, so filtering and sorting stay local and fast while the initial render
  of a very large capture does not.
- The drift view loads only the last six captures, and the legend says so once there are
  more than six. If any of those captures had its branch list cut at the read cap, the
  comparison is partial and the legend says that too.

## Packaging and platforms

- The macOS archives and the `.pkg` installer are neither signed nor notarised. Gatekeeper
  may ask for explicit approval on first launch.
- The Windows installer is not signed, and neither is the portable archive. No artefact
  the release publishes is signed on any platform; SmartScreen may warn on first launch
  until a code signing certificate is in place. The SHA-256 checksums, the SPDX SBOM and
  the provenance attestations allow an archive to be verified, but they do not replace
  signing or notarisation.
- In-app updates exist only on Windows and macOS, and only for an installation made with
  the installer. A portable archive extracted by hand is not a managed installation, so it
  is never offered an update and no button appears. On Linux, only portable archives are
  published and updating stays manual.
- A managed installation asks GitHub whether a newer release exists, at startup. The query
  is anonymous and carries nothing about your repositories, but it does tell GitHub that an
  installation exists and which version it runs. Nothing else in the product contacts a
  forge.
- The desktop window is drawn by a system engine GitHealth neither ships nor installs:
  WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux. When it is unusable,
  GitHealth writes a warning on `stderr` and opens the interface in the system browser
  instead; it never stops for lack of a webview, and it downloads nothing to fix one.
- macOS caches the icon of a bundle it has already seen. An installation sitting in
  `/Applications` keeps showing the old grey placeholder until a release built from `0.2.0`
  replaces the bundle; `killall Dock` refreshes it once the new bundle is in place.

## Interface

- The interface is in English only. It is prepared for translation — every user-facing
  message carries an explicit id, 797 of them are extracted to
  `src/App.GitHealth.Web/src/locale/messages.json`, and the application loads a catalogue at
  runtime for any locale other than the source one — but only `en-US` ships, and the build
  stays single-locale on purpose: compile-time inlining would move the output under a
  locale subdirectory and break the publish path.
- Analysis failure messages stored in SQLite before `0.2.0` keep their French text. They
  are data, and the translation did not rewrite existing rows.

## Deployment

- The product is local and single-user. It is not designed to be exposed on a LAN, on the
  Internet, or behind a reverse proxy.
- The local protection restricts requests to the session and the origin generated at
  startup: a loopback `Host` on every request, a rejected foreign origin and cross-site
  `Sec-Fetch-Site` on `/api`, and an anti-forgery token on every mutation. It does not
  protect against malware running with the same rights as the user.
- Docker only sees the repositories placed under the `/repositories` mount. Host paths
  and container paths are not interchangeable.
