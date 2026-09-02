# Reading a repository with Git, read-only

- **Type** — `feat`
- **Scope** — `api`
- **Landed** — 2026-08-28
- **Commits** — `4921a32`

## What shipped

The Git adapter: a shell-free process runner with a timeout, bounded output and
cancellation; startup detection of Git with a readable diagnostic; recognition of standard
repositories, bare repositories and linked worktrees; path canonicalisation and resolution
of the effective working and Git directories.

It lists `refs/heads/*` and `refs/remotes/*` excluding pseudo-references, detects
`origin/HEAD` and falls back to offering `main` or `master`, captures the SHAs before any
comparison, then computes ahead/behind through the `for-each-ref … ahead-behind` fast path
with a `rev-list --left-right --count` fallback for older Git versions. Merges are detected
through the ancestor relation, histories with no merge base are reported as such, and each
branch's tip date and author are read alongside the authors of `baseline..branch`, excluding
merge commits and honouring `.mailmap`. Enrichment is cached per baseline SHA and branch SHA.

Integration fixtures build real repositories in a temporary directory: a branch identical
to `main`, one ahead with several authors, one merged, one diverged, one with a merge of
the baseline, one with old dates, one with no common ancestor, names with slashes,
escaping-sensitive and non-ASCII characters, and a `.mailmap` grouping two identities.

## Why

Every metric the product shows has to be verifiable against the reference Git command, so
the adapter is tested against real repositories rather than against recorded output. No
shell and separate arguments close the whole class of attacks a hostile branch name would
otherwise open, and the exit check is stated as a property: references, index and worktree
are byte-identical before and after a scan.
