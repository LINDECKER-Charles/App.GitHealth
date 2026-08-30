# Pull request

## Why

<!-- The problem being solved, not the list of files touched. Link to the issue if there
     is one: `Closes #123`. -->

## What changes for the person using GitHealth

<!-- What becomes visible, different or impossible. "No observable change" is a valid
     answer for a refactoring. -->

## How to verify it

<!-- The exact path to follow to observe the result: command, screen, test file that fails
     without the fix. -->

## Type of change

- [ ] `feat` — new feature
- [ ] `fix` — bug fix
- [ ] `docs` — documentation only
- [ ] `refactor` — rework with no behaviour change
- [ ] `perf` — performance
- [ ] `test` — tests only
- [ ] `build` / `ci` / `chore` — tooling and maintenance
- [ ] Breaking change (`BREAKING CHANGE:` present in a commit)

## Checks

- [ ] Commits follow Conventional Commits in French, with the repository scope.
- [ ] The branch follows `type/short-description` and covers a single topic.
- [ ] The feature or the fix is covered by tests, shipped here.
- [ ] `dotnet format --verify-no-changes` and `prettier --check` pass.
- [ ] `dotnet build` in Release produces no warning.
- [ ] `dotnet test` and `npm run test:ci` pass locally.
- [ ] The change honours the project's size, naming and slicing limits.
- [ ] `CHANGELOG.md` is up to date under `[Unreleased]`, if the change is observable.
- [ ] The relevant documentation follows the code.

## Product boundaries

- [ ] No Git write: no reference, index, worktree or reflog modified.
- [ ] No network access added — no `fetch`, no CDN, no telemetry.
- [ ] No author identity and no repository path sent outside the local process.
- [ ] No third-party asset added without an MIT-compatible license and an entry in
      `THIRD-PARTY-NOTICES.md`.

<!-- If one of these boxes cannot be ticked, explain why below. -->

## Additional notes

<!-- Screenshots for a visual change, measurements for a performance change, points to
     watch during review. -->
