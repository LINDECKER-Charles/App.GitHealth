# The application speaks English, and is ready for other locales

- **Type** — `refactor`, `feat`, `docs`, `ci`, `build`, `test`, `chore`
- **Scope** — `core`, `api`, `front`, `ci`, `docs`, `eng`
- **Landed** — 2026-08-31
- **Commits** — `bc43363`, `8bc79ae`, `67830c5`, `55e5c21`, `5dff8f1`, `315c8de`, `498e53b`, `8ab5b99`, `736b12b`

## What shipped

Every screen label, empty state, toast and error message, the launcher's `--help` output,
the log templates, the Problem Details, the build and infrastructure scripts, the workflow
job names, the benchmark harness, the root configuration comments and the whole
documentation are written in English. The CLI help now matches `docs/USER_GUIDE.md`
verbatim, so code and documentation cannot drift apart.

The interface is also prepared for translation: 464 user-facing messages carry an explicit
id, extracted to `src/locale/messages.json`, with `@angular/localize` wired for runtime
catalogues. `main.ts` resolves the locale and loads its catalogue before importing the
application graph, since `$localize` memoises each message on first evaluation;
`bootstrap.ts` exists to keep that import lazy. The build stays single-locale on purpose —
compile-time inlining would move the output under a locale subdirectory and break the
publish path in `App.GitHealth.Api.csproj`. CI re-extracts the catalogue and fails when the
committed file no longer matches the code.

Three bugs the wording alone would have hidden are fixed along the way: `plural()` encoded
the French rule and rendered "0 branch", replaced by `pluralMessage()` selecting an
`Intl.PluralRules` category; `referenceSource()` returned `'locale' | 'distante'` as a
discriminant, now `'local' | 'remote'`, with `deleteCommand()`'s comparison moved with it so
remote branches keep being offered `git push --delete`; and dates, numbers and byte sizes
followed a hard-coded `fr-FR` locale with a decimal-comma replacement, and now follow the
application locale.

`features/branch-fiche` is renamed to `branch-card`, selector and CSS classes included.
The Conventional Commits convention switches to English, declared identically in
`AGENTS.md`, `CONTRIBUTING.md` and the pull request template. Past commits are left as they
are. The README screenshots are regenerated from the English build.

## Why

Message ids are explicit rather than generated because prettier reflows template text and
auto ids hash it: formatting alone would otherwise rotate every id.

Deliberately non-ASCII fixtures are untouched — accented ref names, paths and author names
exist to prove UTF-8 and CSV-injection handling, and folding them would gut those tests.
Analysis failure messages already stored in SQLite keep their French text: they are data,
and this change does not rewrite existing rows.

## Consequences

**Breaking change on the CI contexts.** The job names that double as required status checks
are renamed, so the branch protection rules must be updated in the repository settings or
merges will block on contexts that no longer exist: "Vérifier le socle" becomes "Verify the
baseline", "Auditer les dépendances" becomes "Audit dependencies" and "Examiner les
dépendances" becomes "Review dependencies" — the last two stay distinct because they are
two separate contexts.

The UTF-8 BOM stays on every `.ps1` file; Windows PowerShell 5.1 depends on it. The winget
locale manifest becomes `en-US`: the file, its `PackageLocale`, the version manifest's
`DefaultLocale` and the generator that writes them all move together.
