# Local build gateway and the dev → test → main pipeline

- **Type** — `build`, `ci`, `fix`, `docs`
- **Scope** — `eng`, `ci`, `docs`
- **Landed** — 2026-08-30
- **Commits** — `dc773b3`, `67d4ba9`, `d4ccb07`, `a5295a5`, `b66752c`, `16f467a`, `f636a4f`, `82aa01f`

## What shipped

`eng/build` (`.ps1`, `.cmd`, `.sh`) with `BuildEnvironment.ps1` becomes the single entry
point for local builds, with its infrastructure tests and its installation guide.

The delivery pipeline gains a fast-forward promotion job from `dev` to `test`, publication
moves from the tag event to the release event, Dependabot targets `dev`, and the build
targeting rules are asserted rather than assumed. `GH_REPO` gives `gh` its repository
context in the promotion job, which does no checkout and used to fail on "not a git
repository" — leaving `test` advanced and the platform matrix never triggered.

The platform tests are made diagnosable: `--blame-hang` names the test that freezes, the
`TestResults` directory is kept as an artefact on failure, and restore, build and run are
separated so a step timeout can point at the guilty phase. `DEVOPS.md` records the
`workflow_dispatch` constraint — GitHub only allows dispatching workflows present on the
default branch, so a workflow added on `dev` stays untriggerable until `main` catches up —
and the API documentation justifies why the `XSRF-TOKEN` cookie is readable by the client.

The local pre-push hook is removed once the repository goes public: branch protection now
covers `dev` and `main` server-side, including administrators.

## Why

The `osx-arm64` job froze twice on the Git integration tests without ever reporting its
logs, at the job timeout or on cancellation. A step timeout fails cleanly where a job
timeout cancels everything, which is what lets the trace collection run.

The pre-push hook is deleted rather than kept as a second line: it duplicated a server-side
rule in a weaker form, bypassable with `--no-verify` and only active on the machines that
had installed it.
