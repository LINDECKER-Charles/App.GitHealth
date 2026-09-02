# Container image and pull-request pipeline

- **Type** — `feat`, `ci`, `fix`
- **Scope** — `infra`, `ci`
- **Landed** — 2026-08-28
- **Commits** — `ebdb1b5`, `0c07edf`, `299c33c`

## What shipped

A multi-stage image and a single-service `compose.yaml`: a data volume for the SQLite
database, a repository mount and a published port bound to the loopback address only. The
listening port becomes configurable instead of being written into the compose file, and
`tests/Infrastructure/Assert-ComposeConfiguration.ps1` asserts the shape of the
configuration rather than trusting it.

The CI validates the foundation on every pull request: restore, build and tests.

## Why

The same artefact has to run natively and in a container from the first step, otherwise the
container becomes a port of the product instead of a packaging of it. Binding to the
loopback address is the default the security model assumes: GitHealth is a local
single-user product, and exposing it on the network has to remain a deliberate act.
