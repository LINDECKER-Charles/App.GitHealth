# GitHealth user guide

This guide covers the `0.1.0-rc.1` release candidate. GitHealth runs on your machine,
reads repositories that are already there, and keeps its results in a local SQLite
database.

> [!NOTE]
> The application interface currently ships in French only. The labels quoted in this
> guide are translated to English; look for the equivalent French wording on screen.
> Translating the interface is planned, and tracked in the known limitations.

## Contents

- [What GitHealth does, and never does](#what-githealth-does-and-never-does)
- [Install and start](#install-and-start)
- [Launcher options](#launcher-options)
- [Finding your way around the workspace](#finding-your-way-around-the-workspace)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Adding a repository](#adding-a-repository)
- [Scanning a whole folder](#scanning-a-whole-folder)
- [Reading an analysis](#reading-an-analysis)
- [Understanding the recommendations](#understanding-the-recommendations)
- [Explaining a branch](#explaining-a-branch)
- [Configuring policies](#configuring-policies)
- [Relocating a repository that moved](#relocating-a-repository-that-moved)
- [History and exports](#history-and-exports)
- [Stopping and resuming](#stopping-and-resuming)
- [Frequently asked questions](#frequently-asked-questions)
- [Going further](#going-further)

## What GitHealth does, and never does

GitHealth **observes**. It reads the history of a repository already present on the
machine, derives measurements from it, and offers a reading of the state of its
branches. It decides nothing on your behalf and performs no cleanup action.

| GitHealth does | GitHealth never does |
| --- | --- |
| Read local and remote-tracking branches | Delete, merge or push a branch |
| Compare each branch to a chosen baseline | Check out anything or modify the worktree |
| Measure ahead, behind, merge state and activity | Run `git fetch` or `git remote prune` |
| Identify the contributors of a branch | Clone a repository or handle credentials |
| Offer a recommendation and explain it | Send your data anywhere |
| Keep the analysis history locally | Write anything into the repository |
| Copy the deletion command for you | Run that command |

No analysis writes to the repository: references, index, worktree and reflogs stay
untouched. These guarantees are detailed in the
[security model](SECURITY_MODEL.md).

## Install and start

GitHealth installs like a desktop application: it opens its own window, backed by the
system rendering engine. Portable archives are still published for anyone who prefers to
skip the installer, and Docker mode covers self-hosting.

### Windows

1. Download `App.GitHealth-win-x64-Setup.exe` from the releases page.
2. Run it: GitHealth installs for the current user under
   `%LOCALAPPDATA%\App.GitHealth`, without a UAC prompt, with a Desktop and a Start menu
   shortcut.
3. Open GitHealth: the window appears maximised.

The installer is not signed. If Windows asks for an extra confirmation on first launch,
check where the file came from before continuing.

**Scoop** installs the portable archive rather than the installer: every Windows release
publishes a `githealth.json` manifest next to the archives, and `scoop install` accepts
its URL directly. Because data lives in `%LOCALAPPDATA%\GitHealth`, it survives
`scoop uninstall`.

Without an installer, extract `githealth-win-x64.zip` completely, then run
`githealth.exe`. A repository can be pre-filled at launch:

```powershell
githealth.exe --repo D:\Dev\MyRepository
```

### macOS

Download `App.GitHealth-osx-arm64-Setup.pkg`, or the `osx-x64` variant on an Intel Mac,
then open the package and follow the installation.

Neither the installer nor the archives are signed or notarised. If Gatekeeper blocks the
first launch, check where the file came from before explicitly allowing the application
in the macOS privacy settings.

Without an installer, extract the archive matching your processor, then run `githealth`:

```shell
./githealth --repo "$HOME/Dev/MyRepository"
```

### Linux

Extract `githealth-linux-x64.tar.gz`, then run `githealth`. The window depends on
WebKitGTK there: without that library, GitHealth prints a warning and opens the interface
in the system browser. There is no installer and no in-app update; a new version is
obtained the same way as the first one.

### Self-hosting with Docker

Container mode opens no window: it serves the interface over HTTP, to be opened in a
browser.

Copy `.env.example` to `.env`, point `GITHEALTH_REPOSITORIES_ROOT` at the parent folder
of your repositories, then run:

```shell
docker compose up --build
```

Then open `http://127.0.0.1:8080`. The **Browse** button lists the folders mounted under
`/repositories` and lets you pick the repository without typing its container path.

### Updating

When installed through `Setup.exe` or the macOS package, GitHealth checks whether a newer
version has been published. If so, an **Update** button appears in the top bar: it
downloads the version, installs it and restarts the application. Outside a managed
installation — portable archive, Scoop, Docker, Linux — the button never appears.

If the release source is unreachable, nothing is displayed and nothing fails: the
application stays usable offline. The database lives in a folder separate from the
installation: it survives both updates and uninstallation.

### Prerequisites

Git 2.38 or newer is recommended. GitHealth bundles the .NET runtime, but **not Git**: it
must already be installed on the machine. It looks for it on its own, and the first hit
wins: the path given by `--git-path`, then `git` on the `PATH`, then the standard
installation locations — `%ProgramFiles%\Git\cmd`, `%ProgramFiles(x86)%\Git\cmd` and
`%LOCALAPPDATA%\Programs\Git\cmd` on Windows, `/opt/homebrew/bin`, `/usr/local/bin` and
`/usr/bin` on macOS, `/usr/bin` and `/usr/local/bin` on Linux.

If none of them fits, the interface shows a banner naming the locations it tried, instead
of failing on the first scan. `--git-path <path>`, or the
`GitHealth:Git:ExecutablePath` setting, then points at the executable to use.

The archives are not single-file executables — extract them completely and keep their
files together.

## Launcher options

| Option | Default | Effect |
| --- | --- | --- |
| `--repo <path>` | empty | pre-fills the repository offered on the home screen |
| `--port <1-65535>` | free port | forces a specific port on the loopback interface |
| `--data-dir <path>` | system directory | moves the database and its instance lock |
| `--git-path <path>` | automatic resolution | forces the Git executable to use |
| `--no-window` | desktop window | opens the interface in the system browser |
| `--no-browser` | interface opened | opens no interface at startup |
| `--help`, `-h` | — | prints the help and exits |

The `--repo=…`, `--port=…`, `--data-dir=…` and `--git-path=…` forms are also accepted.
`--no-browser` implies `--no-window`: neither window nor browser, and the interface stays
reachable at the address printed on the console. In container mode no interface is
opened, and both options are moot.

Without `--port`, the system assigns a free port; GitHealth listens only on `127.0.0.1`
and refuses to start rather than silently falling back to a network interface.

The default data locations and the equivalent environment variables are detailed in
[DEVOPS.md](DEVOPS.md).

## Finding your way around the workspace

An opening sequence describes what GitHealth reads at startup. **Skip the intro** or the
`Esc` key cuts it short; it does not replay again during the session. A reduced-motion
setting from the system removes it entirely.

The window opens maximised: the workspace needs at least 1180 CSS pixels of width, and a
fixed size does not guarantee them on a scaled display. Restoring it brings it back to
1360 × 860 pixels, never below 960 × 600.

The screen has three zones:

- the **top bar** carries the global search, the light or dark theme, the data backup and
  the guide — plus **Update** when a newer version has been published;
- the **rail** lists the observed repositories, their reachability and their path,
  arranged in collapsible sections;
- the **central area** presents the current repository under three tabs: **Diagnostic**,
  **History** and **Policies**.

## Keyboard shortcuts

| Shortcut | Effect |
| --- | --- |
| `⌘K` / `Ctrl+K` | open the command palette |
| `↑` `↓` | move through the palette results |
| `Enter` | confirm the highlighted result |
| `Esc` | close the palette or a panel, or cut the opening sequence |

The palette reaches a branch, a repository or an action without leaving the keyboard. On
a repository with several hundred branches, it is the shortest path between an idea and
the matching detail panel.

## Organising the rail: favourites and groups

Hovering over a repository in the rail reveals two actions.

- The **star** pins it in the **Favourites** section, at the very top of the rail. The
  star stays filled and visible once the repository is a favourite. A favourite appears
  there only: it leaves its group's section, and the rail never shows the same repository
  twice.
- The **open folder** opens **Move to a group**: pick an existing group, **Ungrouped**,
  or type a name and select **Create**. A group is born from its first repository and
  disappears when the last one leaves. Both actions are also available in the `⌘K`
  palette for the repository currently open.

Every section header collapses or expands its content, and the counter on the right says
how many repositories it holds. Groups are sorted alphabetically, and so are the
repositories inside a section; **Ungrouped** comes last. As long as no favourite and no
group exist, the rail stays a flat list, with no headers.

Favourites and groups are stored in the local database: they follow the data backup.
Collapsed sections, on the other hand, stay in this machine's browser.

## Adding a repository

1. Select **Add a repository**.
2. Type its absolute path, or use **Browse**. The path is checked as you type: GitHealth
   announces the candidate baselines as soon as it recognises the repository.
3. Choose the display name, the baseline to compare against and the branch scope.
4. Select **Add repository**.

In window mode, **Browse** opens the system folder dialog: the chosen path comes back
into the field. In a browser and under Docker, it shows the folder browser served by the
application.

GitHealth accepts standard repositories, bare repositories and linked worktrees. It does
not clone repositories and uses no remote credentials.

## Scanning a whole folder

To take on several repositories at once, select **Scan a folder**.

1. Type the folder to explore, or use **Browse**.
2. Choose the **depth**: how many levels are inspected below that folder. A repository
   that is found is not opened any deeper, and hidden or build folders are skipped.
3. Select **Detect repositories**. The list distinguishes repositories that are already
   tracked, bare repositories, and those whose references cannot be read — the latter
   cannot be selected.
4. Uncheck whatever should not be measured, then select **Analyse N repositories**.

Unknown repositories are first registered with the proposed baseline and the initial
thresholds; those already tracked keep their configuration. Each repository starts its
analysis as soon as it is registered, without waiting for the others.

Analyses progress **in parallel** up to the limit set by the host, and queue up beyond
that: a repository rejected by a full queue is automatically retried as soon as a slot
frees up. Progress stays readable in the rail. Closing the browser tab interrupts
nothing; closing the desktop window stops GitHealth, and the analyses in flight with it.

## Reading an analysis

Select **Run an analysis** from the dashboard. The visible phases distinguish reading the
topology, enrichment and persistence.

For a baseline `R` and a branch `B`:

- ahead counts the commits reachable from `B` but not from `R`;
- behind counts the commits reachable from `R` but not from `B`;
- activity is the date of the commit the branch points at;
- contributors come from the commits specific to `B`, merge commits excluded.

The capture shows the SHAs that were actually compared. References may move afterwards
without changing that snapshot. GitHealth never runs `fetch` automatically: a remote
branch shown here therefore reflects the local state of `refs/remotes`.

The snapshot is loaded once, then filtered, sorted and counted without another call. The
tiles give the breakdown of recommendations and act as filters. The **Active filters**
chips recall what is narrowing the view and can be removed one by one.

Ticking rows opens the bulk actions: protect, exclude, copy the matching `git` commands,
or export the selection.

## Understanding the recommendations

Each branch receives three independent qualifications: its **topology** relative to the
baseline, its **activity**, and the **recommendation** that follows from them.

### Topology

| Label | Meaning |
| --- | --- |
| **In sync** | the branch and the baseline point at the same commit |
| **Ahead** | it carries commits the baseline does not have, and is not behind |
| **Merged** | its tip is an ancestor of the baseline: all its work is already there |
| **Diverged** | each side carries commits the other does not have |
| **No merge base** | no common ancestor with the baseline |

### Activity

Activity measures the age of the commit the branch points at, not the time spent on it:
Git keeps neither the intent behind its creation nor the checkouts.

| Label | Meaning |
| --- | --- |
| **Active** | more recent than the activity threshold |
| **Ageing** | between the two thresholds |
| **Inactive** | older than the inactivity threshold |
| **Unknown** | no usable tip date |

### Recommendation

| Recommendation | When it appears |
| --- | --- |
| **Keep** | own commits, activity within the thresholds, topology without warning |
| **Review** | inactive, diverged or unrelated branch; or merged past the deadline |
| **Cleanup possible** | no own commits and no activity for a long time |
| **Done** | no own commits, but the deadline is still running — or the tip date is unreadable |
| **Excluded** | a protected or excluded pattern captures the reference, before any other rule |

A **protected** or **excluded** pattern wins over everything else: the branch leaves the
classification, and the detail panel says which of the two patterns captured it.

"Done" is not "Keep". A merged branch no longer holds anything unique — the baseline
already contains its whole history — and the green of "Keep" would suggest it must be
preserved. Green stays reserved for branches carrying commits the baseline does not have.

The branch detail panel always states the rule that was applied in plain words, together
with the threshold scale that was used.

## Explaining a branch

Open a row in the table: the detail panel opens on the right and the branch is written
into the URL, so it can be shared between two tabs of the same local session. The panel
gives:

- the recommendation and the trace of the rules leading to it;
- its topology and its ahead/behind counters;
- the SHAs and the capture time;
- the contributors normalised by `.mailmap`, when one exists;
- the manual deletion command, ready to copy. GitHealth never runs it.

**Protect** and **Exclude** add the reference to the repository's patterns and save the
policy immediately. **Next** walks through the current view without leaving the panel.

After certain merges, Git no longer allows commits to be attributed to their branch of
origin with certainty. GitHealth then reports the attribution as unavailable instead of
inventing an identity.

## Configuring policies

The **Policies** page lets you define:

- how many days a branch counts as active — **30 days** by default;
- the threshold beyond which it becomes inactive — **90 days** by default;
- protected or excluded patterns, with `*` and `?` as wildcards.

Patterns apply to the full reference name, `refs/heads/…` or `refs/remotes/…`, and are
case-sensitive. The inactivity threshold must stay strictly greater than the activity
threshold.

Both thresholds apply to branches that carry their own commits. A branch **merged into
the baseline**, or pointing at the same commit as the baseline, no longer holds anything
unique: the baseline already contains its whole history, and deleting it loses no commit.
It therefore follows a shortened scale, and never goes through "Keep":

| tip age | recommendation |
| --- | --- |
| up to 7 days | **Done**, in purple |
| 7 to 30 days | **Review** |
| beyond 30 days | **Cleanup possible** |

"Done" is not "Keep": there is nothing to preserve, only nothing to do right now. The
green of "Keep" stays reserved for branches carrying commits the baseline does not have.
The branch detail panel states which scale was applied, and why. If the project's own
thresholds are already shorter, those apply instead: the shortened scale never lengthens
anything.

The **Effect on the last snapshot** panel projects the policy being edited onto the facts
already captured, without re-running Git: it compares each recommendation to the saved
policy and lists the branches touched by the patterns. Saving recomputes the current
interpretation, but changes neither the SHAs nor the counters already captured.

## Relocating a repository that moved

If a repository's path changes, open **Policies**, type its new absolute path in
**Relocate repository**, then confirm. GitHealth checks the configured baseline and, if a
successful snapshot exists, the presence of its baseline commit before replacing the
path. The project keeps the same identifier, its policies, all its analyses and its last
successful snapshot. Relocation is refused during an analysis; wait for it to finish,
then try again.

Under Docker, the new path must live under `/repositories`. If the mounted parent folder
on the host changes, recreate the container with the new `GITHEALTH_REPOSITORIES_ROOT`
value first, then use the path as seen from inside the container.

## History and exports

**History** keeps every run and the policy that was used. A failed analysis never
replaces the last successful snapshot.

Each run states its baseline, its thresholds, the number of branches read and the
difference from the previous run. **Policy** expands the patterns captured at that
moment; **Open this snapshot** replays the analysis with the policy of the time.

Three exports serve different needs:

- **Export as CSV** takes the whole current snapshot;
- **Export the selection** takes only the ticked rows;
- **Back up the data** downloads a consistent copy of the entire SQLite database.

The CSV is UTF-8 encoded and neutralises cells a spreadsheet could interpret as formulas.
It contains branch names and author identities: treat it as internal data.

To restore a SQLite backup, stop GitHealth, keep a copy of the current database, replace
`githealth.db`, then restart the application.

## Stopping and resuming

Closing the desktop window, or the `githealth` process, stops the application. In browser
mode, closing the tab leaves the process running: that is what you need to stop. On the
next start with the same data directory, projects, policies and snapshots are restored.
The launcher options and the data locations are detailed in [DEVOPS.md](DEVOPS.md).
An analysis interrupted by an abrupt shutdown is marked **Cancelled** on restart; the
last successful snapshot stays available.

Only one instance can write to a given data directory. To run two in parallel, give the
second one its own `--data-dir`.

## Frequently asked questions

**A remote branch shows a state I know is stale.**
GitHealth never runs `fetch`. It reads `refs/remotes` as it stands on the machine. Run a
`git fetch --prune` in the repository, then run the analysis again.

**The same contributor appears twice.**
They used several addresses. Add a `.mailmap` file to the repository to group them:
GitHealth honours it when it exists.

**The contributors of a merged branch are unavailable.**
After a complete merge, `R..B` is empty and Git no longer allows commits to be attributed
to their branch of origin. GitHealth says so rather than inventing an answer.

**Can GitHealth delete the branches for me?**
No, and that is not planned. The deletion command can be copied from the branch detail
panel or from the bulk actions; running it is up to you, after review.

**Where is my data stored?**
In a local `githealth.db` file, whose location depends on the operating system and on
`--data-dir`. Nothing is sent anywhere. On Windows it lives in `%LOCALAPPDATA%\GitHealth`,
a folder separate from the installation: updating, uninstalling and `scoop uninstall`
leave it intact.

**The "Update" button never appears.**
It only concerns managed installations, on Windows and macOS. From a portable archive,
Scoop, Docker or Linux, updates go through the original channel. Otherwise, either no
newer version has been published, or the release source is unreachable.

**GitHealth cannot find Git, although it is installed.**
It looks on the `PATH`, then in the standard installation locations. An installation
elsewhere is declared with `--git-path <path>`; the warning banner lists the locations
already tried.

**Can I expose GitHealth to my team over the network?**
No. The product is single-user, listens on `127.0.0.1` and has neither authentication nor
isolation. Network exposure is outside its threat model.

**The analysis is slow on a very large repository.**
The measurements and performance budgets are published in
[BENCHMARKING.md](BENCHMARKING.md). Narrowing the branch scope — local branches only —
shortens the read noticeably.

## Going further

- [Troubleshooting](TROUBLESHOOTING.md) — the application does not start, the port is
  taken, Git cannot be found, a repository is rejected.
- [Known limitations](KNOWN_LIMITATIONS.md) — the surprising behaviours that are accepted
  consequences of Git semantics.
- [Security model](SECURITY_MODEL.md) — what the application reads, writes, and never
  sends.
- [Architecture](ARCHITECTURE.md) — how the measurements are computed.
- [Getting help](../.github/SUPPORT.md) — pick the right channel and write a request that
  can be acted on, without exposing real data.
