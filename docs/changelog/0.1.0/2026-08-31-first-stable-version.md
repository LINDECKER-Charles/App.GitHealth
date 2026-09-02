# Publishing 0.1.0 rather than 0.1.0-rc.1

- **Type** — `chore`, `docs`
- **Scope** — `chore`, `docs`
- **Landed** — 2026-08-31
- **Commits** — `a8694c6`, `1893972`

## What shipped

The first version is published as `0.1.0`. `VersionSuffix` and `InformationalVersion`
disappear from `Directory.Build.props` — MSBuild derives both from `VersionPrefix`, and the
explicit line overwrote the `+sha` suffix SourceLink adds. The changelog merges
`[Unreleased]` into `[0.1.0]`.

The publication matrix then runs on `2197c58`: the four runtimes and the Docker smoke test
are green, and `linux-x64` joins the document's matrix, which had not listed it although it
had been published since the archive was added.

## Why

The `0.` prefix already carries the absence of a compatibility guarantee; the `-rc.1`
suffix repeated it, at the cost of a release marked as a pre-release that
`/releases/latest` ignores — the README and the Scoop manifest would then have had to point
at a hard-coded version.

`0.1.0-rc.1` was never tagged nor published, and a public journal does not document a
version nobody could install.

## Consequences

The SHA-256 checksums and the attestations still have to be verified against the artefacts
actually attached to the release.
