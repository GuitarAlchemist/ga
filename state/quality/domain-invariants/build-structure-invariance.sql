-- Tier-2: is OPTIC-K's STRUCTURE partition actually transposition-invariant?
-- STRUCTURE is *claimed* O+P+T+I-invariant. This measures it: for every set class, the
-- STRUCTURE vector of transposition 0 vs each transposition k must be cosine-identical (1.0)
-- if the claim holds. Run from state/quality/domain-invariants/:  duckdb < build-structure-invariance.sql

CREATE OR REPLACE TABLE st AS SELECT * FROM read_json_auto('structure-transpositions.jsonl');

.print '== universe =='
SELECT count(DISTINCT setclass) AS set_classes, count(*) AS structure_vectors FROM st;

.print '== T-invariance: min cosine(t0, tk) per set class — 1.0 == invariant. WORST 15 =='
WITH base AS (SELECT setclass, structure AS s0 FROM st WHERE transposition = 0)
SELECT st.setclass, st.cardinality,
       round(min(list_cosine_similarity(b.s0, st.structure)), 4) AS min_cosine_vs_t0
FROM st JOIN base b USING (setclass)
WHERE st.transposition > 0
GROUP BY st.setclass, st.cardinality
ORDER BY min_cosine_vs_t0
LIMIT 15;

.print '== verdict: how many set classes are T-invariant (min cosine >= 0.9999) vs NOT =='
WITH base AS (SELECT setclass, structure AS s0 FROM st WHERE transposition = 0),
     per AS (
       SELECT st.setclass, min(list_cosine_similarity(b.s0, st.structure)) AS mc
       FROM st JOIN base b USING (setclass) WHERE st.transposition > 0
       GROUP BY st.setclass)
SELECT
  count(*) FILTER (WHERE mc >= 0.9999) AS t_invariant,
  count(*) FILTER (WHERE mc <  0.9999) AS NOT_t_invariant,
  round(avg(mc), 4) AS mean_min_cosine,
  round(min(mc), 4) AS worst_cosine
FROM per;

.print '== the canonical example: a major triad vs its transposition =='
WITH base AS (SELECT setclass, structure AS s0 FROM st WHERE transposition = 0)
SELECT st.setclass, st.transposition,
       round(list_cosine_similarity(b.s0, st.structure), 4) AS cosine_vs_t0
FROM st JOIN base b USING (setclass)
WHERE st.cardinality = 3 AND st.setclass LIKE '%<0 0 1 1 1 0>%'
ORDER BY st.transposition;
