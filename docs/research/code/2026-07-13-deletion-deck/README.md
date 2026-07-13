# Deletion-deck reconstruction — reproducible code

Companion code for
[`../../2026-07-13-deletion-deck-reconstruction-theorem.md`](../../2026-07-13-deletion-deck-reconstruction-theorem.md)
and
[`../../2026-07-13-deletion-deck-hostile-referee-report.md`](../../2026-07-13-deletion-deck-hostile-referee-report.md).

These are the exact scripts that produced the reported numbers (not cleaned-up
rewrites). Pure Python 3 stdlib + one C file; no dependencies.

| File | What it does | Runtime |
|---|---|---|
| `zn_reconstruction_v2.py` | Exhaustive set-deck collision sweep, Z17–Z20 + full collision inventory Z10/Z15 | seconds |
| `zn_deck.c` | C sweep for large moduli (used up to Z30 ≈ 1.07B subsets): set-deck **and** multiset-deck collisions via FNV hash tables, dihedral orbit-fill canonicalization, reps only. `gcc -O2 -o zn_deck zn_deck.c && ./zn_deck 25` | Z25 ≈ 4 s; Z30 ≈ minutes |
| `hostile_referee.py` | Points 1/B/C of the hostile-referee mission, 6 ≤ N ≤ 16: explicit Z10 n=3 ICV counterexample; full enumeration of the parity system {mᵢ ≥ 1, Σmᵢ = n, Aᵀm ≡ 0 mod 2}; ICV-integrality augmentation; realizability of surviving alternatives | < 1 s |
| `referee_ext.py` | Same bench, list-based canonicalization, 17 ≤ N ≤ 20 | ~2 min |
| `realizability_n3.py` | Point C closure at n=3: which abstract {(1,2),(2,1)} ambiguities are *doubly realizable*, 6 ≤ N ≤ 20 | ~2 min |
| `point_d_lemma.py` | Point D: repeated-card lemma sub-case verification (symmetry / impossible / exchange), exhaustive 6 ≤ N ≤ 14 with assertions | ~1 min |
| `referee_6_16.txt`, `referee_17_20.txt` | Captured outputs of the two bench runs | — |

Every script asserts internally (Kelly parity of the true multiplicity vector,
membership of the true vector in the solution set, impossibility of sub-case
(ii)); a silent completion means all assertions passed.
