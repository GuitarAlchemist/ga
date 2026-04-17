# ix-bracelet — Dihedral Operator Algebra for Pitch-Class Sets

**Status:** Design
**Date:** 2026-04-17
**Reversibility:** Two-way door (additive crate; existing APIs unchanged)
**Revisit trigger:** After Phase 1 (crate skeleton + ≥5 catalog invariants converted from tests to theorems) lands, or if the MCP-boundary cost (see §7) exceeds 5ms p50 in hot paths.

Produced by `octo:personas:backend-architect` dispatch on 2026-04-17 per invariant-optimality + bracelet-notation discussion.

---

## 1. Public API

**Core types.**

- `PcSet(u16)` — 12-bit mask over Z/12. Only the low 12 bits are used; bit `k` set ⟺ pitch-class `k` present. Cardinality via `popcount`. This is the carrier of all group actions.
- `DihedralElement { rotation: u8 /* 0..12 */, reflected: bool }` — canonical encoding `g = r^i · s^j`. The 24 elements form D₁₂. Stored in 1 byte.
- `trait Action<T> { fn apply(&self, x: T) -> T; }` — implemented at minimum by `DihedralElement: Action<PcSet>`. The same trait can later carry the action onto triads, intervals, or Forte labels without rewriting call sites.
- `trait Group { fn identity() -> Self; fn compose(&self, other: &Self) -> Self; fn inverse(&self) -> Self; fn order(&self) -> u8; }` — lets tests and callers verify group laws uniformly.
- `fn bracelet_prime_form(x: PcSet) -> PcSet` — canonical D₁₂-orbit representative (§4).
- `fn necklace_prime_form(x: PcSet) -> PcSet` — canonical ⟨r⟩-orbit representative (transposition only).
- `fn orbit_of(x: PcSet) -> OrbitHandle` — lazy iterator over the D₁₂ orbit.

**Composition.** `T3 ∘ I` is `DihedralElement::rotation(3).compose(&DihedralElement::inversion())`. Composition lives on `DihedralElement`, not on `PcSet`, so callers fold a pipeline once and apply the result to many sets. Builder: `DihedralElement::from_tn_tni(n, inverted)`.

**Verifying group laws** is `g.compose(&g.inverse()) == identity()` plus enumeration of the 576 pairs for closure — exposed as a feature-gated `verify_group_laws()` helper.

## 2. Subgroup Taxonomy

| Operator | DihedralElement | Order | Subgroup |
|---|---|---|---|
| T_n (n=0..11) | `rotation(n)` | 12/gcd(n,12) | C₁₂ = ⟨r⟩ |
| I (about 0) | reflected about 0 | 2 | ⟨s⟩ |
| T_n I | `rotation(n) ∘ inversion` | 2 | coset rC₁₂·s |
| P (triadic) | `rotation(0) ∘ inversion about root+third` | 2 | conjugate of ⟨s⟩ |
| L | `rotation(±4) ∘ inversion` | 2 | coset |
| R | `rotation(-3 or +9) ∘ inversion` | 2 | coset |
| S (Slide) | `L·P·R` | 2 | coset |
| N (Nebenverwandt) | `R·L·P` | 2 | coset |
| H (Hexatonic) | `L·P·L` | 2 | coset |

P/L/R are **partial functions** on the 24 consonant triads. Represented as `DihedralElement` + precondition `PcSet::is_consonant_triad()`. S/N/H derived by composition and memoized.

The GA C#/F# side keeps its names; Rust exposes `neo_riemannian_table() -> [(&'static str, DihedralElement); 6]` so the C# MCP caller resolves names without duplicating the derivation.

## 3. Invariant Guarantees

**Theorems (by construction).**

| Catalog # | Statement | Reduces to |
|---|---|---|
| #14 | P, L, R are involutions | `order(g) = 2` for each coset rep |
| #16 | T₁₂ = identity | `r¹² = e` |
| #17 | I ∘ I = identity | `s² = e` |
| #18 | T_n ∘ T_m = T_{n+m mod 12} | ⟨r⟩ abelian of order 12 |
| #19 | (T_n I)² = identity | reflections have order 2 |
| #25 | Cross-instrument STRUCTURE equal for same PC-set | ICV is a D₁₂ invariant; bracelet prime form refines it |
| #26 | Forte class is Tn/TnI-closed | definition of D₁₂ orbit |

~7 of the 35 catalog invariants become theorems. The remaining ~28 concern data ingestion, numerical, or governance properties outside the group-theoretic core — still asserted by test.

## 4. Bracelet Prime Form Algorithm

**Spec.** Return the lexicographically smallest 12-bit mask among `{ g·x : g ∈ D₁₂ }`, with lex order on the sorted pitch-class list (Forte's convention).

```
fn bracelet_prime_form(x):
    best = x
    for i in 0..12:
        rot = rotate_left(x, i)       // O(1) bit op
        if lex_less(rot, best): best = rot
        inv = reflect(rot)            // bit-reverse within 12 bits
        if lex_less(inv, best): best = inv
    return best
```

**Bound.** 24 candidates; lex compare is O(n) on cardinality n ≤ 12. Effectively **O(1)** — bounded ≤ 288 bit ops. **Idempotence** by construction.

## 5. Hash-Indexed Lookup

Combinatorics: **224 D₁₂-orbits** on subsets of Z/12 (Burnside/Pólya on 2¹²). Matches Forte set class count including degenerate edge cases.

**Construction.** At crate init (via `OnceLock`), enumerate all 4096 subsets, compute `bracelet_prime_form`, bucket into `HashMap<PcSet, SmallVec<[PcSet; 24]>>`. Memory ~12 KB, build time ~100 µs.

**Query.** `orbit_of(x) = table[bracelet_prime_form(x)]` — one hash, one clone. Parallel 352-entry necklace table (Burnside on C₁₂) for transposition-only callers.

## 6. Integration Plan

1. **Workspace.** Add `ix-bracelet` (deps: `smallvec` only).
2. **`ix-optick` retrofit.** Replace ad-hoc `transpose(n)` with `DihedralElement::rotation(n).apply(pcs)`. STRUCTURE partition gets one bit-packed dim from `bracelet_prime_form(pcs)` — any two voicings in the same Forte class map to identical STRUCTURE bits **before** any learned layer touches them. That's the invariance-by-construction payoff.
3. **GA C#/F# bridge.** MCP tools `ix.bracelet.apply { op, pc_set }` and `ix.bracelet.prime_form { pc_set }`. C# Neo-Riemannian keeps its surface API but delegates in release builds; keeps native fallback for offline/test paths. Migration is additive.
4. **Forte cross-check.** `ProgrammaticForteCatalog` (C#) adds a verification pass comparing its Forte labels against `ix.bracelet.prime_form`. Divergences become governance findings.

## 7. Risks

- **Binary mask ≠ multiset.** `PcSet` can't express octave doublings in a voicing. Need `PcMultiset` companion; D₁₂ acts on both, but only the mask version admits 224-orbit combinatorics. `Action<T>` generic so both coexist.
- **Hot-path cost.** OPTIC-K calls `bracelet_prime_form` per voicing (~10⁵). ~100 ns each → ~10 ms total. Acceptable, but bench against a naive lookup table keyed by the raw mask.
- **C# boundary.** MCP round-trip adds ~1 ms per call; batch via `prime_form_batch`. Native C# fallback must stay byte-identical — add a cross-implementation fuzzer.
- **Edge cases.** Empty set and chromatic aggregate are fixed points; singletons collapse under transposition but not always under inversion. Document as 0-card / 12-card edge cases; don't let them leak as outliers into learned layers.

## 8. Acceptance Tests

- **Group laws exhaustive.** All 576 `(g, h)` pairs: `g.compose(&h).inverse() == h.inverse().compose(&g.inverse())` and `g.compose(&g.inverse()) == identity()`. Closure: every composite in the 24-element table.
- **Subgroup structure.** `⟨r⟩` has 12 elements. `T_n I`, P, L, R, S, N, H all order 2. `{P, L, R}` generate a 24-element group isomorphic to D₁₂ restricted to consonant triads (orbit-stabilizer check).
- **Bracelet prime form coarsening.** For every `(x, y)` with `y = g·x`: `prime_form(x) == prime_form(y)`. Conversely, different prime forms ⇒ no `g` maps one to the other (exhaust 24). Idempotence on all 4096 sets.
- **Orbit round-trip.** `orbit_of(x)` contains `x`, has size dividing 24, every element shares prime form.
- **Forte cross-check.** Vendored Forte table fixture: `bracelet_prime_form` agrees with the published prime form on all 224 classes.
- **Invariant-catalog regression.** Each of the seven §3 invariants gets a test invoking only `ix-bracelet` APIs — theorems hold at the algebra layer, independent of callers.
