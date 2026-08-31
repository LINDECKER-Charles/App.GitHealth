# Plan — Moving to a desktop application

> Handover document. Status: **implemented** — batches 0 to 4 delivered.
> Kept as a record of the decisions and the reasons behind them.
>
> Three accepted deviations, discovered at implementation time and documented where they
> matter: the entry point became explicit and `[STAThread]` (in an MTA apartment, WebView2
> initialises without ever rendering the page); the batch 1 fallback catches
> `ApplicationException`, in which Photino re-wraps every native failure, rather than
> `DllNotFoundException`; and the update status has a fourth value, `Unknown`, for an
> unreachable release source — without it, `/api/updates` returned 500 when offline.

## 1. Goal

Turn GitHealth into a **desktop application that can be installed and launched by
double-click** on Windows, macOS and Linux, without rewriting the Angular front end and
without breaking the existing Docker mode.

Secondary, non-blocking goal: an "Update available" button inside the application, and
distribution through free package managers.

## 2. Settled decisions

These choices were debated and settled. **Do not reopen them** without new technical
evidence.

| Topic | Decision | Reason |
|---|---|---|
| Shell | **Photino.NET** | Kestrel and the window live in the same process: no child-process supervision, no port handshake, no zombies. Electron would impose ~150 lines of lifecycle plumbing. |
| Slicing | **A single executable**, no separate `Desktop` project | `Publish-Native.ps1`, the smoke tests and `release.yml` keep working unchanged. The weight of the unused Photino natives in Docker mode is negligible (1-2 MB, never loaded). |
| Installer / updates | **Velopack** on Windows and macOS | Free, GitHub Releases feed already produced by `release.yml`, delta updates, per-user installation without UAC. |
| Updates on Linux | **No in-app update** | Velopack's Linux support (AppImage only) is its weak link, and a Linux user expects their package manager, not a button. |
| Platform priority | **Windows and macOS first**, Linux next | Linux is explicitly secondary. |

**Accepted escape hatch**: the front end is served over loopback HTTP, so the shell is an
isolated, replaceable component. If WebKitGTK or WebView2 turn out to be a deal-breaker,
we switch to Electron without throwing anything else away.

## 3. Non-goals

- Do not rewrite the Angular front end. Changes on the `App.GitHealth.Web` side must stay
  **additive**.
- Do not remove Docker mode or `compose.yaml`. The `.env` file and its bind mount concern
  that mode only and stay valid.
- Do not handle code signing or macOS notarisation in this plan (a cost question, to be
  settled before the public 1.0).
- Do not bundle MinGit for now: batch 0 is limited to making the Git path configurable and
  the error actionable.

## 4. Current state of the repository

Read before writing code — a large part of the road has already been travelled.

- `src/App.GitHealth.Api/Program.cs:51` — `useNativeLauncher = isDirectLaunch && !IsContainer()`,
  container mode detected through `DOTNET_RUNNING_IN_CONTAINER`.
- `src/App.GitHealth.Api/Program.cs:182` — `RunNativeAsync`: starts Kestrel on loopback,
  resolves the bound port, opens the system browser.
- `src/App.GitHealth.Api/Hosting/SystemBrowserLauncher.cs` — opening the browser.
- `src/App.GitHealth.Api/Hosting/DataDirectoryResolver.cs` — data in
  `%LOCALAPPDATA%\GitHealth`, `~/Library/Application Support/GitHealth`, XDG.
- `src/App.GitHealth.Api/Hosting/LauncherOptionsParser.cs` — existing flags:
  `--repo`, `--port`, `--data-dir`, `--no-browser`, `--help` / `-h`.
- `src/App.GitHealth.Api/Git/Paths/RepositoryPathGuard.cs:9` — `IsAllowed` returns `true`
  when `RepositoriesRoot` is null: **in native mode there is no root to configure**, the
  user points at any folder.
- `src/App.GitHealth.Api/Features/Runtime/RuntimeEndpoints.cs` — `/api/runtime`
  (already exposes `Mode` = `native` / `docker`) and `/api/runtime/directories`
  (HTML folder browser).
- `eng/Publish-Native.ps1` — self-contained publish, `ValidateSet` limited to `win-x64`,
  `osx-x64`, `osx-arm64`.
- `.github/workflows/release.yml` — 3-RID matrix, native smoke test, SBOM, attestation,
  GitHub Release.
- `tests/Infrastructure/Invoke-NativeSmokeTest.ps1:172` — runs the binary with
  `--no-browser --port --data-dir --repo` and checks that the database is created.

---

## Batch 0 — Decouple Git from the PATH

**A hard prerequisite for any distribution.** An installable application that fails on the
first scan on a Windows machine without Git is the default case.

**Why** — `src/App.GitHealth.Api/Git/Process/GitProcessRunner.cs:120` does
`new ProcessStartInfo("git")`: resolution depends entirely on the `PATH`.

**Files**

- `src/App.GitHealth.Api/Git/GitScannerOptions.cs` — add
  `public string? ExecutablePath { get; init; }` (bound to the `GitHealth:Git` section,
  already bound in `GitServiceCollectionExtensions.cs:14`).
- `src/App.GitHealth.Api/Git/Process/GitProcessRunner.cs` — consume the resolved path
  instead of the `"git"` literal.
- New: a dedicated resolver under `src/App.GitHealth.Api/Git/Process/`.
- `src/App.GitHealth.Api/Git/GitRuntimeDiagnostic.cs` — expose the selected path.
- `src/App.GitHealth.Api/Hosting/LauncherOptionsParser.cs` and
  `StartupFailureReporter.HelpText` — add `--git-path <path>`, modelled on `--data-dir`.

**Work**

Resolution order, first hit wins:

1. `--git-path` / `GitHealth:Git:ExecutablePath`
2. `git` on the `PATH`
3. Standard locations per platform:
   - Windows: `%ProgramFiles%\Git\cmd\git.exe`,
     `%ProgramFiles(x86)%\Git\cmd\git.exe`,
     `%LOCALAPPDATA%\Programs\Git\cmd\git.exe`
   - macOS: `/opt/homebrew/bin/git`, `/usr/local/bin/git`, `/usr/bin/git`
   - Linux: `/usr/bin/git`, `/usr/local/bin/git`

`GitStartupProbe` (already an `IHostedService`) stays the probe point. Enrich the
unavailability message so that it becomes **actionable**: say where we looked and suggest
`--git-path`.

Add Git availability and the resolved path to `RuntimeInfoResponse` so that the front end
can display a blocking banner instead of failing on the first scan.

**Acceptance criteria**

- On a machine without Git on the `PATH` but with Git installed in a standard location,
  the analysis works.
- With no Git at all, `/api/runtime` reports it and the message names `--git-path`.
- Docker mode is unaffected (Git is in the image, resolved through the `PATH`).

**Tests** — `tests/App.GitHealth.Api.Tests`: unit tests on the resolver (configuration
takes precedence, PATH fallback, standard-locations fallback, not-found case). Do not test
actually running Git, which is already covered by
`tests/App.GitHealth.Git.IntegrationTests`.

---

## Batch 1 — Photino shell

**Files**

- `src/App.GitHealth.Api/App.GitHealth.Api.csproj` — Photino.NET `PackageReference`,
  pinned version.
- New folder `src/App.GitHealth.Api/Hosting/Desktop/`.
  ⚠️ `Hosting/` already contains 9 files and the project convention caps folders at 10:
  create the subfolder, do not pile up.
- `src/App.GitHealth.Api/Program.cs:182` — `RunNativeAsync`.
- `src/App.GitHealth.Api/Hosting/LauncherOptions.cs` and `LauncherOptionsParser.cs`.

**Work**

Replace opening the browser with a window, keeping the fallback. The structure of
`RunNativeAsync` stays the same: start the host, resolve the loopback address through
`BoundPort`, open the interface, wait for it to close.

**Flag semantics — to be honoured to the letter, CI depends on it:**

| Invocation | Behaviour |
|---|---|
| (default, native mode) | Photino window |
| `--no-window` | No window, system browser (current behaviour) |
| `--no-browser` | **No interface at all**, implies `--no-window` |
| container mode | Unchanged, `app.RunAsync()` |

`--no-browser` must mean "no UI", not "no browser but a window":
`tests/Infrastructure/Invoke-NativeSmokeTest.ps1:172` passes that flag, and a window would
open on CI runners where it would hang waiting to be closed.

**Mandatory fallback** — if creating the window fails (system engine missing, typically
WebKitGTK on Linux), catch `DllNotFoundException` and `TypeInitializationException`, write
a warning on `stderr` and fall back to `SystemBrowserLauncher`. The application must never
die for lack of a webview.

Keep `Program.cs` under 300 lines: extract the window logic into `Hosting/Desktop/`, not
into the top-level statements.

**Acceptance criteria**

- Double-click on `githealth.exe`: a GitHealth window, no browser opened.
- `--no-window`: current behaviour unchanged.
- `--no-browser`: no UI, and the native smoke test passes **without modification**.
- On a machine without a webview engine, the app starts and opens the browser.

**Tests** — unit tests on resolving the display mode from `LauncherOptions` (the matrix of
the 4 rows above). Creating the window itself is not testable in CI; do not try.

**Point to clear as early as this batch**: validate the rendering of the Angular 22 front
end under WKWebView (macOS) and WebView2 (Windows). It is the only risk in the plan that
cannot be discovered by reading it. Do it before starting batch 2.

---

## Batch 2 — Native folder dialog

**Why** — this is the real UX gain over the current HTML folder browser, and the direct
answer to the original problem ("point at a folder").

**Files**

- `src/App.GitHealth.Api/Hosting/Desktop/` — host-side message bridge.
- `src/App.GitHealth.Web/src/app/core/workspace/` — new bridge service.
- `src/App.GitHealth.Web/src/app/shell/scan-folder/scan-folder-dialog.ts`
- `src/App.GitHealth.Web/src/app/shell/add-repository/`

**Work**

Photino exposes a bidirectional `postMessage` bridge between the host and the page. On the
host side: register a web message handler, open the native folder dialog, return the
chosen path. **Check the exact signatures against the Photino version pinned in batch 1**
rather than trusting this document.

On the Angular side, **strictly additive**: a service that detects the presence of the
bridge and uses it if it exists, otherwise falls back to `/api/runtime/directories`
through `src/App.GitHealth.Web/src/app/core/api/git-health-api-client.ts:40`. Both modes
stay alive — Docker and browser mode keep using the HTML browser.

Since the bridge is asynchronous, correlate request and response with an identifier. A
single in-flight request is enough: one modal dialog at a time.

**Acceptance criteria**

- In window mode: the selection button opens the system dialog.
- In a browser or under Docker: the current HTML folder browser, unchanged.
- No regression on the add-repository and scan-folder journeys.

**Tests** — front end: the bridge service tested on both of its branches (bridge present,
bridge absent) with Vitest.

---

## Batch 3 — Velopack: installer and update button

**Files**

- `src/App.GitHealth.Api/App.GitHealth.Api.csproj` — Velopack `PackageReference`.
- `src/App.GitHealth.Api/Program.cs` — **the program's first line**.
- New `src/App.GitHealth.Api/Features/Updates/`.
- `eng/` — `vpk` packaging script.
- `.github/workflows/release.yml`.

**Two traps not to miss**

1. **`VelopackApp.Build().Run()` must be the very first statement**, before
   `LauncherOptionsParser.Parse(args)` (`Program.cs:15`). Velopack intercepts the install
   and update hooks there; placed any lower, it does not work.
2. **Path collision.** Velopack installs by default into `%LocalAppData%\<packId>`, and
   `DataDirectoryResolver.cs:5` already places the database in `%LOCALAPPDATA%\GitHealth`.
   Use **`--packId App.GitHealth`** when running `vpk pack`: the installation goes to
   `%LocalAppData%\App.GitHealth`, the data stays in `%LOCALAPPDATA%\GitHealth`, and an
   update cannot overwrite the database. **No change to `DataDirectoryResolver`.**

**Work**

An abstraction, in line with the D of SOLID in the project conventions:

- `IUpdateService` in `Features/Updates/`, with a status such as
  "unsupported" / "up to date" / "update available".
- `NullUpdateService` — the default implementation, returning "unsupported". This is the
  one used under Docker, in browser mode and on Linux.
- `VelopackUpdateService` — registered **only** when `useNativeLauncher` is true and the
  platform is Windows or macOS. Source: `GithubSource` on
  `https://github.com/LINDECKER-Charles/App.GitHealth`.
- `GET /api/updates` and `POST /api/updates/apply` endpoints, mounted next to
  `MapRuntimeEndpoints`.
- Front end: a discreet button in the shell, shown only when the status justifies it.
  Additive, with no navigation redesign.

Packaging: an `eng/` script modelled on `Publish-Native.ps1`, taking the publish folder and
producing `Setup.exe` (Windows) or `.pkg` (macOS) plus the delta packages. Wire it into
`release.yml` after the native smoke test step, and publish those artefacts **in addition
to** the current archives, not instead of them: the portable archives serve Scoop and the
users who do not want an installer.

**Acceptance criteria**

- `Setup.exe` installs without a UAC prompt and creates a shortcut.
- Post-installation launch: a GitHealth window, database intact between two versions.
- Under Docker and on Linux, `/api/updates` returns "unsupported" and the button does not
  appear.
- The current `.zip` and `.tar.gz` archives are still published.

**Tests** — unit tests on selecting the `IUpdateService` implementation by mode and
platform. Do not test Velopack itself.

---

## Batch 4 — Distribution channels

In order of effort-to-benefit ratio.

1. **Scoop** (Windows, free) — an immediate win: a JSON manifest of about fifteen lines
   pointing at the `githealth-win-x64.zip` that is **already published**. Requires neither
   an installer nor signing. Doable even before batch 3.
2. **winget** (Windows, free) — a PR against `winget-pkgs`. Requires a silent install,
   which the Velopack `Setup.exe` provides.
3. **Homebrew Cask** (macOS) — technically free, but Gatekeeper quarantines any
   unnotarised `.app`. It presupposes settling the Apple developer account question first
   ($99/year). **Blocked, out of scope.**
4. **Linux** — add `linux-x64` to the `ValidateSet` of `eng/Publish-Native.ps1` and a Linux
   runner to the `release.yml` matrix. The `.tar.gz` already works in browser mode, so
   Linux is shippable **before** the window question is settled there. Ideal target next:
   **Flathub**, whose runtime provides WebKitGTK and removes the system dependency problem.

---

## 5. Repository conventions to honour

See `AGENTS.md`. The points that will bite on this work:

- **Commits**: Conventional Commits in English, subject ≤ 72 characters, one commit = one
  coherent change. Scopes: `src/App.GitHealth.Api/**` → `api`,
  `src/App.GitHealth.Web/**` → `front`, `.github/**` → `ci`, `docs/**` → `docs`.
  `eng/**` is not mapped: attach it to `infra`.
- **Branches**: `type/short-description` in kebab-case, one branch per topic.
  One batch = one branch.
- **Tests shipped with the feature**, same branch. Only what is needed, no race for
  coverage; we test neither the framework nor third-party libraries.
- **Limits**: file ≤ 300 lines (400 max), 10 files per folder, method ≤ 30 lines,
  ≤ 3 parameters, nesting ≤ 3, line ≤ 100 characters.
- **A single public element per file**, named after the file.
- `TreatWarningsAsErrors` is enabled (`Directory.Build.props`): no warning gets through.

## 6. Documentation to update at the end of the work

- `docs/KNOWN_LIMITATIONS.md:11` — "There is no installer, automatic update or
  uninstaller yet".
- `docs/IMPLEMENTATION_PLAN.md:367` — "Signed installers and automatic updates".
- `README.md` and `docs/USER_GUIDE.md` — the default installation path becomes the desktop
  application, and Docker becomes the self-hosting mode.
- `ARCHITECTURE.md` — the shell and the message bridge.

## 7. Execution order

Batch 0 → batch 1 (and webview rendering validation) → batch 4.1 (Scoop) → batch 2 →
batch 3 → batch 4.2 then 4.4.

Batches 0 and 1 are the only genuinely blocking ones. Each is deliverable and testable
independently.
