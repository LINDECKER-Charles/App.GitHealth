# Scanning a folder and running analyses in parallel

- **Type** — `feat`, `docs`
- **Scope** — `api`, `front`, `docs`
- **Landed** — 2026-08-30
- **Commits** — `23e985e`, `7be54d5`, `a84ad7c`, `056b621`

## What shipped

GitHealth detects the Git repositories contained in a folder, up to a chosen depth, and
flags the ones already tracked (`Discovery/`, `RepositoryDiscoveryEndpointTests`). The scan
dialog lets the retained selection be analysed in one go: unknown repositories are
registered along the way, each starting its analysis as soon as it is registered rather
than waiting for the whole batch.

Analyses run in parallel behind the queue: `AnalysisQueue:MaximumParallelAnalyses` sets the
number of readers, four by default, and `1` restores the strictly sequential behaviour. A
repository rejected by a full queue is retried as soon as a slot frees up, so a large
selection never loses a repository silently.

## Why

Adding repositories one path at a time is the step where the product stops being worth the
effort on a machine that holds thirty of them. Registering and analysing repository by
repository, rather than in two phases, means the first results appear while the rest of the
folder is still being read.

The parallelism is bounded and configurable because the cost is not ours to choose: each
analysis spawns Git processes, and the right number depends on the machine and on the size
of the repositories.
