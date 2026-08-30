# Contributing to GitHealth

Thank you for your interest in the project. This document explains how to report a
problem, prepare your environment, write code that follows the repository conventions, and
open a pull request.

Taking part in the project implies respecting the [code of conduct](CODE_OF_CONDUCT.md).

## License of contributions

GitHealth is distributed under the [MIT license](../LICENSE). By offering a contribution,
you agree that it will be published under that same license, and you confirm that you have
the right to submit it. There is no CLA to sign.

Do not integrate code, fonts, icons or text whose license is unknown or incompatible with
MIT. Every added dependency or third-party asset must be declared in
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Before opening an issue

Start by checking:

1. the [user guide](../docs/USER_GUIDE.md), which describes the expected behaviour;
2. the [troubleshooting guide](../docs/TROUBLESHOOTING.md);
3. the [known limitations](../docs/KNOWN_LIMITATIONS.md) — several surprising behaviours
   are accepted consequences of Git semantics;
4. the existing issues, both open and closed.

Then pick the right channel:

| Situation | Channel |
| --- | --- |
| Reproducible bug | **Bug report** issue |
| New idea or need | **Feature request** issue |
| Wrong or incomplete documentation | **Documentation** issue |
| Usage question | see [SUPPORT.md](SUPPORT.md) |
| **Security vulnerability** | **never a public issue** — see [SECURITY.md](SECURITY.md) |

Never attach a company repository path, an internal branch name, an author address or an
extract of a SQLite database to a public issue. Anonymise before publishing.

## Project scope

GitHealth **observes** a repository, it never modifies it. The following contributions will
be declined, however good they are:

- deleting, merging, checking out or pushing a branch;
- automatically running `git fetch` or `git remote prune`;
- cloning a remote repository or handling credentials;
- sending paths, branch names or author identities to an external service;
- turning the product into a multi-user application exposed on a network.

The full scope and the reasons behind it are described in
[ARCHITECTURE.md](../docs/ARCHITECTURE.md). A change that touches those boundaries is
discussed in an issue **before** any code is written.

## Preparing the environment

Versions are pinned by `global.json` and `.nvmrc`; honouring them avoids CI failures that
are hard to diagnose.

| Tool | Version | Source |
| --- | --- | --- |
| .NET SDK | 10.0.400 | `global.json` |
| Node.js | 24.20.0 LTS | `.nvmrc` |
| npm | 11.19.0 | `packageManager` |
| Git | 2.38 or newer | runtime prerequisite |
| PowerShell 7 | for the `eng/` and `tests/Infrastructure/` scripts | macOS and Linux |
| Docker | for the Compose check | optional |

On Windows, the PowerShell 5.1 shipped with the system is enough; elsewhere, PowerShell
installs in one command, detailed in [`eng/README.md`](../eng/README.md).

Restoring the dependencies from the repository root:

```shell
dotnet restore App.GitHealth.sln
npm ci --prefix src/App.GitHealth.Web
npm ci --prefix tests/App.GitHealth.E2E
```

## Development loop

A single entry point covers local builds on all three operating systems, from day-to-day
work up to the installer. The available levels and their limits are detailed in
[`eng/README.md`](../eng/README.md).

```shell
./eng/build.sh dev      # macOS, Linux
```

```powershell
eng\build.cmd dev       # Windows
```

The `dev` level starts the Angular interface and the API in one terminal and stops them
together. Both processes can also be launched by hand, as long as you keep the flags the
script passes: without `--port`, the native launcher picks a random port and the Angular
proxy can no longer find the API; without `--no-browser`, it opens a desktop window on an
empty `wwwroot`.

```shell
# terminal 1 — API on http://localhost:5115
dotnet run --project src/App.GitHealth.Api -- --no-browser --port 5115
```

```shell
# terminal 2 — Angular interface on http://localhost:4200
npm start --prefix src/App.GitHealth.Web
```

The Angular development server forwards `/api`, `/health` and `/openapi` to the API through
`proxy.conf.json`. In development, the `http://localhost:4200` origin is explicitly allowed
by `LocalSecurity:AllowedOrigins`; in production, the interface and the API share the same
origin.

To reproduce the application as it ships — the Angular bundle served from `wwwroot` —
publish it and run the result:

```shell
./eng/build.sh publish
./eng/build.sh run --repo "$HOME/Dev/MyRepository"
```

Several behaviours only show up in that integrated mode, in particular those tied to the
content security policy and to deep links. A fix touching static file serving, the CSP or
routing must be verified that way.

## Code conventions

The full conventions live in [AGENTS.md](../AGENTS.md). The essentials:

- **DRY, KISS, SOLID** as defaults; departing from them requires an explicit reason;
- **a single public element per file**, named after the file;
- **no magic number or string** — a named constant states the intent;
- **guard clauses** rather than nested `if/else`;
- **CQS** — a function either changes state or returns a value, never both;
- idiomatic C# naming: `PascalCase` for types and members, `camelCase` for locals and
  parameters, `I` in front of interfaces, `_camelCase` for private fields;
- booleans prefixed with `Is`, `Has`, `Should`, `Can`.

Verifiable limits, to be honoured:

| Rule | Limit |
| --- | --- |
| File size | ≤ 300 lines (warning), 400 maximum |
| Files per folder | ≤ 10 |
| Function size | ≤ 30 lines |
| Number of parameters | ≤ 3 |
| Nesting depth | ≤ 3 levels |
| Cyclomatic complexity | ≤ 10 per function |
| Line length | ≤ 100 characters |

The repository builds with `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`: a warning
is a build failure, not a detail to handle later.

Formatting is not negotiable and does not have to be discussed in review — it is applied by
tooling:

```shell
dotnet format App.GitHealth.sln
(cd src/App.GitHealth.Web && npx prettier --write .)
(cd tests/App.GitHealth.E2E && npx prettier --write .)
```

## Tests

**Every feature ships with its tests**, in the same branch and the same pull request. The
principle is to cover the nominal behaviour and the edge cases the feature introduces — no
more, no less. We test neither the framework nor third-party libraries, and we do not chase
a coverage percentage.

A good test fails when the **behaviour** breaks, not when the implementation changes.

| Suite | Location | Role |
| --- | --- | --- |
| Domain | `tests/App.GitHealth.Core.Tests` | classification rules, policies, computations |
| API | `tests/App.GitHealth.Api.Tests` | HTTP entry points, analysis queue, security |
| Git | `tests/App.GitHealth.Git.IntegrationTests` | real reads of purpose-built repositories |
| End to end | `tests/App.GitHealth.E2E` | the user journey under Playwright |

Running them:

```shell
dotnet test App.GitHealth.sln
npm run test:ci --prefix src/App.GitHealth.Web
```

The end-to-end tests need a publication and Chromium:

```shell
(cd tests/App.GitHealth.E2E && npx playwright install --with-deps chromium)
GITHEALTH_E2E_PUBLISH="$PWD/artifacts/publish" \
  npm run test:ci --prefix tests/App.GitHealth.E2E
```

The Git integration tests build their own repositories in temporary folders and isolate the
Git environment. Never point them at a real repository on your machine.

## Checks before pushing

This sequence reproduces what CI does. Running it locally avoids the round trip:

```shell
dotnet format App.GitHealth.sln --verify-no-changes
npm run format:check --prefix src/App.GitHealth.Web
npm run typecheck --prefix tests/App.GitHealth.E2E
npm run format:check --prefix tests/App.GitHealth.E2E
dotnet build App.GitHealth.sln --configuration Release
dotnet test App.GitHealth.sln --configuration Release --no-build
npm run test:ci --prefix src/App.GitHealth.Web
```

If the contribution touches the `eng/` scripts:

```shell
pwsh ./tests/Infrastructure/Invoke-BuildEnvironmentTests.ps1
```

If it touches Docker or Compose:

```shell
pwsh ./tests/Infrastructure/Assert-ComposeConfiguration.ps1
docker buildx build --check .
```

The workflows are detailed in [docs/DEVOPS.md](../docs/DEVOPS.md).

## Branches

A branch starts from `dev` and returns to it through a pull request. One branch = one
topic. `dev` is the integration branch: `test` and `main` are advanced automatically or at
release time, never by hand. The model is detailed in
[docs/DEVOPS.md](../docs/DEVOPS.md).

```
type/short-description
```

- **type**: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `style`, `build`, `ci`,
  `chore` — always the short form, `feat/` and never `feature/`;
- **description**: `kebab-case`, without accents, two to five words stating what the branch
  is about.

Examples: `feat/scan-dossier-parallele`, `fix/csp-base-uri`,
`docs/guide-utilisateur-politiques`.

## Commits

The repository follows **Conventional Commits**, written **in French**:

```
type(scope): description
```

- description in the infinitive, lowercase initial, no trailing period;
- subject of **72 characters maximum**;
- optional body, reserved for the *why*;
- a breaking change is signalled by a `BREAKING CHANGE:` footer;
- **one commit = one coherent change**: never mix two topics.

The scope is mandatory as soon as the map below covers the modified files:

| Path | Scope |
| --- | --- |
| `src/App.GitHealth.Api/**` | `api` |
| `src/App.GitHealth.Core/**` | `core` |
| `src/App.GitHealth.Web/**` | `front` |
| `.github/**` | `ci` |
| `docs/**` | `docs` |
| `docker*`, `compose*`, `deploy*`, `k8s*` | `infra` |
| cross-cutting configuration at the root | no scope, type `chore` |

Tests travel with the code they test — they take its scope, not a separate `test` scope.
The changelog entry is attached to the commit of the feature or the fix it documents.

Example:

```
feat(front): ajouter la palette de commandes au clavier
fix(api): rejeter la relocalisation pendant une analyse en cours
docs(docs): documenter l'échelle réduite des branches fusionnées
```

## Changelog

Every user-visible change — feature, fix, behaviour change, new limitation — is added to
the `[Unreleased]` section of [CHANGELOG.md](../CHANGELOG.md), under `Added`, `Changed`,
`Fixed`, `Security` or `Limitations`.

The entry describes what the person using GitHealth observes, not the internal mechanics. A
refactoring with no observable effect produces no entry.

## Opening a pull request

1. Create the branch from `dev`, honouring the naming above.
2. Write the code, its tests and its changelog entry.
3. Run the local verification sequence.
4. Open the pull request against `dev` and fill in the template.
5. A pull request that is still work in progress opens as a **draft**.

A good pull request explains **why** the change exists, what it changes for the person
using the tool, and how to verify it. A link to the original issue with `Closes #123`
closes it automatically on merge.

Keep pull requests small and focused on one topic. A massive rename mixed with a fix makes
review impossible: split them in two.

## Review and merge

CI must be green before any review. What is looked at first:

- does the observed behaviour match what is announced;
- do the tests actually fail without the fix;
- are the product boundaries respected — no Git write, no network access, no author
  identity leak;
- are the size, naming and slicing conventions honoured;
- do the documentation and the changelog follow the code.

Review feedback is about the code, never about the person. A question in review is a
question, not a reproach.
