# A journal behind the changelog, version by version

- **Type** — `docs`
- **Scope** — `docs`
- **Landed** — 2026-09-02
- **Commits** — `419e02c`

## What shipped

`docs/changelog/` holds one folder per version and one file per implementation. Each entry
states what was built, why it was built that way and what it costs, and carries the short
SHAs in its header so every claim traces back to a diff. The journal arrived complete: 27
entries written for `0.1.0` and 7 for what was merged but not yet released, all reconstructed
by reading the history back — 41 files changed and 1,200 lines added in one commit.

`TEMPLATE.md` fixes the shape: an H1 naming the capability, the four header bullets Type,
Scope, Landed and Commits, then `What shipped`, `Why`, and `Consequences` only when there is
one. Each folder carries a `README.md` indexing its entries as a table — date landed, link,
type, one line of contents — so a version reads top-down without opening every file.
`0.1.0/README.md` adds the release date, the tag, the commit range and the limitations known
at release; `unreleased/README.md` adds a "Watch out when releasing" list, already flagging a
removed route and a renamed CI context. `docs/changelog/README.md` holds the conventions, the
five-step release procedure and the version index.

Three existing files were touched. `AGENTS.md` already required a changelog entry to be
attached to the feature or fix commit it documents; the rule now names both destinations —
the reader's line in `CHANGELOG.md` and the detailed entry in `docs/changelog/unreleased/`.
`CHANGELOG.md` and `docs/README.md` gained a pointer to the journal.

## Why

The root `CHANGELOG.md` is written for whoever installs GitHealth: a few lines per
capability, and no room for why a shape was chosen or what it cost. That reasoning otherwise
survives only in commit messages, recoverable by replaying the history commit by commit,
which nobody does.

Two rules carry the weight.

**An entry covers an implementation, not a commit.** A feature whose API half and interface
half landed separately is one file naming both SHAs; a translation pass spread over nine
commits is one file too. Conversely, one commit carrying two distinct capabilities is split
into two entries, each naming that same commit. Indexing on commits would produce a folder
shaped exactly like the Git history, which already exists and which `git log` already reads.

**Files are named `YYYY-MM-DD-short-slug.md`, dated with the day the entry's last commit
landed.** Date-first, so a folder sorts chronologically on its own. A sequence number would
sort too, and it was rejected for one reason: it would have to be rewritten the day the file
moves from `unreleased/` into its version folder. The date is a fact about the change and
never changes, so the move is a `git mv` and nothing more.

A `docs` entry describing a working convention rather than a product capability has a
precedent here:
[`0.1.0/2026-08-28-architecture-and-mvp-plan.md`](../0.1.0/2026-08-28-architecture-and-mvp-plan.md),
which documents the reference architecture and the nine-step MVP plan written before any
adapter existed. Same intent — a constraint stated once, in writing, rather than a habit
someone has to rediscover from the shape of the repository.

## Consequences

Closing a version is a manual procedure, and nothing generates it: the folder is created by
hand, every file is moved by hand, the version `README.md` is written, the entries are
synthesised into `CHANGELOG.md`, and two index tables are rewritten. `docs/changelog/README.md`
states the five steps; `docs/RELEASE_CHECKLIST.md` carries them as boxes so a release does not
discover them from scratch.
