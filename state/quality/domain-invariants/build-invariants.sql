-- GA domain-invariants sweep (Tier 1: structural, no embeddings).
-- Run from this directory:  duckdb < build-invariants.sql
-- Each invariant query returns ROWS ONLY ON VIOLATION; an empty result = the law
-- holds across the entire finite universe (all 4096 pitch-class sets / full set-class
-- catalog). Born from the IntervalClassVector.Major base-12 bug — the same class of
-- latent error is one SQL line over the whole universe.

CREATE OR REPLACE TABLE pcs AS SELECT * FROM read_json_auto('pitchclasssets.jsonl');
CREATE OR REPLACE TABLE sc  AS SELECT * FROM read_json_auto('setclasses.jsonl');

.print '== universe =='
SELECT (SELECT count(*) FROM pcs) AS pitch_class_sets,
       (SELECT count(*) FROM sc)  AS set_classes;

.print '== I1: ICV counts must sum to C(n,2) (VIOLATIONS) =='
SELECT id, cardinality, icv, list_sum(icv) AS got, cardinality*(cardinality-1)/2 AS expected
FROM pcs
WHERE list_sum(icv) <> cardinality*(cardinality-1)/2
ORDER BY cardinality;

.print '== I2: base-12 re-encode of icv must reproduce icv_id (VIOLATIONS) =='
SELECT id, cardinality, icv, icv_id,
       icv[1]*248832 + icv[2]*20736 + icv[3]*1728 + icv[4]*144 + icv[5]*12 + icv[6] AS reencoded
FROM pcs
WHERE icv[1]*248832 + icv[2]*20736 + icv[3]*1728 + icv[4]*144 + icv[5]*12 + icv[6] <> icv_id
ORDER BY cardinality;

.print '== I3: every ICV count must fit a base-12 digit 0..11 (VIOLATIONS) =='
SELECT id, cardinality, icv, list_max(icv) AS max_count
FROM pcs
WHERE list_max(icv) > 11
ORDER BY cardinality;

.print '== I4: a set and its prime form must share the same ICV (T+I invariance) (VIOLATIONS) =='
SELECT p.id, p.cardinality, p.icv_id AS set_icv, q.icv_id AS primeform_icv
FROM pcs p JOIN pcs q ON p.prime_form_id = q.id
WHERE p.icv_id <> q.icv_id
ORDER BY p.cardinality;

.print '== I5: distinct prime forms among all sets must equal the set-class count =='
SELECT (SELECT count(DISTINCT prime_form_id) FROM pcs WHERE prime_form_id IS NOT NULL) AS distinct_prime_forms,
       (SELECT count(*) FROM sc) AS set_classes;

.print '== D: Z-related set classes (DISCOVERY — same ICV, different prime form) =='
SELECT icv_id, count(*) AS n_classes, list(label) AS classes
FROM sc
GROUP BY icv_id
HAVING count(*) > 1
ORDER BY icv_id;
