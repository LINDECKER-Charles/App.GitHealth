# Changelog

All notable changes to GitHealth are recorded in this file. The format follows Keep a
Changelog and semantic versioning.

## [Unreleased]

### Added

- **desktop application**: GitHealth opens a native window on double-click. Kestrel and the
  window live in the same process, and the window embeds the system rendering engine —
  WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux. It opens maximised: at a
  fixed size, the workspace's minimum width is not guaranteed on a scaled display. If the
  engine is unusable, the application warns on `stderr` and falls back to the system
  browser instead of stopping;
- **system folder dialog**: in window mode, "Browse" opens the native folder picker and the
  chosen path comes back into the field. In a browser and under Docker, the HTML folder
  browser is unchanged;
- **installer and in-app updates** on Windows and macOS: `App.GitHealth-win-x64-Setup.exe`
  and `App.GitHealth-<rid>-Setup.pkg` install per user under
  `%LocalAppData%\App.GitHealth`, without a UAC prompt, with Desktop and Start menu
  shortcuts. An "Update" button appears in the top bar only when a newer version has been
  published. The database stays in `%LOCALAPPDATA%\GitHealth`, outside the installation
  folder: it survives both updates and uninstallation. The portable `.zip` and `.tar.gz`
  archives are still published alongside the installers;
- **Scoop manifest** `githealth.json`, produced and published with every Windows release,
  pointing at the portable archive that is already published. Because data lives in
  `%LOCALAPPDATA%\GitHealth`, it survives `scoop uninstall`;
- **winget manifests** generated and published with the release; submitting them to
  `microsoft/winget-pkgs` remains a human action;
- **`linux-x64` publication**: `githealth-linux-x64.tar.gz` joins the release artefacts.
  The window depends on WebKitGTK there — without it, the application opens the browser —
  and there is no in-app update;
- **resolving Git outside the `PATH`**: the `--git-path <path>` option or the
  `GitHealth:Git:ExecutablePath` setting takes precedence, then comes the `PATH`, then the
  platform's standard installation locations. `GET /api/runtime` exposes availability, the
  selected path and the diagnostic; without Git, a banner names the locations tried and
  `--git-path` instead of letting the first scan fail;
- **favourite repositories and groups**: the rail pins favourite repositories at the top
  and arranges the others into named groups, each collapsible in one click. The
  arrangement is written to the database — it follows the SQLite backup — while the
  collapsed state stays local to the browser. A rail with no favourite and no group keeps
  its original flat list;
- **scanning a whole folder**: GitHealth detects the Git repositories in it up to a chosen
  depth, flags the ones already tracked, and analyses the retained selection in one go.
  Unknown repositories are registered along the way, each one starting its analysis as soon
  as it is registered;
- analyses now progress **in parallel** — `AnalysisQueue:MaximumParallelAnalyses` sets the
  number of queue readers, four by default, with `1` restoring the strictly sequential
  behaviour. A repository rejected by a full queue is retried as soon as a slot frees up;
- Établi design system: tokens, IBM Plex fonts and Lucide glyphs served locally;
- unified workspace with a repository rail, tabs and a side branch detail panel;
- `⌘K` command palette to reach a branch, a repository or an action;
- remembered dark theme and an opening sequence, both dismissable from the keyboard;
- breakdown tiles, active filter chips and bulk actions on a selection;
- immediate projection of a policy being edited onto the last snapshot;
- **MIT license**: use, modification and redistribution are free, provided the copyright
  notice is kept; `CITATION.cff` provides the metadata to cite the original project;
- code of conduct, contribution guide, support page and notices for the redistributed
  third-party components — IBM Plex fonts under SIL OFL 1.1 and Lucide glyphs under ISC;
- issue and pull request templates, and code owners;
- expanded user guide: product scope, launcher options, reading the recommendations,
  keyboard shortcuts and frequently asked questions.

### Changed

- the native launch now opens a **desktop window**, where it used to open the system
  browser. `--no-window` restores that behaviour, and container mode is unchanged;
- `--no-browser` now means **"no interface at all"** and implies `--no-window`: it serves
  headless runs, including the native smoke test;
- **the desktop application becomes the default installation path**, and Docker the
  self-hosting mode;
- a branch with no own commits — merged into the baseline, or pointing at the same commit —
  now follows a shortened activity scale: ageing after 7 days, inactive after 30. It is
  never recommended as "Keep" again, whereas the previous rule left it as "keep" for three
  months;
- new **"Done"** recommendation, in purple, for a merged branch whose deadline is still
  running. The green of "Keep" wrongly signalled that there was nothing to do and that it
  should not be touched;
- new `--status-merged-*` semantic family in the design system, declined in light and dark
  on the existing plum ramp;
- the snapshot is loaded once, then filtered, sorted and counted without another call;
- the CSV export is produced locally and follows the view or the selection;
- the history shows the number of branches read and the difference from the previous run.

### Fixed

- the global stylesheet did not apply in the published package: the critical CSS `onload`
  handler was blocked by the content security policy;
- a reloaded deep link served an empty page: `base-uri 'none'` blocked the
  `<base href="/">` tag, and the relative URLs of `index.html` resolved from the current
  route. The directive is now `'self'`.

## [0.1.0-rc.1] - 2026-08-29

### Added

- local branch analysis with ahead, behind, merge state, activity and contributors;
- support for standard repositories, bare repositories and linked worktrees;
- filterable dashboard, branch detail and snapshot history;
- activity policies, protected or excluded patterns and a preview;
- verified relocation of a moved repository, keeping its history;
- filtered CSV export and consistent SQLite backup;
- self-contained launchers for Windows x64, macOS Intel and macOS Apple Silicon;
- unprivileged Docker image with repositories mounted read-only;
- reproducible benchmark up to 1,000 branches and a Playwright E2E scenario.

### Security

- Git commands without a shell, bounded in time and in output volume;
- canonical validation of paths, `commondir`, object databases and alternates;
- isolation of the Git environment for the application, acceptance testing and benchmarks;
- loopback listening, local session, origin check and anti-forgery protection;
- explicit resumption of interrupted analyses, and analysis/relocation mutual exclusion;
- dependency audits, CodeQL, SBOM and provenance in the delivery workflows.

### Limitations

- macOS archives neither signed nor notarised;
- no network fetching and no forge integration;
- a local, single-user product, not meant for network exposure.
