# IX ⟷ GA set-theory cross-check

Verifies GA's C# set-theory engine against IX's **independent** Rust
implementation (`ix-bracelet`), via the DuckDB UDFs `ix_icv` / `ix_prime_form`
/ `ix_forte_number`. Two independent implementations agreeing on the same
invariants is a far stronger correctness signal than either self-checking —
the cross-repo counterpart to PR #414's GA-internal `CanonicalForteCatalogTests`.

## Why

The **interval-class vector is convention-free** (it is THE canonical invariant
of a set class, identical under any prime-form ordering), so `ix_icv` MUST equal
GA's ICV for every set class. Any mismatch is a real bug in one engine. Prime
form and Forte label are convention-**dependent** (Rahn vs Forte 1973 vs
bracelet rotation), so those comparisons are informational — they map the
convention gap, not bugs.

## Run

```powershell
# Needs duckdb on PATH + ix.duckdb_extension built WITH the bracelet UDFs.
pwsh Scripts/ix-ga-settheory-crosscheck.ps1

# Reuse the last GA corpus (skip re-emitting):
pwsh Scripts/ix-ga-settheory-crosscheck.ps1 -SkipEmit
```

**Prereq — the extension must include the set-theory UDFs.** They live in the
`ix-duck` crate (`bracelet.rs`) and are wired into the loadable extension via
`ix_duck::udf::register_all`. If `ix_icv` reports "does not exist", the built
artifact is stale — rebuild it:

```powershell
pwsh ../ix/crates/ix-duck-ext/build.ps1
```

## Pipeline

1. `SetTheoryCrossCheckCorpusEmitter.EmitCorpus` ([Explicit] test) dumps, for
   every GA `SetClass` (cardinality 1–11), its prime form + interval-class
   vector → `state/quality/ga-ix-settheory/ga-settheory-<date>.json`.
2. `Scripts/ix-ga-settheory-crosscheck.sql` loads the IX extension and runs
   `ix_icv` / `ix_prime_form` / `ix_forte_number` over the same prime forms,
   comparing ICV numerically (gold) and prime-form/Forte as informational.
3. The runner writes a markdown report + JSON sidecars; ICV disagreements (if
   any) are surfaced loudly. Oracle-paranoia: a missing extension / duckdb
   error / absent output all exit non-zero — never read as "clean".

## Result (2026-06-16, 222 set classes)

- **ICV: 222/222 agree (100%)** — GA and IX compute the same interval-class
  vector for every set class.
- **Prime form: 181/222 agree (81.5%)** — the 41 differences are IX returning
  *bracelet rotation* representatives (e.g. `[0,1,3,10]`) where GA returns
  musical prime form (`[0,2,3,5]`); same set class (same ICV + Forte label),
  different chosen representative. Convention, not bugs.
- `ix_forte_number` classified all 222 (0 unclassified).

## Follow-up

The ICV agreement is the load-bearing check. A natural extension once PR #414's
`CanonicalForteCatalog` is on main: compare `ix_forte_number` directly against
GA's **canonical** Forte labels (not just GA's Rahn `ProgrammaticForteCatalog`),
closing the loop on the Forte-numbering convention across both repos.
