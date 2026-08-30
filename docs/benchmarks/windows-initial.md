# Initial Windows baseline

## Context

This baseline was measured locally on 29 August 2026 with the step 9 runner. The raw
samples are kept in `windows-initial.json`.

| Property | Value |
|---|---|
| OS | Microsoft Windows 10.0.26200 |
| Runtime | .NET 10.0.11, `win-x64`, X64 process |
| Processor | Intel64 Family 6 Model 186 Stepping 3, GenuineIntel |
| Visible logical processors | 12 |
| Git | 2.55.0.windows.5 |
| Measured commit | `177eede7d88311c7fee8ac1df1203c76987d499b` |
| Worktree state | clean (`sourceWorkingTreeDirty: false`) |
| Warm-up | 1 iteration per phase |
| Measurement | 3 iterations retained per phase |

The runner measured a clean worktree at the commit above. The host's Git configuration was
neutralised to make the fixture deterministic. No other intensive program was started by
the runner and the system caches were not flushed.

## Command

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks/App.GitHealth.Benchmarks.csproj `
  --configuration Release -- `
  --sizes 100,500,1000 --warmup 1 --iterations 3 `
  --enforce-budgets `
  --output docs/benchmarks/windows-initial.json
```

## Results

Durations are in milliseconds. With three measurements, the P95 is the maximum observed.
The budgets were loaded and enforced during this run; no regression was detected.

| Branches | Phase | Median | P95 | P95 budget |
|---:|---|---:|---:|---:|
| 100 | topology | 123.976 | 124.632 | 300 |
| 100 | enrichment | 7,531.697 | 7,629.140 | 15,000 |
| 100 | persistence | 80.450 | 103.648 | 200 |
| 100 | API | 6.781 | 9.622 | 50 |
| 500 | topology | 234.446 | 262.796 | 550 |
| 500 | enrichment | 37,836.940 | 38,569.893 | 75,000 |
| 500 | persistence | 208.476 | 324.947 | 750 |
| 500 | API | 17.019 | 18.490 | 100 |
| 1,000 | topology | 539.290 | 562.748 | 850 |
| 1,000 | enrichment | 76,420.966 | 79,111.705 | 150,000 |
| 1,000 | persistence | 126.663 | 143.766 | 750 |
| 1,000 | API | 38.164 | 39.476 | 125 |

The fixture fingerprints are:

| Branches | SHA-256 of the references |
|---:|---|
| 100 | `5449a8ee4b1fd3ef6513f7e0c5d4365f9d97365a5cd2e078adf8b75026a1536c` |
| 500 | `22abc881b613ecdc007ab7b3544135497751827114b54a430441ebb3a72c9729` |
| 1,000 | `55078751d0dc13f7e6112378ba72cf30f8e2206a5276a2a209a2e67a798c96ff` |

## Interpretation

Enrichment dominates: it accounts for more than 98 % of the time at 1,000 branches and
scales almost linearly, at around 76 ms per branch in this environment. That matches the
current behaviour, one separate `git log` process per branch tip commit. A future
optimisation will have to preserve mailmap accuracy and the output bound before reducing
that process count.

Topology stays below 563 ms at 1,000 branches, thanks to the `for-each-ref ahead-behind`
fast path. Persistence shows more variance at 500 branches, hence the monotonic, rounded
budgets. The API phase, which reloads the whole analysis then serialises a page of 200
entries, stays below 40 ms in this series.

The budgets add at least 50 % to the observed P95. They are not a universal product
target: they act as a guardrail on a comparable Windows environment. The
`docs/BENCHMARKING.md` guide details the protocol and its limitations.
