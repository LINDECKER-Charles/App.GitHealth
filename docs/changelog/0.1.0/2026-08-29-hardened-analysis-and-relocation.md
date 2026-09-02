# Hardened analysis, session protection and repository relocation

- **Type** — `feat`
- **Scope** — `api`, `front`
- **Landed** — 2026-08-29
- **Commits** — `eb33038`, `2d3f47a`

## What shipped

The scanner is hardened where hostile input reaches it: paths, `commondir`, object
databases and alternates are validated canonically, path traversal and symbolic links are
tested rather than assumed safe, the Git environment is isolated for the application, the
acceptance runs and the benchmarks, and the process runner's time, output and concurrency
limits gain their own tests (`GitScannerOptionsValidatorTests`, `GitProcessRunnerTests`).

A repository that has moved is relocated instead of being re-added: the new path is checked
to be the same repository before the history is carried over, and relocation and analysis
exclude each other so neither can run against a path the other is changing. An analysis
interrupted by a shutdown is resumed explicitly rather than left in limbo.

The session is protected on the browser side: loopback listening, a local session token,
an origin check and anti-forgery. A Playwright end-to-end scenario runs the journey from
launch to export, with a check that the analysed repository is unchanged.

## Why

The product reads repositories it does not own, so a branch name is untrusted input all the
way down. Verifying that the relocated path is the same repository is the difference
between keeping a history and silently attaching it to someone else's work.

The non-mutation check belongs in the end-to-end test rather than in the documentation: it
is the one promise the whole product rests on, and it has to fail a build when it stops
being true.
