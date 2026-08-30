# GitHealth implementation plan

> Status: steps 1 to 8 complete; step 9 implemented and qualified locally
> Reference architecture: [`ARCHITECTURE.md`](ARCHITECTURE.md)

## MVP goal

Deliver a local application that starts from a single executable or with
`docker compose up`, analyses an existing Git repository without modifying it, compares
its branches to a baseline and keeps browsable snapshots in SQLite.

The plan favours demonstrable vertical increments. Every step must leave the branch
buildable and tested; no step depends on a simulated interface that would never be wired
to the final product.

## Execution rules

- A feature and its tests ship together.
- Git commands stay read-only for the whole MVP.
- Displayed metrics come with their definition.
- Expected errors become domain results or Problem Details.
- The .NET, Node and npm versions are pinned from the foundation onwards.
- Performance measurements precede any complex optimisation.
- The last successful snapshot stays visible when a new analysis fails.

## Step 1 — Build the executable foundation

> Status: complete

### Expected outcome

An empty skeleton starts from a single ASP.NET Core process, serves Angular and answers a
health endpoint. The same artefact works in native mode and in a container.

### Work

1. Create the .NET solution and pin the .NET 10 SDK.
2. Create `App.GitHealth.Core`, `App.GitHealth.Api` and their test projects.
3. Initialise Angular 22 in standalone mode, strict TypeScript and SCSS styles.
4. Organise the front end by feature and configure its development proxy.
5. Bundle the Angular build into the static files published by ASP.NET Core.
6. Add `/health`, OpenAPI and a minimal technical home page.
7. Configure the shared compilation and static analysis rules.
8. Create a multi-stage image and a single-service `compose.yaml`.
9. Add a data volume and a read-only repository mount.
10. Create a minimal CI: restore, build and tests on every pull request.

### Exit checks

- [x] `dotnet test` passes from the repository root.
- [x] The publish build contains the Angular application.
- [x] Opening the root URL does not require a separate Node server.
- [x] `docker compose up --build` exposes `127.0.0.1:8080` only.
- [x] The container restarts without losing a marker file placed in `/data`.

## Step 2 — Formalise the analysis domain

> Status: complete on 28 August 2026

### Expected outcome

Metrics and recommendations exist as pure domain rules, independent of Git, SQLite and the
API.

### Work

1. Create the `Project`, `GitRef`, `CommitId` and `BranchComparison` types.
2. Assume no length for a Git object identifier.
3. Model topology, activity and recommendation separately.
4. Introduce an injectable clock for age computations.
5. Define the default thresholds and their validation.
6. Handle excluded/protected patterns without conflating them with a Git state.
7. Define the scanner contracts and the domain errors.

### Test matrix

- ahead and behind both zero: in sync;
- ahead only: ahead;
- behind only with an ancestor: merged;
- ahead and behind: diverged;
- no common ancestor: an explicit state, not a generic error;
- thresholds exactly reached and crossed;
- a protected branch is never a cleanup candidate;
- an inactive, unmerged branch is flagged for review, never for deletion.

## Step 3 — Read a repository with Git

> Status: complete on 28 August 2026

### Expected outcome

A tested adapter validates a repository, lists its references and computes an exact
comparison without touching its index, its worktree or its refs.

### Work

1. Implement a shell-free process runner with timeout and cancellation.
2. Detect Git at startup and expose an understandable diagnostic.
3. Recognise standard repositories, bare repositories and linked worktrees.
4. Canonicalise the path and find the effective working/Git directory.
5. List `refs/heads/*` and `refs/remotes/*`, excluding pseudo-references.
6. Detect `origin/HEAD`, then fall back to offering `main` or `master`.
7. Capture the SHAs before any comparison.
8. Implement the `for-each-ref` fast path with `ahead-behind`.
9. Implement the `rev-list --left-right --count` fallback with bounded concurrency.
10. Detect merges through the ancestor relation, and histories with no merge base.
11. Read the date and the author of each branch tip.
12. Aggregate the authors of `baseline..branch`, excluding merges and honouring mailmap.
13. Cache the enrichment per baseline SHA and branch SHA.

### Git integration fixtures

Build the repositories in a temporary directory with real commits:

- a branch identical to `main`;
- a branch ahead with several authors;
- a branch only behind and fully merged;
- a diverged branch;
- a branch with a merge of the baseline;
- a branch whose dates are old;
- a branch with no common ancestor;
- valid names containing slashes, escaping-sensitive characters or non-ASCII characters;
- a `.mailmap` grouping two identities.

### Exit checks

- The results match the reference Git commands.
- A Git version without the `ahead-behind` atom uses the fallback path.
- A cancellation terminates every child process.
- References, index and worktree are identical before and after the scan.
- Malformed output or a malformed branch name produces a controlled error.

## Step 4 — Persist projects and snapshots

> Status: complete on 28 August 2026

### Expected outcome

Configurations and analyses survive a restart, and a SQLite backup can be exported while
the application is running.

### Work

1. Add EF Core SQLite and create the first schema.
2. Map projects, analyses, branches and contributors.
3. Store every date in UTC and every reference under its full name.
4. Enable foreign keys, WAL and a write timeout.
5. Create the data access repositories the use cases need.
6. Persist a run and its snapshots in transactional batches.
7. Promote only completed analyses as the last successful result.
8. Handle relocation of a path that has become unreachable.
9. Create the export service using the SQLite backup API.
10. Plan a configurable retention policy, disabled by default in the MVP.

### Exit checks

- Migrations work on an empty database and on an already initialised one.
- Stopping mid-scan corrupts neither the previous scan nor the database.
- Two concurrent writes honour the timeout and surface a controlled error.
- The exported backup opens on its own, without depending on a separate WAL file.
- A relocated project keeps its historical analyses.

## Step 5 — Expose the use cases through the API

> Status: complete on 29 August 2026

### Expected outcome

The API allows registering a project, configuring its baseline, starting an analysis and
consulting its progress and its results.

### Work

1. Implement path validation and reference discovery.
2. Implement project creation and listing.
3. Implement changing the baseline, the thresholds and the exclusions.
4. Create an analysis queue with an ASP.NET Core worker.
5. Reject or deduplicate two simultaneous analyses of the same project.
6. Return `202 Accepted` and a tracking URL on start.
7. Expose the progress: queued, topology, enrichment, persistence, done.
8. Expose the snapshots paginated, sorted and filtered server-side.
9. Expose a snapshot's detail and its contributors.
10. Add the SQLite backup endpoint.
11. Standardise errors with Problem Details and stable codes.

### Exit checks

- In-memory API tests with a real temporary SQLite database.
- Validation of non-existent, non-Git and disallowed paths.
- The last successful result is returned during a scan or after a failure.
- Stable pagination even when several branches have similar names.
- Clean cancellation when the host shuts down.

## Step 6 — Deliver the project journey and the dashboard

> Status: complete on 29 August 2026

### Expected outcome

A user can complete the main journey without knowing Git commands, and immediately
understand which branches need attention.

### Work

1. Create the home screen and the list of recent projects.
2. Create adding by typed path and, in native mode, a folder browser.
3. Clearly display the allowed root in Docker mode.
4. Offer the detected baseline while allowing it to be changed.
5. Display the provenance of the data: local/remote and scan time.
6. Create the analysis start and the progress tracking by polling.
7. Create the branch table with:
   - name and reference space;
   - ahead and behind;
   - merge state;
   - last activity and age;
   - main author when it can be determined;
   - recommendation and rationale.
8. Add sorting, search, combinable filters and pagination/virtualisation.
9. Keep the useful filters in the URL.
10. Handle the empty, loading, error and unreachable-repository views.

### Exit checks

- The whole journey works from the keyboard.
- Information never relies on colour alone.
- A 1,000-row table stays navigable without rendering everything in the DOM.
- Long names and non-ASCII characters do not break the layout.
- An analysis error does not erase the previous result.

## Step 7 — Explain branches and configure policies

> Status: complete on 29 August 2026

### Expected outcome

Every recommendation can be verified in a branch detail panel, and the rules can be
adapted to the repository's conventions.

### Work

1. Create the snapshot detail page.
2. Display the definition of ahead, behind and activity.
3. Display the contributors, their commit counts and whether mailmap was applied.
4. Explicitly report an impossible attribution after a merge.
5. Display the SHAs used and the capture time.
6. Create the editing of the active/ageing/inactive thresholds.
7. Create the exclusion and protection patterns with a preview of the matches.
8. Add the quick filters: merged, inactive, diverged, to review.
9. Add a CSV export of the filtered view, distinct from the SQLite backup.
10. Browse a project's analysis history.

### Exit checks

- Changing a policy recomputes the interpretation without falsifying the Git facts.
- A protected branch displays the exact reason for its exclusion.
- The CSV honours the filter, UTF-8 encoding and international names.
- Historical data stays attached to the policies used during the scan.

## Step 8 — Finalise the Windows, macOS and Docker entry points

> Status: complete on 29 August 2026
> Since then: the native launch opens a desktop window and `--no-window` restores the
> system browser. The launcher also accepts `--git-path`, and `linux-x64` is published.

### Expected outcome

All three distributions launch the same product and use the same migrations and API
contracts.

### Work

1. Add the launcher to the ASP.NET Core process.
2. Pick an available loopback port and open the default browser.
3. Implement `--repo`, `--port`, `--data-dir` and `--no-browser`.
4. Determine the data directories that comply with Windows and macOS conventions.
5. Produce the self-contained publications for the supported architectures.
6. Verify graceful shutdown and the absence of orphaned Git processes.
7. Finalise the Docker image with Git installed and an unprivileged user.
8. Document `.env`, the Windows/macOS paths and the `:ro` mount.
9. Add the native and Docker smoke tests to the release matrix.
10. Provide a startup diagnostic for missing Git, unavailable port or invalid database.

### Exit checks

- A single launch starts the API and the interface on every platform.
- Two instances do not corrupt the same database and report the conflict clearly.
- The server never listens on the network without a future explicit option.
- The container does not write into the mounted repository.
- The same fixtures produce the same metrics in all three environments.

## Step 9 — Harden, measure and prepare the first version

> Status: implemented and qualified locally on 29 August 2026; final validation at the RC tag

### Expected outcome

The MVP is reproducible, documented and robust enough to be tested on real company
repositories.

### Work

1. Generate a reproducible benchmark of 100, 500 and 1,000 branches.
2. Measure topology, enrichment, persistence and rendering separately.
3. Set the budgets after the first baseline and version them.
4. Verify the time, output and concurrency limits of Git processes.
5. Test path traversal, symbolic links and hostile Git arguments.
6. Add origin protection, anti-forgery and a local session token.
7. Verify that no author data leaves the machine.
8. Add an end-to-end scenario from launch to export.
9. Complete the README, user guide, troubleshooting and known limitations.
10. Produce a release candidate and test it on two large real repositories.

### MVP exit criteria

- Installation and launch demonstrated on Windows, macOS and Docker.
- Accuracy manually compared against Git on the fixture suite.
- No observed change in the analysed repositories.
- The add → configure → analyse → detail → export journey works.
- Restart and migration without losing the last successful snapshot.
- Common Git errors understandable without reading technical logs.
- Results of a 1,000-branch benchmark published in the repository.

### Validation results

- Windows baseline published for 100, 500 and 1,000 branches with P95 budgets;
- 195 .NET tests, 43 Angular tests and the Playwright journey passing;
- NuGet/npm audits with no published vulnerability, and a versioned application audit;
- two anonymised real repositories validated with Git metrics, exports and restart;
- references, reflogs, index and worktree diffs identical before and after acceptance;
- Windows publishing validated locally; the macOS and Docker criteria still have to be
  confirmed by the tag matrix before declaring the step complete.

## Main acceptance scenario

1. Launch GitHealth from the platform's entry point.
2. Add a fixture repository containing at least six branch topologies.
3. Choose `main` as the baseline and `refs/remotes/origin/*` as the scope.
4. Start the analysis and follow its progress.
5. Verify ahead/behind with `git rev-list --left-right --count`.
6. Filter the merged and inactive branches.
7. Open a diverged branch and check its contributors.
8. Protect a pattern, change a threshold and verify the new interpretation.
9. Export CSV and SQLite.
10. Restart the application and find the last result again.
11. Compare refs, reflogs, index and worktree diff against their state before acceptance.

## Risks to watch

| Risk | Planned treatment |
|---|---|
| Stale remote references | State that the analysis performs no fetch, and date the scan |
| Old Git on macOS | Capability detection and a fallback path |
| Thousands of branches | Batch computation, cache, pagination and versioned budgets |
| Merged branch without reliable attribution | Show it as undetermined and keep the history |
| Docker paths differing from the machine | Controlled root and guided relocation |
| Inconsistent SQLite copy | An endpoint using the backup API |
| Hostile command coming from a branch name | No shell, separate arguments and dedicated tests |
| macOS blocking an unsigned binary | Document it, then sign before wide distribution |

## After the MVP

Suggested priority:

1. Managed mirror clones and an explicit `fetch` that never touches worktrees.
2. Git provider integrations and detection of open pull requests.
3. Manual grouping of author identities.
4. Trends between snapshots and local notifications.
5. Installer signing, macOS notarisation and Homebrew Cask publishing.

Deleting a remote branch stays deliberately outside GitHealth until an approval workflow
and a provider integration have been designed.
