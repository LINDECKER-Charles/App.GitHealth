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
- the SQLite database, the policies and the snapshots;
- the local machine's compute capacity;
- the integrity of the distributed archives.

## Untrusted inputs

- repository paths and symbolic links;
- reference names, authors, messages and the repository's Git configuration;
- output and duration of Git processes;
- HTTP requests issued by another site or a local process;
- launcher configuration, environment variables and Docker mounts.

## HTTP controls

The native launcher and Compose listen on loopback only. Every application request must
carry a loopback `Host`. The `/api` routes reject a foreign origin and a cross-site
`Sec-Fetch-Site` context.

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
backup the user requests. Those files must be protected like business data.

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
- the macOS archives of the release candidate are neither signed nor notarised;
- an export copied off the machine escapes GitHealth's controls;
- local references can be stale, because no `fetch` is ever automatic.
