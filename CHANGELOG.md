# Changelog

All notable changes to GitHealth are recorded in this file. The format follows Keep a
Changelog and semantic versioning.

## [Unreleased]

### Added

- **choosing a branch instead of typing it**: in the Policies tab, "Choose…" opens a picker
  listing the repository's references, searchable and navigable with the keyboard, that marks
  the ones an existing pattern already covers. A picked branch is added as an exact pattern;
  the text field stays for globs such as `refs/heads/release/*`. If the repository is out of
  reach, the list falls back to the last capture and says so;

## [0.1.0] - 2026-08-30

First public release. It is a `0.x`: the public contract is not frozen yet, and a later
minor version may still break it.

### Added

- **local branch analysis** with ahead, behind, merge state, activity and contributors,
  explaining the counters, the SHAs, the contributors and the policies applied;
- support for standard repositories, bare repositories and linked worktrees;
- **activity policies**, protected or excluded patterns and a preview: a policy being
  edited is projected immediately onto the last snapshot;
- a branch with no own commits — merged into the baseline, or pointing at the same commit —
  follows a shortened activity scale: ageing after 7 days, inactive after 30. It is never
  recommended as "Keep";
- **"Done" recommendation**, in purple, for a merged branch whose deadline is still
  running;
- unified workspace with a repository rail, tabs and a side branch detail panel;
- filterable dashboard, branch detail and snapshot history: the snapshot is loaded once,
  then filtered, sorted and counted without another call;
- breakdown tiles, active filter chips and bulk actions on a selection;
- `⌘K` command palette to reach a branch, a repository or an action;
- **favourite repositories and groups**: the rail pins favourite repositories at the top
  and arranges the others into named groups, each collapsible in one click. The
  arrangement is written to the database — it follows the SQLite backup — while the
  collapsed state stays local to the browser. A rail with no favourite and no group keeps
  its original flat list;
- remembered dark theme and an opening sequence, both dismissable from the keyboard;
- Établi design system: tokens, IBM Plex fonts and Lucide glyphs served locally, with a
  `--status-merged-*` semantic family declined in light and dark on the plum ramp;
- verified relocation of a moved repository, keeping its history;
- immutable history, showing the number of branches read and the difference from the
  previous run;
- filtered CSV export, produced locally and following the view or the selection, and a
  consistent SQLite backup;
- **scanning a whole folder**: GitHealth detects the Git repositories in it up to a chosen
  depth, flags the ones already tracked, and analyses the retained selection in one go.
  Unknown repositories are registered along the way, each one starting its analysis as soon
  as it is registered;
- analyses progress **in parallel** — `AnalysisQueue:MaximumParallelAnalyses` sets the
  number of queue readers, four by default, with `1` restoring the strictly sequential
  behaviour. A repository rejected by a full queue is retried as soon as a slot frees up;
- **desktop application**: GitHealth opens a native window on double-click. Kestrel and the
  window live in the same process, and the window embeds the system rendering engine —
  WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux. It opens maximised: at a
  fixed size, the workspace's minimum width is not guaranteed on a scaled display. If the
  engine is unusable, the application warns on `stderr` and falls back to the system
  browser instead of stopping. `--no-window` opens the browser directly, and
  `--no-browser` means "no interface at all" — it implies `--no-window` and serves headless
  runs;
- **system folder dialog**: in window mode, "Browse" opens the native folder picker and the
  chosen path comes back into the field. In a browser and under Docker, the HTML folder
  browser is unchanged;
- **installer and in-app updates** on Windows and macOS: `App.GitHealth-win-x64-Setup.exe`
  and `App.GitHealth-<rid>-Setup.pkg` install per user under
  `%LocalAppData%\App.GitHealth`, without a UAC prompt, with Desktop and Start menu
  shortcuts. An "Update" button appears in the top bar only when a newer version has been
  published. The database stays in `%LOCALAPPDATA%\GitHealth`, outside the installation
  folder: it survives both updates and uninstallation;
- self-contained portable archives for Windows x64, macOS Intel, macOS Apple Silicon and
  `linux-x64`, published alongside the installers. The window depends on WebKitGTK on
  Linux — without it, the application opens the browser — and there is no in-app update
  there;
- **Scoop manifest** `githealth.json`, produced and published with every Windows release,
  pointing at the portable archive that is already published. Because data lives in
  `%LOCALAPPDATA%\GitHealth`, it survives `scoop uninstall`;
- **winget manifests** generated and published with the release; submitting them to
  `microsoft/winget-pkgs` remains a human action;
- unprivileged Docker image with repositories mounted read-only, and a Docker Compose
  self-hosting mode — the desktop application being the default installation path;
- **resolving Git outside the `PATH`**: the `--git-path <path>` option or the
  `GitHealth:Git:ExecutablePath` setting takes precedence, then comes the `PATH`, then the
  platform's standard installation locations. `GET /api/runtime` exposes availability, the
  selected path and the diagnostic; without Git, a banner names the locations tried and
  `--git-path` instead of letting the first scan fail;
- **MIT license**: use, modification and redistribution are free, provided the copyright
  notice is kept; `CITATION.cff` provides the metadata to cite the original project;
- code of conduct, contribution guide, support page and notices for the redistributed
  third-party components — IBM Plex fonts under SIL OFL 1.1 and Lucide glyphs under ISC;
- issue and pull request templates, and code owners;
- user guide covering the product scope, the launcher options, reading the
  recommendations, the keyboard shortcuts and frequently asked questions;
- reproducible benchmark up to 1,000 branches and a Playwright end-to-end scenario with a
  non-mutation check.

### Security

- Git commands without a shell, bounded in time and in output volume;
- canonical validation of paths, `commondir`, object databases and alternates;
- isolation of the Git environment for the application, acceptance testing and benchmarks;
- loopback listening, local session, origin check and anti-forgery protection;
- explicit resumption of interrupted analyses, and analysis/relocation mutual exclusion;
- dependency audits, CodeQL, SBOM and provenance in the delivery workflows.

### Limitations

- macOS archives neither signed nor notarised;
- no in-app update on Linux;
- Git has to be installed separately;
- no network fetching and no forge integration;
- a local, single-user product, not meant for network exposure.

[0.1.0]: https://github.com/LINDECKER-Charles/App.GitHealth/releases/tag/v0.1.0
