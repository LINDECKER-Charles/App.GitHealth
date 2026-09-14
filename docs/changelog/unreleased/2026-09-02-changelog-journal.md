# A journal behind the changelog, version by version

- **Type** — `docs`
- **Scope** — `docs`
- **Landed** — 2026-09-02
- **Commits** — `419e02c`

## What shipped

`docs/changelog/`: one folder per version, one file per implementation. Forty-one files
changed, just under 1,200 lines added, thirty-eight of them new — the whole journal
rebuilt in one pass by reading the history back. Twenty-seven entries for `0.1.0`, seven
for what was merged but not yet released on that day. Each entry states what was built,
why it was built that way and what it costs, and carries the short SHAs in its header so
every claim traces back to a diff.

`TEMPLATE.md` fixes the shape: an H1 naming the capability, the four header bullets Type,
Scope, Landed and Commits, then `What shipped`, `Why`, and `Consequences` only when there
is one. Each folder carries a `README.md` indexing its entries as a table — date landed,
link, type, one line of contents — so a version reads top-down without opening every file.
`0.1.0/README.md` adds the release date, the tag, the commit range (`1609b68` … `1893972`,
74 commits without the merge) and the limitations known at release; `unreleased/README.md`
adds a "Watch out when releasing" list, already flagging a removed route and a
required-status-check rename. `docs/changelog/README.md` holds the conventions, the
five-step release procedure and the version index.

Three existing files were touched. `AGENTS.md` already required a changelog entry to be
attached to the feature or fix commit it documents; the rule now names both destinations —
the reader's line in `CHANGELOG.md` and the detailed entry in `docs/changelog/unreleased/`.
`CHANGELOG.md` and `docs/README.md` gained a pointer to the journal.

## Why

The root `CHANGELOG.md` is written for whoever installs GitHealth: a few lines per
capability, and no room for why a shape was chosen or what it cost. That reasoning
otherwise survives only in commit messages, recoverable by replaying the history commit by
commit, which nobody does. The journal is where a decision keeps existing.

Two rules carry the real thinking.

**An entry covers an implementation, not a commit.** A feature whose API half and interface
half landed separately is one file naming both SHAs; a translation pass spread over nine
commits is one file too. Conversely, one commit carrying two distinct capabilities is split
into two entries, each naming that same commit. Indexing on commits would produce a folder
shaped exactly like the Git history — which already exists, and which `git log` already
reads. Indexing on implementations produces the thing the history cannot give: one document
per decision.

**Files are named `YYYY-MM-DD-slug`.** Date-first, so a folder sorts chronologically with no
index to maintain. A sequence number would sort too, and it was rejected for one reason: it
would have to be rewritten the day the file moves from `unreleased/` into its version
folder. The date is a fact about the change and never changes, so the move is a `git mv` and
nothing more — no renaming, no link left pointing at a file that no longer exists.

A `docs` entry describing a working convention rather than a product capability has a
precedent here: `0.1.0/2026-08-28-architecture-and-mvp-plan.md`, which documents the
reference architecture and the nine-step MVP plan written before any adapter existed. Same
intent — a constraint stated once, in writing, rather than a habit someone has to
rediscover from the shape of the repository.

## Consequences

An entry travels in the commit it documents, so it cannot name its own SHA — the SHA does
not exist when the entry is written. Entries therefore carry `_pending_` in their Commits
header until the release fills it in from the history; four of them did as this version
closed. That is a recurring manual step at every release, not a one-off, and
`docs/RELEASE_CHECKLIST.md` now carries it as a step of its own. Committing the
entry afterwards would remove it, and was rejected: an entry written after the fact is an
entry that gets forgotten.
