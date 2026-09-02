# Watching an analysis run, reference by reference

- **Type** — `feat`
- **Scope** — `core`, `api`, `front`, `docs`
- **Landed** — 2026-09-02
- **Commits** — _pending_

## What shipped

An analysis is no longer a bar that fills. Launching one hands the Diagnostic tab to the run
itself, which shows what is being asked of the repository while it is being asked:

- a five-stage header — waiting, topology, contributors, saving, finished — where the current
  stage fills with the references it has read, beside the elapsed time in tenths;
- a ledger, one line per reference in read order, that fills in as the facts land: the
  commit the two histories share, ahead and behind, the topology badge, then the main author.
  The line being read is highlighted, and the ledger scrolls itself to keep it in view;
- a drawing of the topology already placed, each reference set further from the trunk the
  further ahead it has run, hollow when it adds nothing of its own;
- a `git · read only` console listing every command actually run, with its duration and the
  first line of Git's answer.

**Show the last capture** folds the whole thing into a one-line strip carrying the same
stage, counters and target, and hands the tab back to the previous capture; the run carries
on behind it and **Show the analysis** brings it back. A run that lands holds its last frame
for a beat — every stage green, "no Git write" — before the tab returns to the new capture.

Behind it, `GET /api/analyses/{id}` answers with a `progress` object next to the phase: the
ordered ledger of references with their state (`Listed`, `Measuring`, `Measured`,
`Enriching`, `Read`) and what is known of each, plus the last sixty Git commands, ranked, so
a reader appends rather than repeats. The scanner reports the events that feed it —
`ScanReferencesListed`, `ScanReferenceStarted`, `ScanReferenceMeasured`,
`ScanReferenceEnriched`, `ScanCommandCompleted` — replacing the stage-only progress channel.

## Why

The promise GitHealth makes is that it only ever reads. A progress bar asks to be believed;
a console naming every command, and a ledger naming every reference, can be checked. Making
the run legible is what turns that promise into something a reader verifies rather than
accepts — which is why the console shows the command as it would be typed, with the paths
and hardening flags GitHealth injects left out as noise, but nothing else hidden.

The events carry facts the scan already had rather than facts gathered for the display. The
shared commit is the clearest case: `merge-base` is run only for two histories that both
moved, and in every other case the shared commit is one of the two tips. The ledger shows
the merge base for every reference and the scan runs exactly the commands it ran before —
the value was simply being discarded. `BranchClassifier.ClassifyTopology` became public over
a `BranchDivergence` for the same reason: naming the topology of a reference the moment it is
measured must use the classifier, not a second copy of the rule in the front end.

Tracing is a decorator around the process runner rather than a parameter threaded through
every reader, so an unfollowed scan pays nothing and no call site had to grow an argument.
The live state is bounded on purpose: the ledger is one line per reference, and only the last
sixty commands are kept — a repository of a few hundred branches runs thousands, and a status
answer polled every second cannot carry them all. The front end keeps a longer tail of what
it has already been sent, and the drawing is a sliding window ending on the reference being
read, so it stays legible where the ledger stays exhaustive.

A run ends between two polls, which is why its reading now outlives it: the last eight
endings are kept and pushed out by the runs that follow. Dropping the reading the moment the
run ended left the closing frame claiming every branch had been read above a ledger frozen
wherever the last poll had caught it — a header and a table disagreeing about the same run.

## Consequences

`IRepositoryScanner.ScanAsync` now takes an `IProgress<RepositoryScanEvent>` where it took an
`IProgress<RepositoryScanStage>`. The stage-only overload is gone; the default implementation
still falls back to the unfollowed scan, so an implementation that ignores progress is
unaffected.

A followed scan reports one event per Git command, which means the timing decorator wraps
every process it runs. The cost is a stopwatch and a string per command, paid only while
somebody is watching: an analysis launched with no reader runs through the bare runner.
