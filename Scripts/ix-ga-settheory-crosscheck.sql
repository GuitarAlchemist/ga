-- IX ⟷ GA set-theory cross-check — runs IX's DuckDB set-theory UDFs over GA's
-- set classes and compares against GA's independent C# engine.
--
-- Input: ga-settheory-<date>.json (emitted by GA's SetTheoryCrossCheckCorpusEmitter)
--   — one row per set class with GA's prime form + interval-class vector.
--
-- The interval-class vector is convention-FREE, so ix_icv MUST match GA's ICV
-- for every set class (any mismatch = a real bug). Prime form / Forte label are
-- convention-DEPENDENT (Rahn vs Forte 1973), so those are reported as
-- informational — the disagreements map the convention gap, not bugs.
--
-- Placeholders __EXT__ / __CORPUS__ / __OUTDIR__ are filled by
-- Scripts/ix-ga-settheory-crosscheck.ps1. Run via that wrapper.

LOAD '__EXT__';

CREATE OR REPLACE TABLE corpus AS
SELECT CAST(u.primeForm AS BIGINT[]) AS prime,
       CAST(u.gaIcv     AS BIGINT[]) AS ga_icv,
       u.cardinality                 AS card
FROM (SELECT unnest(items) AS u FROM read_json('__CORPUS__', maximum_object_size = 900000000));

-- Apply the IX UDFs once each, then parse ix_icv's "<a,b,c,d,e,f>" into a list.
CREATE OR REPLACE TABLE compared AS
SELECT
    prime, card, ga_icv,
    ix_icv(prime)          AS ix_icv_str,
    list_transform(
        string_split(trim(ix_icv(prime), '<>'), ','),
        lambda x: CAST(trim(x) AS BIGINT))                  AS ix_icv,
    ix_prime_form(prime)   AS ix_prime_str,
    ix_forte_number(prime) AS ix_forte,
    '[' || array_to_string(prime, ',') || ']'               AS ga_prime_str
FROM corpus;

CREATE OR REPLACE TABLE icv_cmp AS
SELECT *, (ix_icv IS NOT NULL AND ga_icv = ix_icv) AS icv_match FROM compared;

-- ── Structured outputs ───────────────────────────────────────────────────────
COPY (SELECT card, ga_prime_str, ga_icv, ix_icv, ix_icv_str
      FROM icv_cmp WHERE NOT icv_match ORDER BY card)
     TO '__OUTDIR__/settheory-icv-disagreements.json' (FORMAT JSON, ARRAY true);
COPY (SELECT card, ga_prime_str, ix_prime_str, ix_forte
      FROM compared WHERE ix_prime_str <> ga_prime_str ORDER BY card)
     TO '__OUTDIR__/settheory-primeform-differences.json' (FORMAT JSON, ARRAY true);

-- ── Human-readable summary (captured by the wrapper) ─────────────────────────
.mode markdown

SELECT '## ICV cross-check — GA engine vs ix_icv (convention-free; MUST be 100%)' AS section;
SELECT count(*)                                              AS n_set_classes,
       sum(icv_match::INT)                                   AS icv_agree,
       count(*) - sum(icv_match::INT)                        AS icv_disagree,
       round(100.0 * sum(icv_match::INT) / count(*), 2)      AS agree_pct
FROM icv_cmp;

SELECT '## ICV disagreements (each is a real bug in one engine)' AS section;
SELECT card, ga_prime_str, ga_icv, ix_icv, ix_icv_str
FROM icv_cmp WHERE NOT icv_match ORDER BY card LIMIT 40;

SELECT '## Prime-form agreement (informational — Rahn vs Forte convention)' AS section;
SELECT count(*)                                                          AS n,
       sum((ix_prime_str = ga_prime_str)::INT)                           AS prime_agree,
       round(100.0 * sum((ix_prime_str = ga_prime_str)::INT) / count(*), 2) AS prime_agree_pct,
       sum((ix_forte IS NULL)::INT)                                      AS ix_forte_unclassified
FROM compared;

SELECT '## Prime-form differences (sample — convention, not bugs)' AS section;
SELECT card, ga_prime_str, ix_prime_str, ix_forte
FROM compared WHERE ix_prime_str <> ga_prime_str ORDER BY card LIMIT 25;
