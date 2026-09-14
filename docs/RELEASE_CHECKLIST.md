# Release checklist

Version being prepared: `0.2.0`. Previous release: `0.1.0`, 30 August 2026.

Every box on this sheet starts unticked. It describes the release being prepared, not a
record of the one that shipped — the record belongs in `docs/release/0.2.0.md` and in
`docs/changelog/0.2.0/README.md`. Resetting the sheet at the start of a cycle is the only
way a tick means something.

The sections are in the order the steps are performed. The two that change the tree — the
version bump and the changelog move — come first, because everything after them validates
a tree that must no longer move: the tag is placed on the commit CI and the release matrix
exercised, bit for bit.

## The version bump

`Directory.Build.props` first, and the reason is not alphabetical. `release.yml` hands
Velopack the tag name — `-Version '${{ github.event.release.tag_name }}'` — so the
installer, its `.nupkg` and the `releases.<rid>.json` feed carry `0.2.0` whatever the tree
says. The assemblies take their version from `VersionPrefix` alone, stamped into
`AssemblyInformationalVersion`. Bump it late and the release ships a `0.2.0` package whose
binaries report `0.1.0` in their file properties, with nothing in the pipeline failing to
say so.

- [ ] `Directory.Build.props` — `<VersionPrefix>0.2.0</VersionPrefix>`
- [ ] `CITATION.cff` — `version: 0.2.0` and `date-released: '2026-09-14'`, which is the
      release date, not the day the field is edited
- [ ] `src/App.GitHealth.Web/package.json` — `"version": "0.2.0"`
- [ ] `tests/App.GitHealth.E2E/package.json` — `"version": "0.2.0"`
- [ ] `src/App.GitHealth.Web/src/app/core/workspace/app-identity.ts` —
      `appVersion = '0.2.0'`. That constant feeds the top bar and the boot sequence only;
      the version the update check compares comes from Velopack through `/api/updates`,
      which is why the two are bumped from different files and must not drift
- [ ] `README.md` — the badge, both the `alt` text and the shields.io URL, and "The
      published version is **`0.2.0`**" in section 07
- [ ] the versioned documents: `docs/USER_GUIDE.md`, `docs/ARCHITECTURE.md`,
      `docs/KNOWN_LIMITATIONS.md`, and the two pointers to the release report in
      `docs/README.md`
- [ ] `docs/SECURITY_AUDIT.md` — never re-titled on its own. The heading moves only
      when the audit is actually redone, because a version number on an unrevised audit is
      a false claim. For `0.2.0` it was redone: the `0.1.0` document asserted "no outbound
      application communication", which the assistant falsifies
- [ ] `.github/ISSUE_TEMPLATE/bug_report.yml` — the version placeholder on the bug form
- [ ] `tests/Infrastructure/Invoke-RealRepositoryAcceptance.ps1` — the `version` field the
      script stamps into its report is a literal. Left alone it labels the 0.2.0 evidence
      `0.1.0`, and the acceptance JSON is the one artefact nobody re-reads afterwards
- [ ] `docs/release/0.2.0.md` written, and `docs/README.md` pointing at it

## The changelog journal

The procedure is in `docs/changelog/README.md`, section "Releasing a version". It has five
steps; the release needs six.

- [ ] **fill every unresolved `Commits` header in `docs/changelog/unreleased/`, before
      anything is moved.** This is the step the README does not state, and it is needed
      every release: an entry travels in the commit it documents, so it cannot name its own
      SHA when it is written. The SHA only exists once the commit lands, and the release is
      the moment somebody goes back and writes it in. Four of the fifteen entries carried
      a placeholder instead of a SHA — `2026-09-02-agent-bridge-and-conversations.md`,
      `2026-09-02-analysis-run-scene.md`, `2026-09-03-watching-the-agent-work.md` and
      `2026-09-14-macos-application-icon.md`. The commit that carried an entry is found by
      its own path:

      ```bash
      git log --oneline v0.1.0..origin/test -- docs/changelog/unreleased/<entry>.md
      ```

      and the feature's other commits over the paths the entry names. An entry covers an
      implementation, not a commit, so a header may list several.

- [ ] create `docs/changelog/0.2.0/`
- [ ] move every file out of `unreleased/`, names unchanged — the date prefix is the day
      the entry's last commit landed, not the release day, and it survives the move
- [ ] write `docs/changelog/0.2.0/README.md`: release date, the tag and the commit it sits
      on, the commit range, and the table of entries
- [ ] synthesise the entries into the `[0.2.0]` section of the root `CHANGELOG.md`, and add
      the tag link at the foot of that file next to the `[0.1.0]` one
- [ ] add the `0.2.0` row to the version index in `docs/changelog/README.md`, and reset
      `unreleased/README.md` to an empty journal whose **Since** reads `v0.2.0`

The "Watch out when releasing" list in `unreleased/README.md` is read at this point and
dispatched: each note either becomes an acceptance check below, a line in the release
notes, or a limitation. Nothing on it is dropped by moving the file.

## Automated checks

Run locally, before the bump is pushed:

- [ ] `dotnet format App.GitHealth.sln --verify-no-changes`
- [ ] `dotnet build App.GitHealth.sln --configuration Release`
- [ ] `dotnet test App.GitHealth.sln --configuration Release --no-build`
- [ ] `npm run format:check --prefix src/App.GitHealth.Web`
- [ ] `npm run test:ci --prefix src/App.GitHealth.Web`
- [ ] `npm run typecheck --prefix tests/App.GitHealth.E2E` and
      `npm run format:check --prefix tests/App.GitHealth.E2E`
- [ ] `npm run i18n:extract --prefix src/App.GitHealth.Web` leaves
      `src/locale/messages.json` unchanged. CI checks that with `git diff --exit-code`, so
      a message added without re-extraction fails the build rather than shipping an
      untranslated string
- [ ] `./tests/Infrastructure/Invoke-BuildEnvironmentTests.ps1`
- [ ] `./tests/Infrastructure/Assert-ComposeConfiguration.ps1`

Green on CI, on the `dev` push that carries the bump:

- [ ] `Verify the baseline` green, and the `promote` job has advanced `test` onto that
      exact commit — not onto the tip of `dev`, which may have moved
- [ ] the Playwright journey: add a repository, analyse, explain a branch, save a policy,
      export, restart and find the data again. The spec also asserts that no external host
      was contacted and that the repository fingerprint is unchanged, which is the
      machine-checked half of the read-only promise
- [ ] the Docker folder-browser spec: a repository selected and validated from the mounted
      folder, then added and analysed
- [ ] relocation: history, repository identity and exclusion of a concurrent scan
- [ ] interrupted analyses resumed at startup with a terminal status
- [ ] `Audit dependencies` green — NuGet with no vulnerable package, and
      `npm audit --audit-level=high` clean in both `src/App.GitHealth.Web` and
      `tests/App.GitHealth.E2E`

## Distribution matrix

- [ ] Windows x64: publication and native smoke test
- [ ] macOS Intel (`osx-x64`): publication and native smoke test
- [ ] macOS Apple Silicon (`osx-arm64`): publication and native smoke test
- [ ] Linux x64: publication and native smoke test
- [ ] Docker: startup, unprivileged UID, non-writable repository mount and persistence
      after recreation
- [ ] Docker Compose: static configuration and confinement validated
- [ ] the rehearsal `release.yml` run that the `promote` job dispatched on `test` is green
      on all four targets and on the Docker job
- [ ] the macOS `.app` installed from the Setup shows GitHealth's own icon in the Dock and
      in Finder. This is the 0.2.0 fix and it only exists in the packaged bundle, so no
      test covers it: a machine that has already run 0.1.0 caches the old grey icon and
      needs `killall Dock` once the new bundle is in place

The `test` matrix runs in rehearsal mode: four native targets and the Docker smoke test,
no installer, no manifest, no attestation. It blocks nothing, because `release.yml` replays
the same matrix from the tag before attaching a single artefact. Checksums, SBOMs,
installers and manifests are produced by that second run, which is why they are verified
after publication rather than here.

## Acceptance testing on real repositories

For each of the two selected repositories, record the commit of the repository under test
without publishing its content, its branch count and the total duration:

- [ ] compare a sample against `git rev-list --left-right --count`;
- [ ] compare `git for-each-ref` before and after;
- [ ] compare the index, the worktree diff and the reflogs before and after;
- [ ] check a merged branch, a diverged one and an inactive one;
- [ ] export CSV and SQLite;
- [ ] restart and find the last successful snapshot again.

Company repositories are never copied into the GitHealth repository. The acceptance report
must contain no author name, no address and no sensitive local path.

Reproducible command, once both paths have been replaced with the selected repositories:

```powershell
dotnet publish src/App.GitHealth.Api/App.GitHealth.Api.csproj `
  --configuration Release --output artifacts/acceptance-app

./tests/Infrastructure/Invoke-RealRepositoryAcceptance.ps1 `
  -RepositoryPath @("D:\Repos\large-1", "D:\Repos\large-2") `
  -PublishDirectory artifacts/acceptance-app `
  -ReportPath docs/release/acceptance-0.2.0.json
```

The script compares up to five branches of each repository with `git rev-list`, requires
merged, diverged and inactive cases, walks the whole pagination, exports CSV and SQLite,
restarts the application and checks the restored snapshot. It finally compares references,
reflogs, index and worktree diff, then anonymises the repositories in the versioned
report.

That script covers the 0.1.0 journey and nothing more. The capabilities added in 0.2.0 are
exercised by hand, on the same two repositories, in the same session:

- [ ] a database written by 0.1.0 opens without a manual step. Both migrations are
      additive: the existing reference is backfilled as the primary baseline and keeps its
      captures, and `Projects.AssistantConsentAtUtc` starts null, so consent is asked once
      per repository rather than inherited;
- [ ] a second and a third baseline added on one repository and measured side by side;
- [ ] the capture selector: one capture read by every tab and carried in the URL as
      `?capture=`. The `analyses/:analysisId` route is removed, so an old bookmark no
      longer resolves — check what it does instead;
- [ ] deleting a capture, then deleting a repository;
- [ ] the author filter and the top contributor;
- [ ] an analysis watched live: the ledger fills reference by reference, the
      `git · read only` console names every command actually run, and **Show the last
      capture** folds the run away without stopping it;
- [ ] the assistant on a repository where consent has been granted: an installed CLI is
      found and named with its version and path, the briefing is readable in full before
      anything is sent, and a run in flight can be stopped;
- [ ] `GitHealth:Assistant:Enabled=false` removes the feature outright, with no interface
      able to turn it back on. This is the answer offered to an installation whose branch
      names are confidential, so it is verified rather than assumed;
- [ ] the exported SQLite file holds the conversations — questions, answers, which agent
      answered, the command line with its token blanked — and no contributor email
      address. That reverses what the previous version's model documented, so the backup is
      opened and read, not trusted.

## Benchmark before the tag

```powershell
gh workflow run benchmark.yml --ref test -f enforce_budgets=true
```

- [ ] dispatched on `test`, on the commit about to be tagged
- [ ] the 100, 500 and 1,000 branch results read against `benchmarks/budgets.json` and the
      verdict written into the release report

`docs/DEVOPS.md` calls this the only step where human judgement is still required, and the
reason is in the budgets themselves: they are absolute P95 ceilings calibrated on a
reference Windows machine, while hosted runners have variable capacity. `enforce_budgets`
turning the run red is therefore information, not a veto. A result outside budget is read
and explained in the report; it does not stop the release.

## CodeQL on the tree being released

`.github/workflows/security.yml` fires on pull requests, on pushes to `main`, on a Monday
cron at 04:17 UTC, and on manual dispatch. It never fires on `dev` and never on `test`. The
tree being released therefore carries no CodeQL result of its own: the pull-request runs
analysed merge candidates that no longer exist as such, and the `main` run only happens
after the fast-forward, which is after the tag. The evidence has to be asked for.

```bash
gh workflow run security.yml --ref test
```

- [ ] dispatched on `test` and green: `CodeQL · csharp` and `CodeQL · javascript-typescript`
      with no blocking alert
- [ ] `Audit dependencies` green in that same run

`Review dependencies` is skipped outside a pull request by design, so its absence from the
dispatched run is expected and is not a gap.

Both dispatches, this one and the benchmark, depend on the workflow file existing on the
default branch — `--ref` chooses which version runs, not whether the workflow exists. All
four workflows are present on `main` today, but a workflow added on `dev` since the last
release would answer `404 not found on the default branch` and the dispatch would have to
wait for the fast-forward.

## Release decision

- [ ] known limitations reviewed in `docs/KNOWN_LIMITATIONS.md`
- [ ] security audit redone in `docs/SECURITY_AUDIT.md`, not merely reviewed. The `0.1.0`
      audit closed by asking to be redone once a feature moved the trust boundary; the
      assistant is that feature. The revision names the three outbound exceptions, rates the
      containment asymmetry between the two agents and the dependence on vendor flags, and
      re-scopes the unencrypted-database finding around the stored conversations
- [ ] `docs/SECURITY_MODEL.md` matches what the agent bridge actually does. It previously
      claimed runs were held in memory and never entered the exportable database; that is
      now false, and the correction is part of this release
- [ ] release notes reviewed
- [ ] branch protection still consistent. `dev` and `main` currently carry `enforce_admins`
      with force pushes and deletions refused, and no required status check; the English
      pass renamed the CI job names that would double as required contexts, so if required
      checks are ever turned on, the new names are the ones to register

### Tag and fast-forward

```bash
git fetch origin
git tag -a v0.2.0 -m "GitHealth 0.2.0" origin/test
git push origin v0.2.0
git push origin origin/test:refs/heads/main
```

- [ ] the annotated tag sits on the `test` commit that CI and the matrix exercised, not on
      the result of a merge — the published object is then exactly the one that was tested
- [ ] `main` fast-forwarded from that same commit. The server refuses the push if it is not
      a fast-forward, which is the guard, not a convention

### Publication

- [ ] immutable releases still disabled on the repository, checked before the release is
      published. `release.yml` attaches every asset in a `publish-release` job that runs
      *after* the release exists — it is triggered by `release: published` and uploads with
      `gh release upload`. An immutable release refuses that upload, and the result is a
      permanently empty release that no rerun can repair
- [ ] GitHub release drafted on `v0.2.0`, left as a normal release. A pre-release is
      skipped by `/releases/latest`, which the README badge link and the Scoop manifest
      both rely on
- [ ] release published — publishing is what triggers `release.yml`, which replays the four
      native targets and the Docker smoke test, builds the installers and manifests, then
      attaches everything once the matrix is green

### The published assets

Twenty-five files, the same set `v0.1.0` carried, with one name changed:

- [ ] the four portable archives: `githealth-win-x64.zip`, `githealth-osx-x64.tar.gz`,
      `githealth-osx-arm64.tar.gz`, `githealth-linux-x64.tar.gz`
- [ ] their four `.sha256` sidecars, each verified against a freshly downloaded archive
      rather than against the build output
- [ ] the four SPDX SBOMs, `githealth-<rid>.spdx.json`
- [ ] the three Velopack installers: `App.GitHealth-win-x64-Setup.exe`,
      `App.GitHealth-osx-x64-Setup.pkg`, `App.GitHealth-osx-arm64-Setup.pkg`. Linux gets
      none by design and keeps its portable archive
- [ ] the three packages, `App.GitHealth-0.2.0-<rid>-full.nupkg`
- [ ] the three update feeds, `releases.win-x64.json`, `releases.osx-x64.json`,
      `releases.osx-arm64.json` — one per channel, because a shared `osx` channel would
      have the two macOS publications overwrite each other. This is the only asset
      `GithubSource` reads: an installed application that finds no feed for its runtime
      offers no update and says nothing about why
- [ ] the Scoop manifest `githealth.json`
- [ ] the three winget manifests, `LINDECKER-Charles.GitHealth.yaml`, `.installer.yaml` and
      `.locale.en-US.yaml`. The locale file was `.locale.fr-FR.yaml` in 0.1.0; the English
      pass moved the file, its `PackageLocale`, the version manifest's `DefaultLocale` and
      the generator together, so the asset name changes this release
- [ ] provenance and SBOM attestations present on the archives
- [ ] the full list matches, checked in one command rather than by eye:

      ```bash
      gh release view v0.2.0 --json assets --jq '.assets[].name'
      ```

- [ ] an installed `0.1.0` on Windows and on macOS is offered the `0.2.0` update and
      applies it — the end of the chain that started with `VersionPrefix`
