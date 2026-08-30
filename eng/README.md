# Installing and building GitHealth

This guide starts from a blank machine and stops when GitHealth opens in front of you.
Allow **about twenty minutes**, most of it spent downloading. No knowledge of .NET or
Angular is required: the commands can be copied as they are.

Three sections, one per operating system. **Follow the one for your machine, from start to
finish**, and ignore the other two:

- [Windows](#windows) · [macOS](#macos) · [Linux](#linux)

What will be installed, in every case: Git, the .NET 10 SDK, Node.js 24, and — on macOS
and Linux only — PowerShell. Plan for a few gigabytes for the toolchain and the downloaded
dependencies; the built application weighs about 120 MB.

---

## Windows

### 1. Install the tools

Open **PowerShell** (Start menu, type "PowerShell") and paste:

```powershell
winget install --id Git.Git --exact
winget install --id Microsoft.DotNet.SDK.10 --exact
winget install --id OpenJS.NodeJS.LTS --exact
```

**Close the PowerShell window and open a fresh one.** Without that, the commands you have
just installed stay unavailable.

PowerShell itself does not need installing: the one shipped with Windows is enough.

### 2. Get the code

```powershell
cd $HOME
git clone https://github.com/LINDECKER-Charles/App.GitHealth.git
cd App.GitHealth
```

### 3. Check that everything is in place

```powershell
eng\build.cmd check
```

The command prints a table of the tools it found. Everything must show `OK`, except
possibly Node.js showing `Écart` ("mismatch") — winget ships the second-to-last patch of
the expected version, which is **harmless** for building locally.

### 4. Build the application

```powershell
eng\build.cmd publish
```

Allow five to ten minutes the first time: .NET and npm download their dependencies, then
the interface and the server are compiled into a self-contained executable. The result
lands in `artifacts\publish\win-x64\`.

### 5. Run it

```powershell
eng\build.cmd run
```

The GitHealth window opens. To start directly on a repository:

```powershell
eng\build.cmd run --repo C:\Path\To\MyRepository
```

---

## macOS

### 1. Install the tools

Open **Terminal** (⌘ + Space, type "Terminal"). If Homebrew is not installed yet, install
it first — it is the macOS package manager:

```shell
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

Then:

```shell
brew install git node@24
brew install --cask powershell
echo 'export PATH="$(brew --prefix)/opt/node@24/bin:$PATH"' >> ~/.zprofile
source ~/.zprofile
```

PowerShell is required here: the build scripts are shared with Windows and with continuous
integration, as a single implementation.

### 2. Get the code

```shell
cd ~
git clone https://github.com/LINDECKER-Charles/App.GitHealth.git
cd App.GitHealth
```

### 3. Install the .NET SDK

Microsoft's official script reads `global.json` and installs **exactly** the version the
project expects, with no administrator password:

```shell
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --jsonfile ./global.json
rm dotnet-install.sh

echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> ~/.zprofile
echo 'export PATH="$DOTNET_ROOT:$PATH"' >> ~/.zprofile
source ~/.zprofile
```

### 4. Check that everything is in place

```shell
./eng/build.sh check
```

The tools table must show `OK` everywhere. An `Écart` ("mismatch") on Node.js signals a
patch-level difference from the pinned version: harmless locally.

### 5. Build the application

```shell
./eng/build.sh publish
```

Allow five to ten minutes the first time. The result lands in
`artifacts/publish/osx-arm64/` — or `osx-x64/` on an Intel Mac, the script picks on its
own.

### 6. Run it

```shell
./eng/build.sh run
./eng/build.sh run --repo ~/Dev/MyRepository    # start on a specific repository
```

> Locally built artefacts are neither signed nor notarised. That has no effect here, since
> you compiled them yourself.

---

## Linux

Instructions for Ubuntu and Debian. On Fedora, replace `sudo apt install` with
`sudo dnf install` and `sudo snap install powershell --classic` with
`sudo dnf install powershell`.

### 1. Install the tools

```shell
sudo apt update
sudo apt install -y git curl libicu-dev
curl -fsSL https://deb.nodesource.com/setup_24.x | sudo -E bash -
sudo apt install -y nodejs
sudo snap install powershell --classic
```

PowerShell is required here: the build scripts are shared with Windows and with continuous
integration, as a single implementation.

### 2. Get the code

```shell
cd ~
git clone https://github.com/LINDECKER-Charles/App.GitHealth.git
cd App.GitHealth
```

### 3. Install the .NET SDK

Microsoft's official script reads `global.json` and installs **exactly** the version the
project expects, with no `sudo`:

```shell
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --jsonfile ./global.json
rm dotnet-install.sh

echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> ~/.bashrc
echo 'export PATH="$DOTNET_ROOT:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

### 4. Check that everything is in place

```shell
./eng/build.sh check
```

The tools table must show `OK` everywhere. An `Écart` ("mismatch") on Node.js signals a
patch-level difference from the pinned version: harmless locally.

### 5. Build the application

```shell
./eng/build.sh publish
```

Allow five to ten minutes the first time. The result lands in
`artifacts/publish/linux-x64/`.

### 6. Run it

```shell
./eng/build.sh run
./eng/build.sh run --repo ~/Dev/MyRepository    # start on a specific repository
```

On Linux, the window relies on WebKitGTK. If the library is missing, GitHealth says so and
opens the interface in the system browser: the application stays fully usable. To get the
window, install the rendering engine — on recent Ubuntu and Debian,
`sudo apt install -y libwebkit2gtk-4.1-0`; the package name varies from one distribution to
another.

---

## What next

Once the application is built, the other `build` levels cover the rest of the cycle.
Replace `./eng/build.sh` with `eng\build.cmd` on Windows.

| Command | What it does |
|---|---|
| `./eng/build.sh check` | the machine's toolchain and the target it can produce |
| `./eng/build.sh dev` | interface and server live, reloading on every save |
| `./eng/build.sh publish` | the self-contained application, as it is distributed |
| `./eng/build.sh run` | runs what `publish` produced |
| `./eng/build.sh installer` | the installer and the update feed |

### Changing the code

`dev` is the level for day-to-day work: it starts the interface and the server together,
and reloads the interface on every save — no rebuild needed.

Go back through `publish` only to check what shows up in the assembled application alone:
the desktop window, static file serving, the content security policy, deep links.

### Producing the installer

```shell
./eng/build.sh publish
./eng/build.sh installer
```

Produces the installer, the packages and the update feed in
`artifacts/velopack/<target>/`. Velopack refuses to overwrite a version that has already
been packaged: to replay the operation, bump the version with `-Version 0.1.1`.

There is **no installer on Linux**, by choice: distribution there goes through the
portable archive that `publish` has already produced.

### Building for another operating system

A machine only produces a usable artefact for its own operating system. A cross
publication does compile — useful to check that a change works everywhere — but it is not
distributable: a Unix archive built from Windows loses the execute bit on its binaries, and
nothing can be tested for an absent platform. The script warns about it rather than
forbidding it.

Installers, on the other hand, are refused: Velopack relies on the target system's
toolchain. For a publishable artefact of another system, go through continuous
integration, whose matrix builds each target on its own runner:

```shell
gh workflow run release.yml --ref "$(git branch --show-current)"
gh run watch
gh run download
```

A manual run uploads the portable archives as workflow artefacts. The installers, the
winget / Scoop manifests and the attestations only arrive when a GitHub release is
published — the full journey is described in
[`docs/RELEASE_CHECKLIST.md`](../docs/RELEASE_CHECKLIST.md).

---

## If something does not work

| Message | What is happening | What to do |
|---|---|---|
| `git` / `dotnet` / `node`: command not found | the terminal was opened before the install | close the terminal, open a fresh one |
| `pwsh: command not found` | PowerShell missing on macOS or Linux | redo step 1 of your section |
| `permission denied: ./eng/build.sh` | execute bit lost on copy | `chmod +x eng/build.sh` |
| `Dépendances absentes` (missing dependencies) | npm dependencies not installed | `npm ci --prefix src/App.GitHealth.Web` |
| `Aucune publication <target>` (no publication) | `run` or `installer` before `publish` | run `publish` first |
| `There is a release ... equal or greater` | version already packaged by Velopack | run again with `-Version 0.1.1` |
| Node.js showing `Écart` (mismatch) | patch level differs from the pinned version | nothing, it has no local effect |
| .NET SDK showing `Écart` (mismatch) | major version differs from `global.json` | reinstall through the ".NET SDK" step |
| Unreadable accents in the messages | the UTF-8 byte order mark was stripped from a `.ps1` | restore it, see below |

The `check` table stays the first reflex: it says what is missing before the build fails.

> The build scripts print their messages in French. The quoted strings above are what
> appears on screen; the English in brackets is only there to help.

---

## What this folder contains

| File | Role |
|---|---|
| `build.sh` · `build.cmd` | the entry points — gateways to `build.ps1` |
| `build.ps1` | the build levels, a single implementation for all three systems |
| `BuildEnvironment.ps1` | machine target, toolchain, cross-building rules |
| `Publish-Native.ps1` | self-contained publication and portable archive |
| `New-VelopackRelease.ps1` | installer and update feed |
| `New-WingetManifest.ps1` · `New-ScoopManifest.ps1` | distribution manifests, produced by continuous integration |

Two things to know before touching them:

- The logic is written **once**, in PowerShell, and it is that same implementation which
  `.github/workflows/release.yml` runs. A local build therefore follows the path of a
  release build. On Windows, PowerShell 5.1 is enough; elsewhere, PowerShell 7 is required.
- The `.ps1` files carry a **UTF-8 byte order mark**. Without it, PowerShell 5.1 reads them
  in the machine's ANSI code page and renders accents unreadable. Keep it when rewriting
  them.

The cross-building rules are covered by
`tests/Infrastructure/Invoke-BuildEnvironmentTests.ps1`, run on every pull request. The
full release chain is described in [`docs/DEVOPS.md`](../docs/DEVOPS.md), and the
contribution conventions in [`.github/CONTRIBUTING.md`](../.github/CONTRIBUTING.md).
