# Dependency updates and two flaky tests closed

- **Type** — `build`, `test`
- **Scope** — `chore`, `api`, `infra`
- **Landed** — 2026-08-30
- **Commits** — `bd63448`, `d44e090`, `6990c97`, `656a226`

## What shipped

The dotnet test group moves to Test.Sdk 18, xunit.runner 4 and coverlet 10 — still on
VSTest, xunit 2.9.3 unchanged, with the same 254 tests discovered — and
`anchore/sbom-action` goes from 0.24.0 to 0.24.1, carrying Syft 1.42.3 → 1.51.1 with an
identical `action.yml`.

Two sources of intermittent failure are closed. The test host publishes its PID through an
atomic rename instead of `File.WriteAllText`, which made the file visible before its
content: `ProcessTreeProbe` waited on `File.Exists` then parsed, and read an empty string at
random, failing `CancellationStopsTheEntireDescendantProcessTree` in CI. And an assertion
now pins the Dockerfile's Node image to `.nvmrc`.

## Why

Hardening the read would not have been enough for the PID: a partial content parses into a
truncated but valid PID, turning a noisy failure into a silent false positive. The fix goes
on the write side, where the window closes completely. Measured over 200,000 iterations on
Linux, the CI platform: 195,383 empty reads with the direct write, none with the
publish-by-rename.

The Node version was hard-coded in the Dockerfile with nothing confronting it to `.nvmrc`,
the source of truth for the three workflows. An image bump leaving `.nvmrc` behind passed
CI in silence: the shipped bundle would have been compiled by a runtime other than the one
validating the repository, and the licence notices would have named an upstream image no
longer in use.

## Consequences

Bumping the Node image becomes an atomic change — no pull request can carry it alone
anymore.
