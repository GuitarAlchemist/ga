-- OPTIC-K SAE feature-coverage lens — DuckDB (+ IX UDFs) over an ix-optick-sae
-- training artifact (cross-repo: ix produces it per docs/contracts/
-- 2026-05-02-optick-sae-artifact.contract.md).
--
-- Three inputs from one dated artifact dir under state/quality/optick-sae/<date>/:
--   * optick-sae-artifact.json  — the trainer's DECLARED metrics (ground truth to
--     reconcile against): dict_size, k_sparse, dead_features_pct, alive count, R^2.
--   * feature_manifest.jsonl    — per-feature scalars (activation_count, is_alive,
--     decoder_norm) — the independent recomputation surface.
--   * feature_activations.parquet — dense N_voicings x dict_size matrix (f0..fK-1),
--     one row per voicing in optick.index scan order (no id column → row ordinal IS
--     the voicing id). The vector surface ix_kdist runs over.
--
-- What it answers: is the dictionary being USED (dead-feature %, alive coverage,
-- activation-frequency spread), does the manifest agree with the artifact's
-- declared metrics (drift/mislabel guard), is the top-k sparsity actually k, and
-- which voicings sit isolated in SAE feature space (rare structure / suspect rows).
--
-- Placeholders __EXT__ / __ARTIFACT__ / __MANIFEST__ / __ACTIVATIONS__ / __OUTDIR__
-- / __SAMPLE__ are substituted by Scripts/sae-feature-coverage.ps1. Run via that
-- wrapper, not directly.

LOAD '__EXT__';

-- ── Declared metrics (trainer ground truth) ──────────────────────────────────
CREATE OR REPLACE TABLE art AS SELECT * FROM read_json('__ARTIFACT__');

-- ── Per-feature manifest (independent recomputation) ─────────────────────────
CREATE OR REPLACE TABLE manifest AS
SELECT feature_idx, activation_count, is_alive, decoder_norm
FROM read_json_auto('__MANIFEST__');

CREATE OR REPLACE TABLE coverage AS
SELECT count(*)                                              AS dict_size,
       count(*) FILTER (WHERE is_alive)                      AS alive,
       count(*) FILTER (WHERE NOT is_alive)                  AS dead,
       round(100.0 * count(*) FILTER (WHERE NOT is_alive) / count(*), 2) AS dead_pct,
       round(avg(activation_count) FILTER (WHERE is_alive), 1)          AS mean_act_alive,
       round(quantile_cont(activation_count, 0.5) FILTER (WHERE is_alive), 0) AS median_act_alive,
       round(quantile_cont(activation_count, 0.95) FILTER (WHERE is_alive), 0) AS p95_act_alive,
       max(activation_count)                                 AS max_act
FROM manifest;

-- Reconcile recomputed dead_pct against the artifact's declared metrics.dead_features_pct.
CREATE OR REPLACE TABLE reconcile AS
SELECT a.metrics.dead_features_pct                AS declared_dead_pct,
       c.dead_pct                                 AS recomputed_dead_pct,
       round(abs(a.metrics.dead_features_pct - c.dead_pct), 3) AS abs_delta,
       a.features_summary.alive                   AS declared_alive,
       c.alive                                    AS recomputed_alive,
       a.model.dict_size                          AS declared_dict_size,
       c.dict_size                                AS recomputed_dict_size,
       (abs(a.metrics.dead_features_pct - c.dead_pct) <= 0.5
        AND a.features_summary.alive = c.alive
        AND a.model.dict_size = c.dict_size)      AS consistent
FROM art a CROSS JOIN coverage c;

-- ── Sampled per-voicing activation vectors (the IX surface) ──────────────────
-- Deterministic first-N rows (file order) so the lens is reproducible. The dense
-- f0..fK-1 columns are folded to long form, then re-assembled as a dict_size-wide
-- vector ORDERED BY feature index (UNPIVOT preserves no column order, and a stable
-- dimension order is required for ix_kdist to compare vectors meaningfully).
CREATE OR REPLACE TABLE act_rows AS
SELECT * FROM (
    SELECT (row_number() OVER ()) - 1 AS rid, *
    FROM read_parquet('__ACTIVATIONS__')
) WHERE rid < __SAMPLE__;

CREATE OR REPLACE TABLE act_long AS
SELECT rid, CAST(substr(k, 2) AS INTEGER) AS fidx, v
FROM (UNPIVOT act_rows ON COLUMNS('f[0-9]+') INTO NAME k VALUE v);

CREATE OR REPLACE TABLE act_sample AS
SELECT rid, list(v ORDER BY fidx)::DOUBLE[] AS vec
FROM act_long GROUP BY rid;

-- Per-voicing active-feature count: a top-k SAE should fire exactly k_sparse.
CREATE OR REPLACE TABLE sparsity AS
SELECT rid, count(*) FILTER (WHERE abs(v) > 1e-6) AS active_features
FROM act_long GROUP BY rid;

-- ix_kdist (k=5) over the feature vectors → local density / isolation in SAE space.
SET VARIABLE sae_vecs = (SELECT to_json(list(vec ORDER BY rid)) FROM act_sample);
CREATE OR REPLACE TABLE kd AS SELECT * FROM ix_kdist(getvariable('sae_vecs'), 5);

-- ── Structured outputs ───────────────────────────────────────────────────────
COPY (SELECT feature_idx, activation_count, decoder_norm
      FROM manifest WHERE is_alive ORDER BY activation_count DESC LIMIT 50)
     TO '__OUTDIR__/sae-top-features.json' (FORMAT JSON, ARRAY true);
COPY (SELECT feature_idx, decoder_norm
      FROM manifest WHERE NOT is_alive ORDER BY decoder_norm DESC)
     TO '__OUTDIR__/sae-dead-features.json' (FORMAT JSON, ARRAY true);
COPY (SELECT s.rid AS voicing_ordinal, s.active_features, round(k.kdist, 5) AS kdist
      FROM kd k JOIN sparsity s ON s.rid = k.row ORDER BY k.kdist DESC LIMIT 200)
     TO '__OUTDIR__/sae-isolated-voicings.json' (FORMAT JSON, ARRAY true);

-- ── Human-readable summary (captured by the wrapper) ─────────────────────────
.mode markdown

SELECT '## Declared vs recomputed (reconcile)' AS section;
SELECT declared_dict_size, recomputed_dict_size,
       declared_alive, recomputed_alive,
       declared_dead_pct, recomputed_dead_pct, abs_delta, consistent
FROM reconcile;

SELECT '## Dictionary coverage (from manifest)' AS section;
SELECT dict_size, alive, dead, dead_pct,
       mean_act_alive, median_act_alive, p95_act_alive, max_act
FROM coverage;

SELECT '## Decoder-norm by liveness (dead features should have small norm)' AS section;
SELECT is_alive,
       count(*)                          AS n,
       round(avg(decoder_norm), 4)       AS mean_norm,
       round(min(decoder_norm), 4)       AS min_norm,
       round(max(decoder_norm), 4)       AS max_norm
FROM manifest GROUP BY is_alive ORDER BY is_alive DESC;

SELECT '## Top-k sparsity (parquet) — active features per voicing should equal k_sparse' AS section;
SELECT (SELECT model.k_sparse FROM art)              AS declared_k_sparse,
       count(*)                                      AS sampled_voicings,
       round(avg(active_features), 2)                AS mean_active,
       min(active_features)                          AS min_active,
       round(quantile_cont(active_features, 0.5), 0) AS median_active,
       max(active_features)                          AS max_active
FROM sparsity;

SELECT '## Local density in SAE feature space (ix_kdist, k=5) — higher = more isolated' AS section;
SELECT round(min(kdist), 4)                 AS min,
       round(quantile_cont(kdist, 0.5), 4)  AS median,
       round(quantile_cont(kdist, 0.95), 4) AS p95,
       round(max(kdist), 4)                 AS max
FROM kd;

SELECT '## Most isolated voicings (highest kdist — rare structure / inspect)' AS section;
SELECT s.rid AS voicing_ordinal, s.active_features, round(k.kdist, 4) AS kdist
FROM kd k JOIN sparsity s ON s.rid = k.row ORDER BY k.kdist DESC LIMIT 15;

-- Machine-greppable verdict for the wrapper (so it never depends on parsing the
-- markdown tables, where a stray 'false' in the is_alive column would false-match).
.mode line
SELECT 'RECONCILE_CONSISTENT=' || lower(consistent::VARCHAR) AS marker FROM reconcile;
