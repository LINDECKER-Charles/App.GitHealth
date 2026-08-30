# Operating the GitHealth platform

## Pinned versions

| Tool                 |     Version |
| -------------------- | ----------: |
| .NET SDK             |    10.0.400 |
| ASP.NET Core runtime |     10.0.11 |
| Node.js              | 24.20.0 LTS |
| npm                  |     11.19.0 |

## Native publishing

The publishing script produces four self-contained distributions:

| System | Architecture | RID | Entry point |
|---|---|---|---|
| Windows | x64 | `win-x64` | `githealth.exe` |
| macOS | Intel | `osx-x64` | `githealth` |
| macOS | Apple Silicon | `osx-arm64` | `githealth` |
| Linux | x64 | `linux-x64` | `githealth` |

To build from a development machine, the entry point is `eng/build`, which offers five
levels — from checking the toolchain up to the installer — and knows the limits of
cross-building. Its prerequisites per operating system are detailed in
[`eng/README.md`](../eng/README.md).

```shell
./eng/build.sh publish      # macOS, Linux
```

```powershell
eng\build.cmd publish       # Windows
```

That entry point delegates to `eng/Publish-Native.ps1`, which CI also calls and which
remains usable directly. From the repository root, it publishes all four targets at once:

```powershell
./eng/Publish-Native.ps1
```

A single target and an output directory can also be specified:

```powershell
./eng/Publish-Native.ps1 `
  -RuntimeIdentifier win-x64 `
  -OutputRoot artifacts/publish
```

Every publication is self-contained, untrimmed and shipped with the Angular bundle. The
script checks the executable and `wwwroot/index.html`, then creates:

- `artifacts/publish/githealth-win-x64.zip`;
- `artifacts/publish/githealth-osx-x64.tar.gz`;
- `artifacts/publish/githealth-osx-arm64.tar.gz`;
- `artifacts/publish/githealth-linux-x64.tar.gz`.

These portable archives are still published alongside the Velopack installers: they serve
Scoop and machines where nothing should be installed. `eng/New-VelopackRelease.ps1`
produces a target's installer from its publish folder, and both
`eng/New-ScoopManifest.ps1` and `eng/New-WingetManifest.ps1` derive the distribution
manifests from it.

The archives are not single-file executables: extract them completely and keep their
files together. The launcher pins its content root to the executable's folder; it can
therefore be called from any current directory. The macOS artefacts of the MVP are
neither signed nor notarised.

Launch examples:

```powershell
D:\Applications\GitHealth\githealth.exe `
  --repo D:\Dev\MyRepository `
  --data-dir D:\Data\GitHealth
```

```shell
/Applications/GitHealth/githealth \
  --repo "$HOME/Dev/MyRepository" \
  --data-dir "$HOME/Library/Application Support/GitHealth"
```

Relative paths passed as options, on the other hand, are resolved from the current
directory. Using absolute paths therefore removes any ambiguity.

### Launcher options

| Option | Default value | Effect |
|---|---|---|
| `--repo <path>` | empty | pre-fills the repository offered on the home screen |
| `--port <1-65535>` | free port | forces a specific port on the loopback interface |
| `--data-dir <path>` | system directory | moves the database and its instance lock |
| `--git-path <path>` | automatic resolution | forces the Git executable to use |
| `--no-window` | desktop window | opens the interface in the system browser |
| `--no-browser` | interface opened | opens no interface at startup |
| `--help`, `-h` | — | prints the help and exits |

The `--repo=...`, `--port=...`, `--data-dir=...` and `--git-path=...` forms are also
accepted. Without `--port`, the system assigns a free port. In every case, the native
launcher listens exclusively on `127.0.0.1`.

In native mode, the default interface is a desktop window backed by the system rendering
engine. `--no-window` uses the browser instead, and `--no-browser` means "no interface at
all" — it therefore implies `--no-window`, and it is the form used by the native smoke
test and the end-to-end tests. In container mode no interface is opened, and both options
are moot.

### Data directories

Without `--data-dir` and without explicit configuration, `githealth.db` is created in:

| System | Default directory |
|---|---|
| Windows | `%LOCALAPPDATA%\GitHealth` |
| macOS | `$HOME/Library/Application Support/GitHealth` |
| Linux | `$XDG_DATA_HOME/GitHealth` or `$HOME/.local/share/GitHealth` |

On Windows, `%USERPROFILE%\AppData\Local\GitHealth` is the fallback when the system does
not provide the local application folder. On Linux, `XDG_DATA_HOME` is only used when it
contains an absolute path.

The `--data-dir` option takes precedence over `GitHealth__DataDirectory`. An explicit
`Persistence__DatabasePath` remains usable when no data directory is imposed.

### Startup diagnostics

The launcher exits with code `1` and an actionable message when an argument is invalid,
when a requested port is already in use, when the data directory is unreachable, or when
SQLite cannot open the database. A `githealth.db.instance.lock` file reserves the database
for the whole lifetime of the process: a second instance targeting the same database fails
clearly, without running a migration or writing to SQLite.

If Git is missing or unusable, the application stays reachable but `/health` reports the
unavailability and describes its cause; installing Git and restarting restores analyses.

The native smoke test exercises the published entry point, the interface, `/health`, the
`--repo` pre-fill, the creation of the database, and then the port and database conflict
diagnostics:

```powershell
./tests/Infrastructure/Invoke-NativeSmokeTest.ps1 `
  -PublishDirectory artifacts/publish/win-x64
```

## Docker Compose

Copy `.env.example` to `.env`, then fill in the root containing the repositories to make
visible. That path is mounted into `/repositories` read-only (`:ro`), and the container
filesystem is read-only too. On Windows, use forward slashes: `D:/Dev/Repos`. The host
port stays `8080` by default; `GITHEALTH_HTTP_PORT` allows another one to be chosen if
that port is already taken, without changing the listener, which stays restricted to
`127.0.0.1`.

The `.` value in the example mounts the root of the GitHealth repository itself. For
normal use, replace it with the absolute path of your repository folder.

```shell
docker compose up --build
```

The application is available on `http://127.0.0.1:8080` only. The named volume
`githealth-data` preserves `/data` when the container is recreated.

To check persistence without deleting the volume:

```shell
docker compose exec githealth touch /data/persistence-check
docker compose up --detach --force-recreate
docker compose exec githealth test -f /data/persistence-check
```

Do not run `docker compose down --volumes` if the data must be preserved.

## Git mount security

The container runs as the unprivileged user of the ASP.NET image. Each Git command marks
as a safe directory only the repository already validated under `/repositories`. It uses
neither the global `safe.directory=*` wildcard nor a descendant wildcard whose behaviour
depends on the Git version.

## Read-only Git analysis

Git is detected by the `/health` diagnostic. Every command runs without a shell, with a
timeout, bounded output and cancellation of the whole process tree. The scanner sets
`GIT_OPTIONAL_LOCKS=0`, `GIT_NO_LAZY_FETCH=1` and `GIT_TERMINAL_PROMPT=0`: it performs no
checkout, no fetch and no reference write. The host's `GIT_TRACE*` variables, global
configuration redirections and Git path redirections are stripped before every process.
The `commondir`, the main object database and every nested alternate are resolved
physically and must stay within the allowed root under Docker.

Batch computation uses the `ahead-behind` atom when it is available. An older Git
installation automatically falls back to `rev-list` with bounded concurrency. Comparisons
always use the identifiers captured at the start of the scan, even if a branch moves
afterwards.

## SQLite persistence

The EF Core migration is applied at startup. In native mode, the database sits in the
system directory described above. Compose explicitly sets
`Persistence__DatabasePath=/data/githealth.db` so that the file stays in the
`githealth-data` volume.

On Unix, a data directory created by GitHealth is restricted to the current user; the
database, its lock and any `-wal` and `-shm` files are readable and writable by that same
user only. A pre-existing parent folder keeps its permissions.

The available options are:

| Configuration | Default | Effect |
|---|---:|---|
| `Persistence__DatabasePath` | `<data>/githealth.db` | path of the SQLite file |
| `Persistence__WriteTimeoutSeconds` | `5` | maximum wait for a write lock |
| `Persistence__RetentionDays` | empty | age of the analyses to delete |

Retention is disabled when its value is empty. When enabled, it never deletes a project's
last successful snapshot. Foreign keys are enabled, the journal uses WAL, and every
completed analysis is persisted with its branches and contributors in a single
transaction. An interrupted or failed analysis therefore does not replace the last
successful result. At startup, any analysis left `Running` after an abrupt shutdown
becomes `Cancelled` with the `analysis.interrupted` code.

The export uses the SQLite backup API while the application stays live, then normalises
the copy to the `DELETE` journal mode. The exported file is self-contained: it can be
archived or restored without a `-wal` or `-shm` file. Before restoring manually, stop
GitHealth, keep a copy of the current database, replace the configured file with the
export, then restart so that any migrations are applied. The backup is downloaded with
`GET /api/exports/database`. The file name includes a UTC timestamp and the response is a
self-contained SQLite database.

## Local API and analyses

The routes under `/api` expose project validation and configuration, the analysis queue,
its progress, the paginated snapshots and their detail. An unknown API route always
returns a JSON Problem Details; it is never absorbed by the Angular application fallback.

`GET /api/session` initialises the local session and the anti-forgery token. Angular calls
this bootstrap before any other request; every API mutation then requires `X-XSRF-TOKEN`.
Requests whose `Host`, origin or navigation context is not loopback/same-origin are
rejected. `/health` stays public on loopback.

`AnalysisQueue__Capacity` limits the number of queued analyses (32 by default, 1,024
maximum). `AnalysisQueue__TimeoutSeconds` bounds a full analysis to 300 seconds by
default and accepts a value between 1 and 3,600 seconds. A project can have only one
active analysis, and an accepted start returns `202 Accepted` with the tracking URL in the
`Location` header.

The Git process limits are validated at startup:

| Configuration | Default | Bounds | Effect |
|---|---:|---:|---|
| `GitHealth__Git__CommandTimeout` | `00:00:30` | 1 to 120 s | duration of a command |
| `GitHealth__Git__MaximumOutputBytes` | 4 MiB | 1 KiB to 16 MiB | stdout and stderr combined |
| `GitHealth__Git__MaximumParallelCommands` | 4 | 1 to 8 | simultaneous Git processes |
| `GitHealth__Git__ExecutablePath` | automatic resolution | — | path of the Git executable |

`GitHealth__Git__ExecutablePath`, like `--git-path`, takes precedence over automatic
resolution: the `PATH`, then the platform's standard installation locations. The first
path that exists wins; `GET /api/runtime` publishes the one that was selected and, failing
that, the list of locations tried.

An out-of-bounds value prevents startup with an explicit diagnostic. The analysis-wide
timeout stays independent from the timeout applied to each Git command.

The HTTP contracts also reject oversized input: repository path limited to 32,768
characters, display name to 200, Git reference to 1,024, scope and pattern to 512. Each
pattern list accepts at most 64 entries. These rejections are controlled Problem Details
and happen before Git is ever started.

## Web journey and execution mode

`GET /api/runtime` tells the interface whether GitHealth is running in native or Docker
mode. In a container, the configured root is displayed and the folder browser starts at
that root. It allows neither going above it nor following a symbolic link that leaves it;
only paths already mounted under that root are accepted.

In native mode, `GET /api/runtime/directories` feeds the local browser. It returns only
reachable folders, sorted and limited to 250 entries per level; it neither reads nor
returns file contents. Access errors become Problem Details, and no technical trace is
exposed to the browser.

The dashboard polls the state of an analysis, limits each page to 50 branches and keeps
the last successful snapshot during a new scan or after a failure. Search, Git relation,
sort and order are reflected in the URL.

## Policies, history and CSV export

A project's policy is changed with `PUT /api/projects/{id}/policy`. That operation does
not restart Git and modifies no captured fact: the last snapshot is merely reclassified
with the current thresholds and patterns. The `POST /api/projects/{id}/policy/preview`
preview applies the same rules without saving them, and states, branch by branch, the
reason for an exclusion or a protection.

The history pages under `/api/analyses/{id}/branches` and a snapshot's detail, by
contrast, keep the policy captured during the analysis.
`GET /api/projects/{id}/analyses` returns that configuration receipt with every run,
including the ones that failed.

The `GET /api/projects/{id}/analyses/latest/branches.csv` export applies exactly the
filters and the ordering of the current view, without pagination. It is UTF-8 encoded and
neutralises cells a spreadsheet could interpret as formulas. It remains distinct from the
SQLite backup, which is meant to restore the whole application.

## Branch model

```
feat/xxx ──PR──► dev ──push──► CI ──green──► test ──► multi-OS matrix
                                             │
                                             ├──► annotated tag ──► published release
                                             │
                                             └──► main (fast-forward)
```

| Reference | What it guarantees | Who feeds it |
|---|---|---|
| `feat/*` | nothing | the developer |
| `dev` | integration: the tip may be red for the duration of a run | pull request |
| `test` | the latest `dev` commit whose CI is green | automatic promotion |
| `main` | the published version | manual fast-forward from `test` |

Every reference advance is a **fast-forward**. The tree validated by CI is therefore
bit-for-bit the one that gets promoted, then the one that gets tagged: no merge commit
introduces a state nobody has exercised. The corollary: nothing must land directly on
`test` or on `main`, or the fast-forward becomes impossible.

## Continuous integration

`.github/workflows/ci.yml` runs on every pull request and on every push to `dev` or
`main`. It restores and builds .NET, checks the .NET, Angular and Playwright formatting,
runs the .NET and Angular tests, publishes the integrated application, checks that the
bundle is present in `wwwroot`, plays the end-to-end journey under Chromium, exercises the
local build targeting rules, validates Compose and analyses the Dockerfile with BuildKit.

The suite is identical on the pull request and on the push that follows the merge. This is
deliberate: the pull request makes `dev` green by construction, and the next run
re-validates the real merge commit before gating the promotion. Trimming the pull request
by removing Playwright from it would save a few minutes on a two-scenario suite, at the
price of an end-to-end regression breaking `dev` after the fact.

## Automatic promotion to test

When CI is green on a push to `dev`, the `promote` job advances `test` onto the exact
commit that has just been validated — never onto the tip of `dev`, which may have moved in
the meantime. It uses the Git references API with `force=false`: the server refuses the
update if the commit is not a direct descendant of `test`, which makes a stale promotion
impossible without any extra guard code.

The `promote-test` concurrency group serialises promotions without cancelling the one in
progress. The concurrency of `ci.yml`, on the other hand, cancels obsolete runs: two pushes
close together on `dev` promote the second and skip the first, which is the intended
behaviour.

A push performed with the `GITHUB_TOKEN` triggers no other workflow — an Actions guardrail
against loops. The job therefore explicitly calls
`gh workflow run release.yml --ref test` to launch the cross-platform matrix on the
promoted commit.

On `test`, that matrix runs in rehearsal mode: it publishes and tests the four native
targets and runs the Docker smoke test, without producing an installer, a manifest or an
attestation. It acts as an **early warning** — a macOS or Docker breakage shows up on
integration day rather than at release time. It blocks nothing, because `release.yml`
replays the very same matrix from the tag before attaching a single artefact.

## Releasing

A release starts from a `test` commit that has already been through CI and the
cross-platform matrix. Before creating the tag, measure performance with the budgets:

```powershell
gh workflow run benchmark.yml --ref test -f enforce_budgets=true
```

This is the only step where human judgement is still required. The budgets in
`benchmarks/budgets.json` are absolute and calibrated on a reference machine, while hosted
runners have variable capacity: the result is read and interpreted, it does not block the
release.

The tag is then placed on the validated commit, and `main` catches up:

```bash
git fetch origin
git tag -a v0.1.0 -m "GitHealth 0.1.0" origin/test
git push origin v0.1.0
git push origin origin/test:refs/heads/main
```

Tagging the `test` commit rather than the result of a merge guarantees that the published
object is exactly the one CI and the matrix exercised. The last push is refused by the
server if it is not a fast-forward.

The `.github/workflows/release.yml` workflow runs manually or when a GitHub release is
published. Its matrix publishes and tests `win-x64` on `windows-latest`, `osx-x64` on
`macos-15-intel`, `osx-arm64` on `macos-15` and `linux-x64` on `ubuntu-latest`. When a
release is published, every target except Linux also produces its Velopack installer, and
the Windows target produces the Scoop and winget manifests. A separate Ubuntu job builds
the image and runs the Docker smoke test: interface available, Git installed, unprivileged
UID, non-writable repository mount and SQLite volume persisting across recreation. Once the
matrix and the Docker smoke test are green, a final job attaches all the archives,
checksums, SBOMs, installers and manifests to the release that triggered the workflow.

## Branch protection

The model rests on one invariant: `test` must remain an ancestor of `dev`. Rewriting
`dev`'s history breaks it — the `promote` job then pushes a reference that is no longer a
direct descendant, the API answers 422, and no promotion succeeds until someone repairs it
by hand. Forbidding force pushes on `dev` and `main` is therefore the only protection that
matters here. Keeping `test` from ever receiving a red commit is already handled by
`needs: verify`, with no branch protection involved.

`test` must carry no rule at all: any rule would cause the `promote` job's push to be
rejected, and `GITHUB_TOKEN` is subject to protection like anyone else.

### Local guard

Server-side protection is unavailable on this repository: private on the GitHub Free plan,
both `branches/*/protection` and `rulesets` answer 403. The guard therefore lives on the
workstation, and is set once per clone:

```bash
git config core.hooksPath eng/hooks
```

For every push to `dev` or `main`, `eng/hooks/pre-push` compares the remote reference with
the local one: if the old commit is not an ancestor of the new one, the push is refused.
It also rejects deleting either branch. It covers the accident, not malice — `--no-verify`
bypasses it, and it only holds on the workstations that enabled it.

### Once server-side protection becomes available

Making the repository public or subscribing to GitHub Pro unlocks both APIs. The minimal
rule, equivalent to the hook, on `dev` and on `main`:

```bash
gh api --method PUT repos/LINDECKER-Charles/App.GitHealth/branches/dev/protection \
  --input - <<'JSON'
{
  "required_status_checks": null,
  "required_pull_request_reviews": null,
  "enforce_admins": false,
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
JSON
```

To go further and make `dev` green by construction, add the required checks
`Vérifier le socle` and `Auditer les dépendances`. Those contexts are the workflow job
names, written in French in `.github/workflows/`, and must be copied exactly. Keep
`strict: false`: requiring an up-to-date branch before merging would be redundant, because
CI replays on the merge commit anyway, and it is that run which gates the promotion.
Required checks impose the pull request on their own — a commit pushed directly has no
result yet and gets refused.

Do not declare the dependency review job or CodeQL as required while the repository is
private without an Advanced Security licence: their job condition makes them skipped, and
a skipped check counts as green. The gate would look active while analysing nothing.

On a public repository, Dependency Review, CodeQL and the GitHub attestations are enabled
automatically. For a private repository with the corresponding GitHub plans, create the
repository variables `ENABLE_GITHUB_SECURITY_FEATURES=true` and
`ENABLE_GITHUB_ATTESTATIONS=true`. Without those licences, the jobs concerned are skipped;
the NuGet/npm audits, the SHA-256 checksums and the SBOMs still run.
