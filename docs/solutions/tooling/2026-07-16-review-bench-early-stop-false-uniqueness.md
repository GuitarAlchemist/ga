---
module: docs/research code benches / any verification harness
tags: [verification, bench, early-stop, truncation, false-uniqueness, assertions, hostile-review]
problem_type: runtime_error
decision: "Verification benches must enumerate ALL candidates (or explicitly flag truncation as 'unverified'); and every bench must assert that the known ground truth belongs to its own enumerated set."
rejected:
  - "Keeping the limit=3 early stop for performance (candidate counts were tiny — C(n-1,r-1) ≤ 6435 — the optimization was pure risk, zero benefit)."
reason: "The first multiplicity-recovery bench stopped enumerating parity solutions after 3 hits. Downstream, an ICV filter selected among the TRUNCATED list — so 'exactly one survivor' could mean 'exactly one among the first three found', silently certifying false uniqueness. The bug was caught before any number was reported, by the self-assertion `assert mstar in sols` failing at (Z8, n=7): the true vector wasn't among the first three lexicographic solutions. Without that assertion the campaign's headline experimental claim could have been wrong."
date_decided: 2026-07-16
---

# Early-stopped enumeration + downstream filter = silent false uniqueness

## The trap, generalized

Any pipeline of the form *enumerate candidates (capped) → filter → count
survivors* can certify uniqueness that is an artifact of the cap. The cap and
the filter are individually innocent; their composition is the bug. This is
not specific to math benches — the same shape appears in search-then-rank
harnesses, dedup passes, and top-K recall tests.

## The two defenses (both cheap)

1. **Enumerate everything when the space is small** — measure it first; the
   "optimization" that motivated the cap saved nothing here (composition
   counts ≤ C(15,7)).
2. **Assert ground truth membership in the bench's own output**
   (`assert truth in enumerated`). This is the assertion that fired. Every
   bench in the campaign now carries self-assertions (Kelly parity of the
   true vector, impossibility of dead sub-cases), so a silent pass means
   every internal invariant held — a materially stronger statement than
   "the script printed the expected summary."

## Where this lives

`docs/research/code/2026-07-13-deletion-deck/hostile_referee.py` (fixed
version, with the assertion and full enumeration); the incident is recorded
in `2026-07-13-deletion-deck-hostile-referee-report.md` §4.
