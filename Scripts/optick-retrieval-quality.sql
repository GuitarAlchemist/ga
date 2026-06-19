-- OPTIC-K retrieval-quality lens — IX DuckDB UDFs over the live voicing index.
--
-- Reads state/voicings/optick.index directly via ix_optick_scan (no Parquet
-- export), samples up to __PER_INSTRUMENT__ voicings per instrument, and runs:
--   * ix_kdist     — mean distance to the k nearest neighbours (local-density /
--                    outlier signal): high kdist = an isolated voicing whose
--                    embedding has few near neighbours (rare voicing or a
--                    suspect embedding worth inspecting).
--   * ix_silhouette by instrument — how separable guitar/bass/ukulele are in
--                    embedding space. Per OPTIC-K v1.8 design the STRUCTURE
--                    partition is instrument-INVARIANT (same PC-set across
--                    instruments should be CLOSE — invariant test #25 / the ROOT
--                    partition), so LOW/near-zero silhouette here is the GOOD,
--                    expected outcome; a high positive value would mean
--                    instrument identity leaked into the embedding.
--
-- Placeholders __EXT__ / __INDEX__ / __OUTDIR__ / __PER_INSTRUMENT__ are filled
-- by Scripts/optick-retrieval-quality.ps1. Run via that wrapper.

LOAD '__EXT__';

-- Deterministic, instrument-stratified sample so bass/ukulele (tiny vs guitar)
-- are represented. Contiguous 0-based eid aligns ix_kdist/ix_silhouette `row`.
CREATE OR REPLACE TABLE sample AS
SELECT (row_number() OVER (ORDER BY instrument, voicing)) - 1 AS eid,
       voicing, instrument,
       CAST(embedding AS DOUBLE[]) AS vec
FROM (
    SELECT voicing, instrument, embedding,
           row_number() OVER (PARTITION BY instrument ORDER BY voicing) AS rn
    FROM ix_optick_scan('__INDEX__')
) WHERE rn <= __PER_INSTRUMENT__;

CREATE OR REPLACE TABLE inst_dim AS
SELECT instrument, (dense_rank() OVER (ORDER BY instrument)) - 1 AS label
FROM (SELECT DISTINCT instrument FROM sample);

-- DuckDB forbids subqueries as table-function args → stage JSON in variables.
SET VARIABLE vecs = (SELECT to_json(list(vec ORDER BY eid)) FROM sample);
SET VARIABLE labs = (SELECT to_json(list(d.label ORDER BY s.eid))
                     FROM sample s JOIN inst_dim d USING (instrument));

CREATE OR REPLACE TABLE kd  AS SELECT * FROM ix_kdist(getvariable('vecs'), 5);
CREATE OR REPLACE TABLE sil AS SELECT * FROM ix_silhouette(getvariable('vecs'), getvariable('labs'));

-- ── Structured outputs ───────────────────────────────────────────────────────
COPY (SELECT s.voicing, s.instrument, round(k.kdist, 5) AS kdist
      FROM kd k JOIN sample s ON s.eid = k.row ORDER BY k.kdist DESC LIMIT 200)
     TO '__OUTDIR__/optick-isolated-voicings.json' (FORMAT JSON, ARRAY true);
COPY (SELECT s.instrument,
             round(avg(k.kdist), 5)        AS mean_kdist,
             round(avg(sl.silhouette), 5)  AS mean_silhouette
      FROM sample s
      JOIN kd  k  ON k.row  = s.eid
      JOIN sil sl ON sl.row = s.eid
      GROUP BY s.instrument)
     TO '__OUTDIR__/optick-per-instrument.json' (FORMAT JSON, ARRAY true);

-- ── Human-readable summary (captured by the wrapper) ─────────────────────────
.mode markdown

SELECT '## Sample' AS section;
SELECT count(*) AS sampled, count(DISTINCT instrument) AS instruments, any_value(len(vec)) AS dim FROM sample;

SELECT '## Local density (ix_kdist, k=5) — distribution (higher = more isolated)' AS section;
SELECT round(min(kdist), 4)                  AS min,
       round(quantile_cont(kdist, 0.5), 4)   AS median,
       round(quantile_cont(kdist, 0.95), 4)  AS p95,
       round(max(kdist), 4)                  AS max,
       round(avg(kdist), 4)                  AS mean
FROM kd;

SELECT '## Per-instrument local density (mean kdist)' AS section;
SELECT s.instrument, count(*) AS n,
       round(avg(k.kdist), 4) AS mean_kdist,
       round(max(k.kdist), 4) AS max_kdist
FROM kd k JOIN sample s ON s.eid = k.row
GROUP BY s.instrument ORDER BY mean_kdist DESC;

SELECT '## Most isolated voicings (highest kdist — inspect as outliers)' AS section;
SELECT s.voicing, s.instrument, round(k.kdist, 4) AS kdist
FROM kd k JOIN sample s ON s.eid = k.row
ORDER BY k.kdist DESC LIMIT 15;

SELECT '## Instrument separability (ix_silhouette) — low/neg = instrument-invariant (GOOD per OPTIC-K v1.8)' AS section;
SELECT round(avg(silhouette), 4) AS overall_silhouette FROM sil;
SELECT s.instrument, round(avg(sl.silhouette), 4) AS mean_silhouette, count(*) AS n
FROM sil sl JOIN sample s ON s.eid = sl.row
GROUP BY s.instrument ORDER BY mean_silhouette DESC;
