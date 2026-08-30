# GitHealth benchmarks

## Purpose

The runner measures the cost of the analysis journey on a synthetic, deterministic Git
fixture. It covers 100, 500 and 1,000 branches without depending on a private repository.
The results are meant to detect a regression, not to compare two machines.

The project is deliberately a plain .NET executable, with no micro-benchmark framework.
The phases spawn Git and SQLite: their duration is large enough to be measured with a
`Stopwatch`, while a simple runner keeps the same application code paths.

## Deterministic fixture

Each scenario creates, outside the measurement, a temporary repository with
`git fast-import`:

- a local `main` branch with a baseline commit;
- N references `refs/remotes/origin/benchmark/NNNN`;
- a single commit, directly ahead of `main`, for each reference;
- fixed identities, dates, messages and contents;
- a fixed local `.mailmap`.

The report contains the SHA-256 of the sorted `reference:commit` list. Two runs with the
same generator version must obtain the same fingerprint for a given size. The temporary
path and the creation of the fixture are excluded. The runner strips the host's `GIT_*`
redirections, traces and global configuration before every command, so that the fixture
stays inside its temporary directory.

## Measured phases

- `topology`: from reading the topology of the already captured references to the
  produced divergences. Locating the repository and the initial capture are excluded.
- `enrichment`: from a fresh contributor reader, with no cache, to the built
  `RepositoryScan`. The topology result is provided as input.
- `persistence`: from creating an analysis to the committed SQLite completion
  transaction. Creating the schema and the project is excluded.
- `api`: from reading the persisted snapshot to the paginated DTO serialised as JSON.
  Kestrel, the network and the browser are excluded.

Enrichment starts a fresh reader on every iteration. The fixture has a different commit
per branch: the measurement therefore runs a real `git log` for each branch and does not
turn 1,000 branches into a single cached access.

Persistence uses a fresh database for every sample. It includes both writes of the real
flow (`StartAsync`, then `CompleteAsync`) with the branches and the contributors.
`EnsureCreated` and the project insertion happen before the stopwatch starts.

The API phase reloads the whole analysis from SQLite, classifies the branches, sorts them
and serialises the first page of 200 entries with the web JSON options. It measures the
server-side cost of rendering, but not Angular, the browser, HTTP or Kestrel.

## Running it

From the repository root:

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks `
  --configuration Release -- `
  --sizes 100,500,1000 `
  --warmup 1 `
  --iterations 3 `
  --output artifacts/benchmarks/latest.json
```

Quick smoke run:

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks `
  --configuration Release -- `
  --sizes 100 --warmup 0 --iterations 1
```

The runner forces a full GC right before every retained sample. The file system and Git
caches are not flushed: the warm-up and the measurements represent repeated use on a
machine that is already running.

To obtain comparable values:

1. use the `Release` build and the SDK pinned by `global.json`;
2. close heavy tasks and disable sleep during the run;
3. keep the same warm-up and iteration counts;
4. compare the runtime, Git, OS, architecture and processor from the report;
5. check the fingerprints before comparing durations.

## Budgets

The absolute budgets live in `benchmarks/budgets.json`. They apply to the P95 of each
size/phase pair and were set after the first Windows baseline. The runner displays them
when the file exists.

The following option returns exit code `2` when a P95 exceeds its budget:

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks `
  --configuration Release -- --enforce-budgets
```

Enforcing absolute budgets is only meaningful on a reference agent with stable
characteristics. On another machine, keep the result as informative data and first compare
against a baseline specific to that agent.

The `benchmark.yml` workflow therefore keeps the measurement from GitHub-hosted runners as
informative data by default. Its manual run offers the `enforce_budgets` option only when
a runner comparable to the baseline is selected. That separation prevents a capacity
variation on a shared runner from blocking a merge.

To revise a budget:

1. measure before changing the budget;
2. explain the regression or the improvement in the associated report;
3. keep an explicit margin above the observed P95;
4. change the JSON in the same commit as the new validated report.

## Published baseline

The initial local baseline and its interpretation are available in
`docs/benchmarks/windows-initial.md`. Its raw JSON is kept next to the report. It contains
the samples, the exact environment and the state of the worktree.

## Limitations

- The fixture represents short branches, all one commit ahead. It does not cover a deep
  graph, submodules, Git LFS or partial clones.
- Git processes dominate enrichment on Windows. An antivirus or a power-saving mode can
  change the results substantially.
- The P95 of three samples is the maximum observed. A diagnostic campaign should raise
  `--iterations`.
- Browser-side rendering must be profiled separately with the browser tools.
- The baseline does not replace acceptance testing on real, large repositories.

No remote repository is ever contacted and no real identity is used. The runner sends no
data off the machine.
