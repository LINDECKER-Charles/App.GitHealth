# Windows, macOS and Docker entry points

- **Type** — `feat`
- **Scope** — `api`, `infra`
- **Landed** — 2026-08-29
- **Commits** — `2a9575f`

## What shipped

One launcher inside the ASP.NET Core process, picking an available loopback port and
opening the default browser, with `--repo`, `--port`, `--data-dir` and `--no-browser`. The
data directory follows the platform's convention on Windows and macOS. Self-contained
publications are produced for the supported architectures, and the Docker image is
finalised with Git installed and an unprivileged user.

Two smoke tests join the release matrix — `Invoke-NativeSmokeTest.ps1` and
`Invoke-DockerSmokeTest.ps1` — covering launch, graceful shutdown and the absence of
orphaned Git processes. A missing Git, an unavailable port or an invalid database produce a
startup diagnostic instead of a stack trace.

## Why

Three distributions must run the same product, the same migrations and the same API
contracts, otherwise "it works on Docker" stops being evidence about the native build. The
smoke tests are the mechanism that keeps that true: they are run per platform at release
time, on the artefact that is actually published.
