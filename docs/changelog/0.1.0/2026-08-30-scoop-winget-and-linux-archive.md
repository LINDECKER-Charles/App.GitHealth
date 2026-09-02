# Scoop manifest, winget manifests and the Linux archive

- **Type** — `feat`
- **Scope** — `infra`
- **Landed** — 2026-08-30
- **Commits** — `c37b2eb`, `bc03599`, `0120991`

## What shipped

Three distribution paths added to the release. A Scoop manifest `githealth.json` is
generated and published with every Windows release, pointing at the portable archive
already produced and carrying its exact SHA-256. The winget manifests are generated the
same way, from the version and the checksum of the installer actually published; winget
requires a silent install, which the per-user Velopack installer provides through
`--silent`. And `githealth-linux-x64.tar.gz` joins the published artefacts.

## Why

Both manifests carry the checksum of a specific file, so they can only be produced at
publication time — generating them earlier would mean describing an artefact that does not
exist yet. They ship as release assets for the same reason.

Linux is released as an archive because the browser mode already works there: shipping it
does not have to wait for the window question to be settled. Because data lives in
`%LOCALAPPDATA%\GitHealth` on Windows, it survives `scoop uninstall`.

## Consequences

Submitting the winget manifests to `microsoft/winget-pkgs` stays a human action — the
generated files reduce it to a copy. There is no in-app update on Linux.
