# Persistence is two planes: MongoDB operational, DuckDB/Parquet analytical

**Status:** Accepted 2026-07-24 (owner). Decision issue: #594; epic: #590.
**Evidence:** spikes #591 (MongoDB inventory), #592 (Gel prototype, executed live),
#593 (DuckDB/Parquet on real datasets), #597 (MotherDuck desk eval + local compat).
Every claim below traces to a comment on one of those issues.

GA runs on a single live datastore (MongoDB, ~24 projects) whose coupling is
high and broad but shallow in kind: no transactions, no change streams, vector
search already strategy-pluggable — yet no repository seam, with
`IMongoCollection<T>`/`BsonDocument` leaking above the service boundary, and
the canonical music graph held together by embedded copies
(`ChordDocument.RelatedScales`) and name-string pointers
(`ProgressionDocument.Chords`, `ScaleDocument.Modes`) that silently orphan on
rename. Meanwhile DuckDB was already adopted de facto — three in-process
DuckDB.NET tools (GaDuckLens, QualityLens, GaDomainInvariants), production
Parquet (OPTICK SAE), and the ix `ix-duck` crates — just never named as a
decision. This ADR names it.

## Decision

**Two planes, one boundary:**

- **MongoDB keeps the operational/document plane:** canonical music documents
  (until the graph engine question reopens — see rider 1), RAG/vector
  collections (search stays behind `IVectorSearchStrategy`), GridFS blobs
  (assets, scenes), document processing, auth, telemetry.
- **DuckDB + Parquet owns the analytical/generated plane:** voicing and
  fretboard enumeration (667k rows), OPTICK SAE outputs (297k×1025, ~0.1%
  dense), set-class analytics, date-partitioned embedding logs, ML feature
  matrices, and the `$group` analytics reads currently aggregating in Mongo.
  Measured on real repo data (#593): 23.5× storage (zstd Parquet), ~28× scan
  vs raw JSONL, ~150× projection pushdown on the wide SAE matrix.
- **`chords` splits along the #591 seam:** the identity facet (Name/Root/
  Quality + relations) is canonical-graph data; the 427k generated enumeration
  rows and embeddings belong to the analytical plane.

**Rider 1 — Gel is the reference model, not a runtime.** The #592 prototype
proved the model right on live queries: identity-keyed typed links survive a
scale rename that Mongo's embedded copies and name strings silently orphan.
But the only .NET driver is community EdgeDB.Net 1.4.0 (May 2024, EdgeDB-5-era,
net6) against a Gel-7 server and GA's net10 — a live protocol-skew risk — and
Windows server DX requires WSL/Docker. The SDL schema from #592 is therefore
adopted as the *normative target model* for the canonical graph (including the
junction-type decision: ordered/repeating sequences like a 12-bar blues need a
`ProgressionStep`, and `Tuning.open_notes` must not collapse duplicate
strings — required regardless of engine). Re-evaluate the Gel *engine* when an
official Gel.Net driver ships or the community driver targets Gel 7+/net10.

**Rider 2 — MotherDuck is the pre-qualified, gated growth path; Databricks is
ruled out as the default.** Because MotherDuck *is* DuckDB, migration later is
a connection-string + `ATTACH` change on the same ADO.NET provider (hours);
Databricks is a re-platform (new dialect, UDFs don't port, Parquet→Delta+Unity,
loses the in-process model). Adoption triggers, verbatim from #597: (1) ≥2
concurrent live readers or a web app needing live query on shared state;
(2) a second machine or CI live-querying >~5 GB of shared analytics state
weekly or more (today's largest candidate is 59.5 MB); (3) governed external
collaborators. Until a trigger fires, shared datasets publish as Parquet on
object storage — which serves the no-vendor baseline today (proven locally via
`httpfs`) and becomes MotherDuck's input unchanged if one does. Storage never
forks.

## Consequences / immediate actions

1. Pin `DuckDB.NET.Data.Full` **1.5.3** ecosystem-wide (tools currently pin
   1.3.0). One bump resolves #593's version-drift risk and satisfies
   MotherDuck's ≥1.4.0 floor (#597).
2. Standardize the already-shipping patterns: persisted `.duckdb` built by
   `build-views.sql`, readers open `ACCESS_MODE=READ_ONLY`, zstd Parquet at
   rest, single-writer regenerate-and-swap only (DuckDB is single-writer).
3. Analytical datasets are **derived, regenerable, never source of truth** —
   they are rebuilt from generators, not migrated or backed up as canonical
   state.
4. Before any canonical-graph store change (including eventual Gel): introduce
   a repository seam. #591 ranks the migration surface; the `BsonDocument`-in-
   controller sites and the missing `IRepository<T>` boundary are the
   prerequisite work, not the engine swap itself.
5. The four dead microservice Mongo copies (Knowledge/Fretboard/Analytics/BSP,
   unwired in `AllProjects.AppHost`) are deletion candidates, not migration
   targets.

## Standing constraints (LOLLI)

Do not architect around Markov/Monte-Carlo outputs or experiment/benchmark
datasets — zero real data exists on disk (#593). Web-facing concurrent
analytics does not exist — it is a MotherDuck trigger, not a requirement.
Rejected options for the record: Mongo-as-is (keeps both proven weaknesses),
Gel-in-production-now (driver blocker), retiring Mongo (GridFS/RAG/auth have
no better home), Databricks-by-default (re-platform without a driving
workload).
