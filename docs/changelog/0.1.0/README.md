# 0.1.0 — first public release

- **Released** — 2026-08-30, tagged just after midnight on the 31st
- **Tag** — [`v0.1.0`](https://github.com/LINDECKER-Charles/App.GitHealth/releases/tag/v0.1.0) on `1893972`
- **Range** — `1609b68` … `1893972`, 74 commits without the merge
- **Reader's summary** — [`CHANGELOG.md`](../../../CHANGELOG.md#010---2026-08-30)
- **Acceptance report** — [`docs/release/0.1.0.md`](../../release/0.1.0.md)

The whole MVP, built in four days: a domain of Git facts, a read-only adapter, a
persistence layer, an API, a workspace, then the packaging that turns it into an
installable desktop application. It is a `0.x` — the public contract is not frozen, and a
later minor version may still break it.

## Entries

| Landed | Entry | Type | What it delivers |
|---|---|---|---|
| 2026-08-28 | [Architecture and MVP plan](2026-08-28-architecture-and-mvp-plan.md) | `docs` | The nine-step plan and the reference architecture |
| 2026-08-28 | [A single process serving the API and the interface](2026-08-28-single-process-baseline.md) | `feat` | .NET 10 host, Angular 22, one artefact |
| 2026-08-28 | [Container image and pull-request pipeline](2026-08-28-container-and-pull-request-pipeline.md) | `feat` | Compose on loopback, CI on every PR |
| 2026-08-28 | [The analysis domain, independent of Git](2026-08-28-analysis-domain.md) | `feat` | Metrics, thresholds and recommendations as pure rules |
| 2026-08-28 | [Reading a repository with Git, read-only](2026-08-28-read-only-git-adapter.md) | `feat` | Refs, ahead/behind, merge state, contributors |
| 2026-08-28 | [Projects and analyses that survive a restart](2026-08-28-persisting-projects-and-analyses.md) | `feat` | SQLite, transactional runs, consistent backup |
| 2026-08-29 | [The analysis use cases, exposed over HTTP](2026-08-29-analysis-api.md) | `feat` | Queue, progress, snapshots, Problem Details |
| 2026-08-29 | [The project journey and the dashboard](2026-08-29-project-journey-and-dashboard.md) | `feat` | Add, analyse, read the branch table |
| 2026-08-29 | [Explaining a branch and configuring the policies](2026-08-29-branch-explanation-and-policies.md) | `feat` | Branch detail, thresholds, patterns, CSV, history |
| 2026-08-29 | [Windows, macOS and Docker entry points](2026-08-29-cross-platform-entry-points.md) | `feat` | One launcher, three distributions, smoke tests |
| 2026-08-29 | [Hardened analysis, session protection and relocation](2026-08-29-hardened-analysis-and-relocation.md) | `feat` | Hostile input, local session, verified relocation |
| 2026-08-29 | [Measuring analyses at scale](2026-08-29-benchmarks-at-scale.md) | `perf` | Versioned budgets up to 1,000 branches |
| 2026-08-29 | [Qualifying the release on real repositories](2026-08-29-release-qualification.md) | `test` | Acceptance harness and confined container |
| 2026-08-29 | [Child repositories under Docker, and the `base` tag](2026-08-29-docker-child-repositories-and-csp.md) | `fix` | Two controls stricter than their threat |
| 2026-08-29 | [A shortened activity scale for branches with no own commits](2026-08-29-shortened-scale-without-own-commits.md) | `feat` | The "Done" recommendation |
| 2026-08-29 | [The Établi design system and the unified workspace](2026-08-29-etabli-design-system-and-workspace.md) | `feat` | Tokens, local assets, rail, tabs, `⌘K` |
| 2026-08-30 | [Favourite repositories and groups in the rail](2026-08-30-favourites-and-groups.md) | `feat` | A rail that stays readable at thirty repositories |
| 2026-08-30 | [Scanning a folder and running analyses in parallel](2026-08-30-folder-scan-and-parallel-analyses.md) | `feat` | Discovery, selection, four analyses at once |
| 2026-08-30 | [MIT licence, contribution framework and English documentation](2026-08-30-open-source-release-materials.md) | `chore` | What a public repository is read for |
| 2026-08-30 | [Resolving Git outside the `PATH`](2026-08-30-git-outside-the-path.md) | `feat` | `--git-path`, `/api/runtime`, a named diagnostic |
| 2026-08-30 | [GitHealth opens as a desktop application](2026-08-30-desktop-window.md) | `feat` | Native window, `--no-window`, icon, no console |
| 2026-08-30 | [Choosing a folder through the system dialog](2026-08-30-native-folder-dialog.md) | `feat` | The platform's picker in window mode |
| 2026-08-30 | [Installer and in-app updates](2026-08-30-installer-and-in-app-updates.md) | `feat` | Per-user install, delta updates, data preserved |
| 2026-08-30 | [Scoop manifest, winget manifests and the Linux archive](2026-08-30-scoop-winget-and-linux-archive.md) | `feat` | Three more ways to install it |
| 2026-08-30 | [Local build gateway and the dev → test → main pipeline](2026-08-30-build-gateway-and-promotion-pipeline.md) | `build` | `eng/build`, promotion, diagnosable CI |
| 2026-08-30 | [Dependency updates and two flaky tests closed](2026-08-30-toolchain-and-test-stability.md) | `build` | An atomic PID write, Node pinned to `.nvmrc` |
| 2026-08-31 | [Publishing 0.1.0 rather than 0.1.0-rc.1](2026-08-31-first-stable-version.md) | `chore` | The version, and the green platform matrix |

## Known limitations at release

macOS archives are neither signed nor notarised, there is no in-app update on Linux, Git
has to be installed separately, nothing is fetched from a forge, and the product is local
and single-user — see [`KNOWN_LIMITATIONS.md`](../../KNOWN_LIMITATIONS.md).
