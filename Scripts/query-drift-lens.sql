-- Per-query routing/retrieval drift lens — IX DuckDB UDFs over the live query
-- embedding sink (GA → ix cross-repo Contract B, written by QueryEmbeddingLog).
--
-- Input: state/quality/query-embeddings/*.jsonl — one row per ROUTED query holding
-- the EXACT vector the SemanticIntentRouter scored with (NOT a re-embed), plus the
-- decision it drove: {query_id, ts, query_text, intent, route_method,
-- route_confidence, embedder, dim, embedding[]}.
--
-- Two questions, both over real runtime vectors:
--   * ix_kdist  — local density of each live query against the others. A query far
--     from all neighbours (high kdist) is OUT-OF-DISTRIBUTION for what the router
--     usually sees — the raw-cosine OOD signal ix's ROC sweep validated, computed
--     GA-side so you don't need the ix sibling checked out.
--   * ix_silhouette by routed intent — how separable the LIVE queries of each
--     intent are. This is the RUNTIME counterpart to routing-ambiguity-diagnostic
--     (which measures the design-time ANCHOR geometry): low/negative silhouette
--     here means real users' queries for an intent land amongst another intent's.
--
-- Embedder hygiene: rows can span embedder swaps (nomic → bge-large). Cosine across
-- embedders is meaningless, so the lens pins to the DOMINANT (embedder, dim) and
-- reports how many rows that dropped.
--
-- Placeholders __EXT__ / __GLOB__ / __OUTDIR__ / __SAMPLE__ are substituted by
-- Scripts/query-drift-lens.ps1. Run via that wrapper, not directly.

LOAD '__EXT__';

CREATE OR REPLACE TABLE raw AS
SELECT query_id, ts, query_text, intent, route_method, route_confidence,
       embedder, dim, CAST(embedding AS DOUBLE[]) AS vec
FROM read_json_auto('__GLOB__', format = 'newline_delimited',
                    union_by_name = true, maximum_object_size = 900000000);

-- Pin to the dominant embedder + its dominant dim (cosine is only valid within one).
SET VARIABLE top_embedder = (SELECT embedder FROM raw GROUP BY embedder ORDER BY count(*) DESC LIMIT 1);
SET VARIABLE top_dim      = (SELECT dim FROM raw WHERE embedder = getvariable('top_embedder')
                             GROUP BY dim ORDER BY count(*) DESC LIMIT 1);

CREATE OR REPLACE TABLE q AS
SELECT * FROM (
    SELECT (row_number() OVER (ORDER BY ts, query_id)) - 1 AS eid, *
    FROM raw
    WHERE embedder = getvariable('top_embedder') AND dim = getvariable('top_dim')
) WHERE eid < __SAMPLE__;

-- ── Local density / OOD over ALL pinned queries (declined ones included — they
--    are still real vectors the router saw) ─────────────────────────────────────
SET VARIABLE q_vecs = (SELECT to_json(list(vec ORDER BY eid)) FROM q);
CREATE OR REPLACE TABLE kd AS SELECT * FROM ix_kdist(getvariable('q_vecs'), 5);

-- ── Runtime per-intent separability over ROUTED queries only ──────────────────
CREATE OR REPLACE TABLE routed AS
SELECT (row_number() OVER (ORDER BY eid)) - 1 AS rid, eid, intent, query_text, vec
FROM q WHERE intent IS NOT NULL;

CREATE OR REPLACE TABLE intent_dim AS
SELECT intent, (dense_rank() OVER (ORDER BY intent)) - 1 AS label
FROM (SELECT DISTINCT intent FROM routed);

-- ix_silhouette needs >= 2 labels; guarded by the wrapper which requires a corpus
-- spanning multiple intents (a single-intent sink can't be confusable anyway).
SET VARIABLE r_vecs = (SELECT to_json(list(vec ORDER BY rid)) FROM routed);
SET VARIABLE r_labs = (SELECT to_json(list(d.label ORDER BY r.rid))
                       FROM routed r JOIN intent_dim d USING (intent));
CREATE OR REPLACE TABLE sil AS
SELECT * FROM ix_silhouette(getvariable('r_vecs'), getvariable('r_labs'));

-- ── Structured outputs ───────────────────────────────────────────────────────
COPY (SELECT q.query_text, q.intent, q.route_method,
             round(q.route_confidence, 4) AS route_confidence, round(k.kdist, 5) AS kdist
      FROM kd k JOIN q ON q.eid = k.row ORDER BY k.kdist DESC LIMIT 200)
     TO '__OUTDIR__/query-drift-isolated.json' (FORMAT JSON, ARRAY true);
COPY (SELECT r.intent,
             count(*)                       AS n_queries,
             round(avg(k.kdist), 5)         AS mean_kdist,
             round(avg(s.silhouette), 5)    AS mean_silhouette
      FROM routed r
      JOIN kd  k ON k.row = r.eid
      JOIN sil s ON s.row = r.rid
      GROUP BY r.intent ORDER BY mean_silhouette ASC)
     TO '__OUTDIR__/query-drift-per-intent.json' (FORMAT JSON, ARRAY true);

-- ── Human-readable summary (captured by the wrapper) ─────────────────────────
.mode markdown

SELECT '## Sample' AS section;
SELECT count(*)                                            AS sampled_queries,
       (SELECT count(*) FROM raw)                          AS total_rows,
       getvariable('top_embedder')                        AS embedder,
       getvariable('top_dim')                              AS dim,
       (SELECT count(*) FROM raw) - (SELECT count(*) FROM q) AS dropped_offembedder,
       count(*) FILTER (WHERE intent IS NULL)              AS declined,
       round(100.0 * count(*) FILTER (WHERE intent IS NULL) / count(*), 1) AS declined_pct,
       (SELECT count(*) FROM intent_dim)                   AS n_intents
FROM q;

SELECT '## Out-of-distribution signal (ix_kdist, k=5) — higher = more isolated query' AS section;
SELECT round(min(kdist), 4)                  AS min,
       round(quantile_cont(kdist, 0.5), 4)   AS median,
       round(quantile_cont(kdist, 0.95), 4)  AS p95,
       round(max(kdist), 4)                  AS max
FROM kd;

SELECT '## Most out-of-distribution queries (highest kdist — review / add coverage)' AS section;
SELECT q.query_text, q.intent, q.route_method, round(q.route_confidence, 3) AS conf,
       round(k.kdist, 4) AS kdist
FROM kd k JOIN q ON q.eid = k.row ORDER BY k.kdist DESC LIMIT 20;

SELECT '## Runtime per-intent separability (ix_silhouette) — lowest first = live queries overlap other intents' AS section;
SELECT r.intent, count(*) AS n,
       round(avg(s.silhouette), 4) AS mean_silhouette,
       round(avg(k.kdist), 4)      AS mean_kdist
FROM routed r JOIN sil s ON s.row = r.rid JOIN kd k ON k.row = r.eid
GROUP BY r.intent ORDER BY mean_silhouette ASC;

SELECT '## Lowest-confidence routed queries (closest to the decline threshold)' AS section;
SELECT query_text, intent, round(route_confidence, 4) AS conf
FROM q WHERE intent IS NOT NULL ORDER BY route_confidence ASC LIMIT 15;

-- Machine-greppable verdict for the wrapper.
.mode line
SELECT 'QUERY_DRIFT_OK=' || count(*) AS marker FROM kd;
