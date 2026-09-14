# GitHealth carries its own icon on macOS

- **Type** — `fix`
- **Scope** — `eng`, `api`, `docs`
- **Landed** — 2026-09-14
- **Commits** — _pending_

## What shipped

The macOS application shows GitHealth's icon — in the Dock, in Finder, in the application
switcher — instead of the grey placeholder every Velopack application shares. Windows was
never affected: it has carried `githealth.ico` since the first release.

The artwork now exists in the two containers the two systems read, side by side next to the
launcher: `src/App.GitHealth.Api/githealth.ico` and `src/App.GitHealth.Api/githealth.icns`.
`eng/New-VelopackRelease.ps1` hands the packager the one its target reads, through the new
`Get-PackageIconPath` rule in `eng/BuildEnvironment.ps1`; a target with no icon declared, or
an icon missing from the working tree, now stops the packaging with a sentence naming the
file it expected.

Only the `.ico` travels in the publication — the window reads it at runtime on Windows. The
`.icns` is a packaging asset and carries no `Content` entry in the project file, so no
platform pays a megabyte for an icon it cannot read.

## Why

`vpk pack` does not fail when it is handed no icon: it writes its own `DefaultApp.icns` into
the bundle, points `CFBundleIconFile` at it, and reports success. Nothing in the release log
says the application just shipped unbranded — which is how `0.1.0` reached macOS with a
generic icon and no one noticed until it was installed.

A rule that refuses beats a flag that can be forgotten, so the icon is resolved by a
function with the target as its parameter, tested in
`tests/Infrastructure/Invoke-BuildEnvironmentTests.ps1` alongside the other targeting rules
and run by CI on every pull request. The failure mode was never a build error; it was a
silent substitution. It is now an error.

The `.icns` is derived from `src/App.GitHealth.Web/public/icons/icon-512x512.png`, laid out
on Apple's grid — the rounded square filling 824 of a 1024 canvas, the rest transparent —
rather than used edge to edge. The inset is not decoration: a full-bleed icon reads as
visibly larger than its neighbours in the Dock, and the artwork already had the corner
radius of the system shape. `docs/DEVOPS.md` describes the regeneration for the day the
artwork changes.

## Consequences

macOS caches the icon of a bundle it has already seen. An installation sitting in
`/Applications` keeps showing the old grey icon until a release built from this change
replaces the bundle; `killall Dock` refreshes it once the new bundle is in place.
