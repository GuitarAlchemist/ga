# GaDuckLens

A proof that **GA core-domain entities can be queried as live SQL** by embedding
DuckDB in-process (DuckDB.NET) and registering the domain model as table/scalar
functions. The C# mirror of ix's `ix-duck` crate.

## Why in-process (and not a C# `.duckdb_extension`)

DuckDB loads native extensions over a C ABI — DuckDB is the host process. A
C#-authored loadable extension would need Native AOT + hand-written C glue and
exact-version pinning. **Embedding DuckDB inside the CLR** (what this tool does)
needs none of that and is supported out of the box via `DuckDB.NET.Data.Full`.

Trade-off: table/scalar functions registered this way live **only on the
embedding process's connection** — the standalone `duckdb.exe` CLI cannot see
them. To reach the CLI, materialize a projection to disk with the **appender**
(`--appender`); the CLI then reads plain data, no extension required.

## Run

```bash
dotnet run --project Tools/GaDuckLens                 # demo queries
dotnet run --project Tools/GaDuckLens -- --sql "SELECT * FROM ga_scale_notes('Bb','major')"
dotnet run --project Tools/GaDuckLens -- --appender ga-domain.duckdb
duckdb ga-domain.duckdb "SELECT * FROM ga_key_notes LIMIT 5"   # standalone CLI reads it
```

## What it exposes

- **`ga_scale_notes(root VARCHAR, mode VARCHAR)`** — a **table function** backed
  by the `Key` domain type (`GA.Domain.Core`). Returns `(degree, note,
  pitch_class)` with the domain's correct enharmonic spelling (e.g. C# major
  yields `E#`/`B#`). `mode ∈ {major, minor}` in this POC.
- **`--appender`** — bulk-loads the full `Key.Items` corpus (30 keys × 7 notes)
  into a real `ga_key_notes` table via the DuckDB appender, so the standalone CLI
  and the `state/quality/analytics` lens can read GA domain data directly.

## Architecture

Layer-5 tooling that projects layer-1 (`GA.Domain.Core`) entities into DuckDB. It
calls *down* into the domain only — no layer violation, same shape as
`Tools/QualityLens` and ix's `ix-duck`.

## Extending

- **More modes** — back `ga_scale_notes` with `GA.Domain.Core` `ScaleMode`
  (rooted rotation of the parent scale) to add Dorian/Lydian/etc.
- **Scalar UDFs** — `RegisterScalarFunction` for mid-query domain math, e.g.
  `ga_interval_semitones('M3')`, `ga_transpose('Cmaj7', 2)`.
- **Voicing corpus** — appender-snapshot the OPTIC-K voicing corpus into a table
  for join-friendly analytics alongside `quality.duckdb`.
