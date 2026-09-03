# Changelog journal

The root [`CHANGELOG.md`](../../CHANGELOG.md) is the reader's changelog: a few lines per
capability, written for whoever installs GitHealth. This folder is the journal behind it —
one file per implementation, holding what was built, why it was built that way and what it
costs. It lets a reader go from "what changed" to "why it was decided" without replaying
the Git history commit by commit.

Nothing here is generated. An entry is written with the change it documents and travels in
the same commit.

## Layout

```
docs/changelog/
├── README.md          this page and the version index
├── TEMPLATE.md        the shape of an entry
├── unreleased/        merged, not attached to a version yet
└── 0.1.0/             everything that shipped in 0.1.0
```

One folder per version, named after the version alone — `0.1.0`, not `v0.1.0`; the `v`
belongs to the Git tag. Everything merged but not yet released goes to `unreleased/`, and
moves as-is into the version folder on the day that version is published.

## An entry

**One file per implementation, not one per commit.** A feature whose API half and
interface half landed separately is a single entry naming both commits; a translation pass
spread over nine commits is a single entry too. Conversely, one commit carrying two
distinct capabilities is split into two entries, each naming that same commit.

**File name: `YYYY-MM-DD-short-slug.md`**, dated with the day the entry's last commit
landed. The date prefix sorts a folder chronologically, and it survives the move from
`unreleased/` to a version folder — a sequence number would have to be rewritten.

**Shape:** see [`TEMPLATE.md`](TEMPLATE.md). The header states type, scope, date and the
short SHAs the entry covers, so every claim can be traced back to a diff. `Consequences`
is written only when there is one: a migration, a breaking change, a cost accepted on
purpose.

Each folder carries a `README.md` indexing its entries, so a version reads top-down
without opening every file.

## Releasing a version

1. create `docs/changelog/<version>/`;
2. move every file out of `unreleased/`, keeping the names — the dates stay those of the
   commits, not the release day;
3. write the version `README.md`: release date, tag, and the table of entries;
4. synthesise those entries into the `[<version>]` section of the root `CHANGELOG.md`;
5. add the version to the index below.

## Versions

| Version | Released | Entries | Contents |
|---|---|---|---|
| [unreleased](unreleased/README.md) | — | 11 | Baselines, capture selector, visualisation, English interface, deletion, live analysis run, agent assistant |
| [0.1.0](0.1.0/README.md) | 2026-08-30 | 27 | First public release: the whole MVP, from the domain to the installers |
