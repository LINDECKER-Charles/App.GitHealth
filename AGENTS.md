# App.GitHealth

A .NET / C# project. B-Hive conventions installed by `/b-hive-init`; the cross-cutting
conventions (commit format, branch naming) live in the global `AGENTS.md` and are
installed by `/b-hive-dev-convention`.

## commit

Commit convention (maintained by /commit, initialised by /b-hive-init).
- Style: Conventional Commits — language: en
- Scopes (path → scope):
  - `src/App.GitHealth.Api/**` → `api`
  - `src/App.GitHealth.Core/**` → `core`
  - `src/App.GitHealth.Web/**` → `front`
  - `.github/**` → `ci`
  - `docs/**` → `docs`
  - `docker*`, `compose*`, `deploy*`, `k8s*`, `.gitlab-ci*` → `infra`
  - cross-cutting configuration at the root (`*.sln`, `Directory.*.props`, `.editorconfig`,
    `global.json`) → `chore` (no scope)

Extend this map as new projects appear — a `src/App.GitHealth.<Zone>` project gives the
scope `<zone>` in lowercase.

Cross-cutting rules: tests (`tests/**`, `*Tests.cs`, `*.Tests/**`) travel with the code
they test; changelog entries are attached to the feature/fix commit they document.

## Code conventions

These conventions apply to all the project's code. The **numeric limits** are ceilings to
respect; the **principles** are defaults to follow, unless there is an explicit, justified
reason to depart from them.

### Guiding principles

- **DRY (Don't Repeat Yourself)** — No duplication of logic or of domain knowledge: a rule
  lives in a single place. (Nuance: do not abstract before the 3rd repetition — a one-off
  duplication beats a bad abstraction.)
- **KISS (Keep It Simple)** — Choose the simplest solution that actually solves the
  problem. No gratuitous complexity or cleverness.
- **SOLID**:
  - **S — Single responsibility**: a class or a module has only one reason to change.
  - **O — Open/closed**: open to extension, closed to modification.
  - **L — Liskov substitution**: a subtype must be able to replace its parent type without
    breaking the expected behaviour.
  - **I — Interface segregation**: prefer several focused interfaces to one catch-all
    interface.
  - **D — Dependency inversion**: depend on abstractions, not on concrete implementations.

### Size and complexity limits (verifiable)

| Rule | Limit |
|---|---|
| File size | ≤ 300 lines (warning), 400 maximum |
| Files per folder | ≤ 10 (beyond that, split into subfolders by domain) |
| Function / method size | ≤ 30 lines |
| Number of parameters | ≤ 3 (beyond that, group them into an object / a struct) |
| Nesting depth | ≤ 3 levels |
| Cyclomatic complexity | ≤ 10 per function |
| Line length | ≤ 100 characters |

- **A single public element per file** (one class, component or module per file), named
  after the file.
- **No magic numbers or strings** — extract them into named constants that explain their
  intent.

### Naming

- **Explicit names that reveal intent** — the name says *what* and *why*, not *how*. A long
  clear name beats a short obscure one.
- **Consistent conventions** across the whole project — idiomatic C# casing, never mixed:
  `PascalCase` for types, methods, properties and constants; `camelCase` for local
  variables and parameters; interfaces prefixed with `I` (`IRepositoryScanner`); private
  fields in `_camelCase`.
- **No cryptic abbreviations** — `userCount`, not `usrCnt`. Only universal abbreviations
  are tolerated (`id`, `url`, `http`).
- **Booleans prefixed** with `Is`, `Has`, `Should`, `Can`… (e.g. `IsActive`, `HasAccess`,
  `ShouldRetry`).

### Functions

- **One function = one thing** — if you have to write "and" to describe what it does, split
  it.
- **Favour pure functions** — avoid side effects where possible, and make them explicit
  when they are necessary.
- **Guard clauses / return early** — handle edge cases and return early instead of nesting
  `if/else`.
- **Avoid flag parameters** — a boolean that changes behaviour hides two functions
  disguised as one; split them.
- **CQS (Command Query Separation)** — a function either *changes* state OR *returns* a
  value, never both.

## Tests

- **Every feature ships with tests**, delivered alongside it (same branch, same PR).
- **Only what is needed to test the feature**: the nominal behaviour and the edge cases it
  introduces. No chasing a coverage percentage, no redundant tests; we test neither the
  framework nor third-party libraries.
- A good test fails when the feature's **behaviour** breaks — not when its implementation
  changes.
