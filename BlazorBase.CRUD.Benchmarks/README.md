# BlazorBase.CRUD.Benchmarks

BenchmarkDotNet-based overhead suite that compares **BlazorBase.CRUD** against
hand-written EF Core / raw Minimal-API code on identical workloads.

The project covers two paths:

| Path | What is measured |
|---|---|
| **Server / in-process** | `DbContextBaseDataProvider<T>` vs. raw `DbContext` |
| **HTTP / end-to-end** | `HttpBaseDataProvider<T>` vs. handwritten `HttpClient` against raw Minimal-API endpoints (both hosted in-process via Kestrel on localhost) |

A throw-away SQLite database file is created and seeded per run, so no
migrations are required.

## Project Structure

```
Libs/BlazorBase.CRUD.Benchmarks/
├─ BlazorBase.CRUD.Benchmarks.csproj   Console-App (net10.0, Web SDK for Kestrel)
├─ Program.cs                          BenchmarkSwitcher entry point
├─ Entities/                           Test entities ([BaseCrud] decorated)
├─ Infrastructure/
│  ├─ BenchmarkDbContext.cs            Dedicated DbContext (NOT OmniLog.Web's)
│  ├─ DatabaseFixture.cs               SQLite EnsureCreated + deterministic seeding
│  ├─ ServerServiceProviderFactory.cs  DI container with BlazorBase.CRUD wiring
│  └─ BenchmarkWebHost.cs              In-process Kestrel host with BB.CRUD + raw endpoints
├─ Scenarios/
│  ├─ ServerBenchmarkBase.cs           Shared GlobalSetup / IterationSetup for server path
│  ├─ ReadByIdBenchmarks.cs            Get-by-id, with and without projection
│  ├─ ListQueryBenchmarks.cs           Unfiltered / simple Where / complex AND-OR
│  ├─ ProjectionBenchmarks.cs          Dynamic Select (2 fields, 5 fields)
│  ├─ NavigationIncludeBenchmarks.cs   Filtered Include + simple reference Include
│  ├─ WriteBenchmarks.cs               Create / Patch 1 field / Patch 5 fields
│  ├─ HttpBenchmarkBase.cs             Shared setup for HTTP path
│  └─ Http/
│     ├─ HttpReadBenchmarks.cs
│     └─ HttpWriteBenchmarks.cs
├─ tools/
│  └─ Compare-BenchmarkResults.ps1     Baseline-vs-current diff for CI
├─ azure-pipelines.yml                 Azure DevOps pipeline definition
└─ README.md
```

## Running locally

Benchmarks must run in **Release** configuration. BenchmarkDotNet refuses to
run a Debug build.

```pwsh
# Run everything
dotnet run -c Release --project Libs/BlazorBase.CRUD.Benchmarks

# Filter by category or class
dotnet run -c Release --project Libs/BlazorBase.CRUD.Benchmarks -- --filter "*Read*"
dotnet run -c Release --project Libs/BlazorBase.CRUD.Benchmarks -- --filter "*Http*"
dotnet run -c Release --project Libs/BlazorBase.CRUD.Benchmarks -- --filter "*WriteBenchmarks*"

# List available benchmarks without running them
dotnet run -c Release --project Libs/BlazorBase.CRUD.Benchmarks -- --list flat
```

Reports are written to `BenchmarkDotNet.Artifacts/` next to the project (Markdown,
JSON, CSV). The Markdown report is the easiest to skim.

### From Visual Studio

1. Set `BlazorBase.CRUD.Benchmarks` as startup project.
2. Switch the configuration drop-down to **Release**.
3. Add a launch profile in `launchSettings.json` per scenario you run often,
   e.g.:

   ```json
   {
     "profiles": {
       "Server-Read":  { "commandLineArgs": "--filter \"*ReadById*\"" },
       "Server-Write": { "commandLineArgs": "--filter \"*WriteBenchmarks*\"" },
       "Http":         { "commandLineArgs": "--filter \"*Http*\"" }
     }
   }
   ```

4. Run with **Ctrl+F5** (run without debugger — debugger massively skews timing).

The benchmarks intentionally are *not* exposed via xUnit/MSTest. Wrapping
BenchmarkDotNet runs in tests is technically possible but each run takes minutes
and would explode "Run All Tests" times — keep them as a deliberate, manual
invocation.

## How a benchmark is structured

Each scenario class derives from `ServerBenchmarkBase` (or `HttpBenchmarkBase`).
`[Benchmark(Baseline = true)]` marks the raw EF / raw HTTP version so
BenchmarkDotNet emits the ratio column automatically.

```csharp
[Benchmark(Baseline = true, Description = "Raw EF: FindAsync")]
public Task<BenchProduct?> Raw_FindAsync()
    => CurrentContext.Products.FindAsync(TargetId).AsTask();

[Benchmark(Description = "BB.CRUD: GetByIdAsync")]
public Task<BenchProduct?> BlazorBase_GetByIdAsync()
    => ProductProvider.GetByIdAsync(TargetId);
```

`[Params(100, 10_000)]` controls the seeded row count — overhead behaves
differently on small vs. larger tables, especially for projection/filter paths.

`WarmUp()` in `GlobalSetup` runs each provider method once before the timed
iterations start, so the projection cache (`ConcurrentDictionary<string, LambdaExpression>`
in `DbContextBaseDataProvider`) and the `FilterExpressionDecomposer` cache are
populated before the first measurement.

## Adding a new scenario

1. Add a class under `Scenarios/` deriving from `ServerBenchmarkBase` or
   `HttpBenchmarkBase`.
2. Add `[BenchmarkCategory("YourCategory")]` so it can be filtered.
3. Add the **baseline** benchmark with `[Benchmark(Baseline = true)]` and the
   BlazorBase.CRUD equivalent. Keep both sides functionally identical — same
   filter, same select, same Take.
4. If you touch new providers / new methods in setup, extend `WarmUp()` so the
   first measured iteration is not paying one-time cache-population costs.

## Azure DevOps pipeline

`azure-pipelines.yml` defines a manually-triggered and nightly-scheduled
benchmark run with optional baseline comparison.

### Variables

| Variable | Default | Purpose |
|---|---|---|
| `benchmarkFilter` | `*` | BenchmarkDotNet `--filter` argument |
| `regressionThreshold` | `10` | Allowed slowdown in % before the build fails |
| `baselineBuildId` | empty | Build ID whose `benchmark-results` artifact is the baseline. Empty = skip comparison. |
| `benchmarksAgentPool` | (none) | Required: name of the Azure DevOps pool to run on |

### Stages

1. **Benchmark** — restore, build (Release), run BenchmarkDotNet, publish
   `BenchmarkDotNet.Artifacts/` as the `benchmark-results` artifact.
2. **Compare** — runs only when `baselineBuildId` is set. Downloads the
   baseline artifact, runs `tools/Compare-BenchmarkResults.ps1`, publishes a
   Markdown + CSV diff as `benchmark-comparison`. The build fails when any
   benchmark exceeds the configured regression threshold.

### Typical workflow

1. **First run on `main`**: trigger with no `baselineBuildId` to produce the
   first artifact. Note the build ID.
2. **Subsequent runs**: pass the previous successful run's build ID as
   `baselineBuildId`. The Compare stage flags regressions.
3. **PR validation** (optional): enable the `pr` trigger in the YAML and use
   the latest `main` build as the baseline for PR comparison.

### ⚠️ Agent variance — important

Microsoft-hosted Azure DevOps agents share hardware with other jobs and
typically show **20-50 % variance** between identical runs. Commit-to-commit
regression detection below ~10 % is **not reliable** on hosted agents — it will
flag noise.

For meaningful trend tracking:

- Run the pipeline on a **self-hosted agent** on a dedicated machine (even an
  old NUC works) that runs nothing else.
- Variance drops to ~2-3 %, real regressions become visible, the configured
  10 % threshold becomes meaningful.

The pipeline targets `$(benchmarksAgentPool)` for exactly this reason — set it
to your self-hosted pool, not `ubuntu-latest` / `windows-latest`.

## Why a separate DbContext from OmniLog

The benchmark uses its own `BenchmarkDbContext` with `BenchProduct`,
`BenchCategory`, `BenchOrder`, `BenchOrderItem` instead of `AppDbContext`:

- **Isolation**: we measure framework overhead, not OmniLog's specific schema.
- **Speed**: `EnsureCreated()` over 4 simple tables is millisecond-fast and
  introduces no setup variance.
- **Decoupling**: benchmark stability is not affected when OmniLog adds /
  renames / migrates entities.

## Caveats

- BenchmarkDotNet rebuilds the project for each run into a separate folder.
  Make sure no other tools are watching `bin/` while benchmarks run.
- SQLite is intentional — it isolates EF-translation and provider-overhead from
  network/server variability. To benchmark against the production database
  engine, add a new `*BenchmarkBase` class with `UseSqlServer(...)` and run it
  on a quiet machine.
- `[Params]` row counts double the run time. Reduce to a single value when
  iterating locally on a new scenario.
