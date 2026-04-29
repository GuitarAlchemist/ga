# OPTIC-K Weights Config — Cross-Repo Contract

**Version:** 1.0.0
**Schema version:** 1
**Status:** Active (Phase 6 of `ix-autoresearch` plan, 2026-04-27)
**Producer:** `ix-autoresearch` Target A driver (writes JSON, invokes `FretboardVoicingsCLI`)
**Consumer:** `FretboardVoicingsCLI --weights-config <path>` (reads JSON, applies override at index-rebuild time)
**Companion class:** `Demos/Music Theory/FretboardVoicingsCLI/WeightsOverride.cs`
**Companion adapter:** `ix/crates/ix-autoresearch/src/target_optick.rs`

---

## 1. Why This Contract Exists

`ix-autoresearch` runs an edit-eval-iterate loop over the OPTIC-K v4 partition similarity weights. Each iteration proposes a new weight vector on the simplex; GA's CLI rebuilds the index with that vector; IX scores the resulting index for retrieval-vs-leak trade-off and feeds the score back to the search strategy (Greedy / SimulatedAnnealing).

The two repos communicate through one file: a JSON document matching the shape below. This contract pins the shape so a one-line typo on either side cannot silently corrupt a 30-iteration overnight run.

---

## 2. JSON Shape

All keys are **optional**. Missing keys keep their `EmbeddingSchema.SimilarityPartitions` default. Explicit zeros exclude that partition from similarity (mirrors the schema's `weight = 0 ⇒ excluded` convention).

```json
{
  "schema_version":   1,
  "structure_weight": 0.45,
  "morphology_weight": 0.25,
  "context_weight":   0.20,
  "symbolic_weight":  0.10,
  "modal_weight":     0.10,
  "root_weight":      0.05
}
```

**Key naming:** snake_case in JSON. Maps to UPPERCASE partition names (`STRUCTURE`, `MORPHOLOGY`, `CONTEXT`, `SYMBOLIC`, `MODAL`, `ROOT`) used by `EmbeddingPartition.Name`.

**Encoding:** UTF-8 without BOM. Numbers parsed via `JsonElement.GetSingle()` (accepts `0.45` and `1` indistinguishably).

---

## 3. Validation Rules

A weights-config is **rejected** (CLI exits non-zero, no index produced) if any of:

| Rule | Diagnostic |
|------|-----------|
| Root is not a JSON object | `weights-config root must be a JSON object; got Array` |
| Any present weight is non-number | `weights-config: '<key>' must be a number; got String` |
| Any weight is `NaN`, `+Inf`, `-Inf` | `weights-config: '<key>' must be finite; got NaN` |
| Any weight is negative | `weights-config: '<key>' must be ≥ 0; got -0.5` |
| `schema_version` > current supported (1) | `weights-config schema_version 99 > supported 1` |
| File cannot be read | `--weights-config: cannot read '<path>': ...` |

**Note on simplex normalization.** This contract does **not** require weights to sum to 1.0. The IX-side Dirichlet sampler produces simplex-valid vectors, but the contract accepts any non-negative finite vector — `OptickIndexWriter` normalizes per partition independently. Future versions MAY add a `normalize: "simplex" | "raw"` knob; v1 is `raw`.

---

## 4. Lifecycle and Reversibility

- **CLI flag (`--weights-config <path>`)**: **two-way door**. The flag is purely additive. Without it, GA's CLI uses `EmbeddingSchema.SimilarityPartitions` defaults — identical to pre-Phase-6 behaviour. Removing the flag is non-breaking for any existing caller.

- **Partition layout (the six keys above)**: **one-way door**. Adding, removing, or renaming a partition affects:
  - `OptickIndexWriter` (GA, .NET) — partition list and weights
  - `WeightsOverride.JsonKeyToPartition` (GA, .NET) — JSON key map
  - `target_optick::OpticKConfig` (IX, Rust) — fixed-size `[f64; N]` array, Dirichlet `<f64, N>` const generic
  - All previously written `optick.index` files (GA) — embedded partition layout, schema hash
  - The `optk-v4-pp-r` schema version itself (corpus rebuild required)

  **Process:** ratify in this contract document first. Bump `SchemaVersion` to 2. Update both repos in lockstep. Document the migration in `state/voicings/` (GA) and `docs/plans/` (IX).

- **`schema_version`**: forward-compatible read protocol. v1 readers reject `schema_version > 1`; v2+ readers MUST accept v1 inputs.

---

## 5. Producer Contract (IX side)

`ix-autoresearch`'s Target A driver writes the JSON to a temp file before invoking the CLI. Required guarantees:

1. The producer has already validated that the proposed config is on the simplex (`Σ wᵢ ≈ 1.0`, all `wᵢ ≥ ε` where `ε = 1e-3` is the floor against absorbing states).
2. The temp file is written atomically (write + sync + rename) so a partial write cannot race the CLI's read.
3. The temp file lives outside any indexed corpus directory; the CLI reads it once and never writes back.
4. The producer logs the config hash (blake3 of canonical JSON) in its own `Iteration` log event before invocation. This is what `ix-cache` keys on for memoization.

---

## 6. Consumer Contract (GA side)

`FretboardVoicingsCLI`'s `--weights-config` flag:

1. Reads the file synchronously at startup (before any voicing generation).
2. Validates per Section 3. **On any failure: print diagnostic to stderr, exit non-zero, write nothing.**
3. On success: prints `weights: OVERRIDE applied — WeightsOverride{...}` to stderr (per-partition values for audit), then proceeds with the rebuild using `OptickIndexWriter(outputPath, weightsOverride)`.
4. Default-weight partitions (those not present in the JSON) keep their `EmbeddingSchema.SimilarityPartitions` value — they are NOT zeroed.

The CLI MUST NOT mutate the override file. The CLI MUST NOT write the resolved weight vector anywhere except into the index header (where it has always lived).

---

## 7. Verification

End-to-end smoke tests covering this contract live in:

- IX side: `crates/ix-autoresearch/tests/optick_mocked.rs` — Dirichlet simplex invariants, lex-order reward, eval-inputs hash.
- GA side: integration matrix run during Phase 6 ratification (2026-04-27):
  - happy path with explicit 6-key override
  - empty `{}` (valid no-op)
  - negative weight → reject
  - NaN / non-number → reject
  - array root → reject
  - `schema_version: 99` → reject
  - missing file → reject

Re-run the GA matrix whenever this contract is modified.

---

## 8. Open Questions (Defer to v2)

- Should `normalize: "simplex"` be a first-class knob, or remain implicit per-partition?
- Should the override carry a `provenance` field (UUIDv7 of the IX run that generated it) so the resulting `optick.index` can be traced back to the autoresearch iteration?
- Should a missing partition explicitly *zero* it out instead of falling back to default? (Currently: missing = default. Explicit zero = excluded. The asymmetry was a deliberate v1 choice for backwards compatibility.)

These are **two-way door** decisions and can be added in a future schema version without breaking v1 consumers.
