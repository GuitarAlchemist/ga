-- Routing-ambiguity diagnostic — IX DuckDB extension over the router's anchors.
--
-- Input: routing-anchors-<date>.json (emitted by RoutingEvalHarness
--   .DumpRoutingAnchors_ForAmbiguityDiagnostic) — every routed intent's
--   description + example prompts, embedded with the SAME embedder/normalization
--   the production SemanticIntentRouter uses.
--
-- The router classifies a query by max-cosine against these anchors, so two
-- intents whose example-prompt clouds overlap in embedding space MISROUTE into
-- each other. This script quantifies that overlap:
--   * ix_silhouette  — per-intent separability (low = confusable)
--   * ix_cosine      — nearest-wrong-neighbour → which intent PAIRS overlap
--   * ix_pca_project — 2-D coords for a scatter plot
--
-- Placeholders __EXT__ / __ANCHORS__ / __OUTDIR__ are substituted by
-- Scripts/routing-ambiguity-diagnostic.ps1. Run via that wrapper, not directly.

LOAD '__EXT__';

-- ── Load anchors ─────────────────────────────────────────────────────────────
-- read_json yields one row with an `anchors` array of {IntentId,Kind,Text,Vector}.
CREATE OR REPLACE TABLE anchor AS
SELECT (row_number() OVER ()) - 1          AS rid,
       u.IntentId                          AS intent_id,
       u.Kind                              AS kind,
       u.Text                              AS text,
       CAST(u.Vector AS DOUBLE[])          AS vec
FROM (
    SELECT unnest(anchors) AS u
    FROM read_json('__ANCHORS__', maximum_object_size = 900000000)
);

-- Integer label per intent — ix_silhouette needs integer cluster labels.
CREATE OR REPLACE TABLE intent_dim AS
SELECT intent_id, (dense_rank() OVER (ORDER BY intent_id)) - 1 AS label
FROM (SELECT DISTINCT intent_id FROM anchor);

-- Example anchors only = the user-query-like decision basis. (Descriptions are
-- also anchors but are sentence-style meta-text; examples are what queries land
-- on.) Contiguous 0-based eid so ix_silhouette's `row` aligns to it.
CREATE OR REPLACE TABLE ex AS
SELECT (row_number() OVER (ORDER BY a.rid)) - 1 AS eid,
       a.intent_id, d.label, a.text, a.vec
FROM anchor a JOIN intent_dim d USING (intent_id)
WHERE a.kind = 'example';

-- DuckDB forbids subqueries as table-function arguments, so stage the JSON
-- vector/label payloads in session variables and pass them via getvariable().
SET VARIABLE ex_vecs   = (SELECT to_json(list(vec   ORDER BY eid)) FROM ex);
SET VARIABLE ex_labels = (SELECT to_json(list(label ORDER BY eid)) FROM ex);

-- ── Silhouette (per-point separability) ──────────────────────────────────────
CREATE OR REPLACE TABLE sil AS
SELECT * FROM ix_silhouette(getvariable('ex_vecs'), getvariable('ex_labels'));

CREATE OR REPLACE TABLE per_intent AS
SELECT e.intent_id,
       count(*)                    AS n_examples,
       round(avg(s.silhouette), 4) AS mean_silhouette,
       round(min(s.silhouette), 4) AS min_silhouette
FROM sil s JOIN ex e ON e.eid = s.row
GROUP BY e.intent_id;

-- ── Nearest-wrong-neighbour (which example prompts collide across intents) ────
CREATE OR REPLACE TABLE pair_cos AS
SELECT a.eid        AS a_eid,
       a.intent_id  AS a_intent, a.text AS a_text,
       b.intent_id  AS b_intent, b.text AS b_text,
       ix_cosine(a.vec, b.vec) AS cos
FROM ex a JOIN ex b ON a.intent_id <> b.intent_id;

CREATE OR REPLACE TABLE nwn AS
SELECT *, row_number() OVER (PARTITION BY a_eid ORDER BY cos DESC) AS rk
FROM pair_cos;

CREATE OR REPLACE TABLE confusable_pairs AS
SELECT a_intent, b_intent,
       count(*)              AS n_anchors_nearest,
       round(avg(cos), 4)    AS avg_nearest_cos,
       round(max(cos), 4)    AS max_cos
FROM nwn WHERE rk = 1
GROUP BY a_intent, b_intent;

-- ── 2-D PCA scatter (for plotting the intent space) ──────────────────────────
CREATE OR REPLACE TABLE pca AS
SELECT p.row AS eid, p.coords[1] AS pc1, p.coords[2] AS pc2, e.intent_id
FROM ix_pca_project(getvariable('ex_vecs'), 2) p
JOIN ex e ON e.eid = p.row;

-- ── Structured outputs (consumed by the quality pipeline) ────────────────────
COPY (SELECT * FROM per_intent ORDER BY mean_silhouette ASC)
     TO '__OUTDIR__/routing-silhouette-by-intent.json' (FORMAT JSON, ARRAY true);
COPY (SELECT * FROM confusable_pairs WHERE avg_nearest_cos > 0.5 ORDER BY avg_nearest_cos DESC)
     TO '__OUTDIR__/routing-confusable-pairs.json' (FORMAT JSON, ARRAY true);
COPY (SELECT * FROM pca ORDER BY eid)
     TO '__OUTDIR__/routing-pca-coords.json' (FORMAT JSON, ARRAY true);

-- ── Human-readable summary (captured to the markdown report by the wrapper) ──
.mode markdown

SELECT '## Overall separability' AS section;
SELECT round(avg(silhouette), 4) AS overall_mean_silhouette,
       count(*)                  AS n_example_anchors,
       (SELECT count(*) FROM intent_dim) AS n_intents
FROM sil;

SELECT '## Per-intent separability (lowest first = most confusable)' AS section;
SELECT intent_id, n_examples, mean_silhouette, min_silhouette
FROM per_intent ORDER BY mean_silhouette ASC;

SELECT '## Top confusable intent pairs (nearest wrong-neighbour, avg cos > 0.5)' AS section;
SELECT a_intent, b_intent, n_anchors_nearest, avg_nearest_cos, max_cos
FROM confusable_pairs WHERE avg_nearest_cos > 0.5
ORDER BY avg_nearest_cos DESC LIMIT 25;

SELECT '## Worst individual anchor collisions (an example prompt closer to a wrong intent)' AS section;
SELECT a_intent, a_text, b_intent, b_text, round(cos, 4) AS cos
FROM nwn WHERE rk = 1 ORDER BY cos DESC LIMIT 20;
