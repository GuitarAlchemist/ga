-- build-views.sql — DuckDB quality analytics layer for Guitar Alchemist
--
-- Materializes the file-based quality artifacts under state/quality/ into a
-- self-contained quality.duckdb so trends are queryable across sessions and
-- from .NET (Tools/QualityLens) without depending on JSON glob paths.
--
-- Refresh (run from the state/quality/ directory):
--   duckdb analytics/quality.duckdb < analytics/build-views.sql
--
-- Design notes:
--   * union_by_name=true tolerates the schema drift between snapshots
--     (e.g. chatbot-qa gained `degraded`/`degraded_reason` on 2026-05-24).
--   * Tables are materialized (CREATE OR REPLACE TABLE AS) so the .duckdb is
--     portable; re-run this script to pick up new daily snapshots.
--   * The day is parsed from the filename, which is the canonical date key
--     across the quality pipeline (the YYYY-MM-DD stem convention).

-- chatbot-qa: daily prompt-corpus pass rate (NULL when backend was degraded).
CREATE OR REPLACE TABLE chatbot_qa AS
SELECT
    regexp_extract(filename, '([0-9]{4}-[0-9]{2}-[0-9]{2})', 1)        AS day,
    total_prompts,
    TRY_CAST(pass_pct AS DOUBLE)                                       AS pass_pct,
    COALESCE(degraded, false)                                          AS degraded,
    degraded_reason,
    TRY_CAST(last_known_good_pass_pct AS DOUBLE)                       AS last_known_good_pass_pct
FROM read_json_auto('chatbot-qa/*.json', filename = true, union_by_name = true)
WHERE regexp_full_match(
        regexp_extract(filename, '([0-9]{4}-[0-9]{2}-[0-9]{2})', 1),
        '[0-9]{4}-[0-9]{2}-[0-9]{2}')          -- excludes baseline.json
ORDER BY day;

-- routing-eval: semantic intent router accuracy over the eval corpus.
CREATE OR REPLACE TABLE routing_eval AS
SELECT
    regexp_extract(filename, '([0-9]{4}-[0-9]{2}-[0-9]{2})', 1)        AS day,
    totalPrompts                                                       AS total_prompts,
    overall.Total                                                      AS total,
    overall.Correct                                                    AS correct,
    overall.Accuracy                                                   AS accuracy,
    overall.InScopeAccuracy                                            AS in_scope_accuracy,
    overall.OosDeclineRate                                             AS oos_decline_rate
FROM read_json_auto('routing-eval-*.json', filename = true, union_by_name = true)
ORDER BY day;

-- voicing-analysis: OPTIC-K corpus health (size + chord-recognition coverage).
CREATE OR REPLACE TABLE voicing_analysis AS
SELECT
    regexp_extract(filename, '([0-9]{4}-[0-9]{2}-[0-9]{2})', 1)        AS day,
    Corpus.Total                                                       AS corpus_total,
    Corpus.Guitar                                                      AS corpus_guitar,
    Corpus.Bass                                                        AS corpus_bass,
    Corpus.Ukulele                                                     AS corpus_ukulele,
    ChordRecognition.DistinctChordNames                                AS distinct_chord_names,
    ChordRecognition.NullChordName.Pct                                 AS null_chord_pct,
    ChordRecognition.UnknownChordName.Pct                              AS unknown_chord_pct
FROM read_json_auto('voicing-analysis/*.json', filename = true, union_by_name = true)
ORDER BY day;

-- pr-grades: post-merge intent-vs-delivery grade cards (/grade-last-pr).
-- No grade cards exist yet (only README.md + SCHEMA.json), so this starts as an
-- explicit empty table. Once <merge-sha>.json cards land, replace the body with
-- the read below and re-run:
--
--   CREATE OR REPLACE TABLE pr_grades AS
--   SELECT pr_number, merge_sha, merged_at, title, alignment, grader, graded_at
--   FROM read_json_auto('pr-grades/[0-9a-f]*.json', union_by_name = true)
--   WHERE schema = 'pr-grade-v1';
CREATE OR REPLACE TABLE pr_grades (
    pr_number  BIGINT,
    merge_sha  VARCHAR,
    merged_at  VARCHAR,
    title      VARCHAR,
    alignment  VARCHAR,
    grader     VARCHAR,
    graded_at  VARCHAR
);

-- Unified latest-value-per-source rollup. A view over the materialized tables,
-- so it carries no JSON path dependency and is safe to query from anywhere.
CREATE OR REPLACE VIEW quality_latest AS
SELECT 'chatbot_qa'       AS source, day, pass_pct          AS metric, 'pass_pct'          AS metric_name FROM chatbot_qa       QUALIFY row_number() OVER (ORDER BY day DESC) = 1
UNION ALL
SELECT 'routing_eval'     AS source, day, accuracy          AS metric, 'accuracy'          AS metric_name FROM routing_eval     QUALIFY row_number() OVER (ORDER BY day DESC) = 1
UNION ALL
SELECT 'voicing_analysis' AS source, day, corpus_total      AS metric, 'corpus_total'      AS metric_name FROM voicing_analysis QUALIFY row_number() OVER (ORDER BY day DESC) = 1;
