# Plan: `ix.duckdb_extension` — a `LOAD`-able DuckDB extension for IX UDFs

**Date:** 2026-06-15
**Type:** tools / cross-repo (impl lands in **ix**, not ga)
**Status:** v0.1 SHIPPED 2026-06-15 — scalar + table functions loadable & tested. **ix PR #106** (`feat/ix-duck-loadable-extension`).
**Owner repo:** `../ix` (`crates/ix-duck`)
**Builds on:** `ix/docs/plans/2026-06-14-001-feat-ix-duck-duckdb-udfs-plan.md` (the in-process UDFs this packages)

---

## 1. Problem / who is in pain

Today IX's DuckDB UDFs (`ix_cosine`, `ix_euclidean`) and GA's domain functions
(`ga_scale_notes`) only exist **in-process** — they're registered by the host
binary (the Rust `ix-duck` bench, the C# `GaDuckLens` tool) and vanish when that
process exits. You **cannot** open a plain `duckdb.exe` and `LOAD` them.

Anyone wanting `ix_cosine(a, b)` from a bare CLI session, a notebook's DuckDB
connection, or another tool's embedded DuckDB has no path. The ask: make at least
the numeric IX UDFs loadable into any `duckdb.exe`.

**What changes for them:** `SET allow_unsigned_extensions=true; LOAD 'ix.duckdb_extension';`
then `SELECT ix_euclidean(q, r) ... ORDER BY 1 LIMIT k` works in any matching CLI.

## 2. Scope decision (what's in, what's deliberately out)

| Candidate | In scope? | Rationale |
|---|---|---|
| `ix_cosine`, `ix_euclidean` | ✅ **Yes** | Pure numeric Rust (`ix_math::distance`); the `VScalar` impls + `register_all(&conn)` in `ix-duck/src/udf.rs` are already the exact shape a loadable entrypoint needs. Zero domain duplication. |
| `ix_pca_project`, `ix_silhouette` (table fns) | ⏸ Deferred | Already deferred to the follow-up in the 06-14 plan; table-function ABI is a separate surface. Add once scalar path proves out. |
| `ga_scale_notes` (GaDuckLens) | ❌ **No** | It's **C# domain logic** (`Key`, enharmonic spelling). A Rust/C extension can't call it without reimplementing music theory in Rust (breaks single-source-of-truth) or Native-AOT C# + C glue (worst toolchain). The `GaDuckLens --appender` **snapshot** already lets a bare CLI read domain *data* with no extension — keep that for data; an extension only adds live *computation*, which the domain side doesn't need. |

**Net deliverable:** one signed-as-unsigned `ix.duckdb_extension` exporting
`ix_cosine` + `ix_euclidean`, built against DuckDB **1.5.3** (the version both
the CLI and `ix-duck`'s crate currently target).

## 3. Hard constraints (the two things that break loadable extensions)

> **Phase-0 spike result (2026-06-15): both constraints RESOLVED — see §5.**

1. **Version (softened — NOT a hard lock on the C-API path).** The spike confirmed
   `loadable-extension` builds a **C-API (`C_STRUCT` ABI)** extension that binds
   DuckDB function pointers at load time and declares a `min_duckdb_version`
   instead of linking a specific engine. The footer encodes the **C API version**
   (e.g. `v1.0.0`), not the build engine — so one artifact loads across engine
   versions ≥ its declared min. The spike's `v1.0.0`-tagged build loaded cleanly
   into the **v1.5.3** CLI. Net: pin the *crate* to a tested release, but the
   shipped artifact is engine-version-tolerant. (A classic C++ extension would be
   hard-locked; we are deliberately NOT taking that path.)
2. **Metadata footer + unsigned flag (resolved).** A raw `cdylib` will **not**
   `LOAD` ("metadata at the end of the file is invalid"). DuckDB's
   `append_extension_metadata.py` appends a 512-byte footer
   (`abi_type=C_STRUCT`, C-API version, `windows_amd64`, ext version, zero
   signature). Launch with `duckdb -unsigned` (or `SET allow_unsigned_extensions`
   at startup). Spike proved the exact recipe (§5, Phase 0).

## 4. Technical approach

`duckdb-rs` supports loadable extensions via the `loadable-extension` feature +
the `#[duckdb_entrypoint_c_api]` proc-macro, producing a `cdylib`. The friction:
the **same crate can't be both** `bundled` (in-process bench, links DuckDB
statically) **and** a loadable cdylib (links the host's C API at load time). So:

**New sibling crate `ix-duck-ext`** (`crate-type = ["cdylib"]`):
- depends on `duckdb` with `["loadable-extension", "vscalar"]` (NOT `bundled`)
- reuses the metric logic by depending on the shared bits of `ix-duck`
  (factor the `VScalar` impls + `register_all` out of the `duck`-feature-gated
  module so both the bench and the extension import them; the impls only need
  `vscalar`, not `bundled`).
- entrypoint:
  ```rust
  #[duckdb_entrypoint_c_api]
  pub unsafe fn ix_duck_init(conn: Connection) -> Result<(), Box<dyn Error>> {
      ix_duck::udf::register_all(&conn)?;   // same registration the bench uses
      Ok(())
  }
  ```
- build emits `libix_duck_ext.{dll,so,dylib}`; the metadata-append step renames/
  rewrites it to `ix.duckdb_extension`.

**Refactor needed in `ix-duck`:** split `udf.rs` so `register_all` + the
`VScalar` types compile under a `vscalar`-only feature (no `bundled`,
no `ix-unsupervised`/`ndarray`-heavy deps beyond what the metrics need). The
metrics already only touch `ix_math` + `ndarray` — low risk.

## 5. Phases (reversibility-aware)

### Phase 0 — Spike (de-risk the footer + LOAD). ✅ **DONE 2026-06-15 — GO.**
Built a throwaway cdylib (`C:\tmp\ix-duck-spike`, table function
`ix_spike_hello`, verbatim from the duckdb crate's `hello-ext`), appended the
footer, and proved `LOAD` + `SELECT` round-trips in the **v1.5.3** CLI. Findings:
- `loadable-extension` does **not** pull `bundled` → **no DuckDB C++ compile**;
  release build was **53s** (Rust + arrow only).
- C-API extension via `#[duckdb_entrypoint_c_api(ext_name="ix_spike", min_duckdb_version="v1.0.0")]`.
- Raw cdylib LOAD fails with "metadata at the end of the file is invalid" — footer required.

**Exact working recipe (reuse in Phase 3):**
```bash
cargo build --release                     # emits target/release/<crate>.dll
PLATFORM=$(duckdb -noheader -list -c "PRAGMA platform;")   # -> windows_amd64
python append_extension_metadata.py \
  -l target/release/<crate>.dll -n <ext_name> \
  -dv v1.0.0 -p "$PLATFORM" -ev <ext_ver> --abi-type C_STRUCT \
  -o <ext_name>.duckdb_extension
duckdb -unsigned -c "LOAD '<abs>/<ext_name>.duckdb_extension'; SELECT ...;"
```
Result: `SELECT * FROM ix_spike_hello('IX')` → `Hello IX`. **GO to Phase 1.**

### Phase 1 — Refactor `ix-duck` for shared UDF reuse. ✅ **DONE 2026-06-15.**
Split `ix-duck`'s features so the UDFs compile without `bundled`:
- `duckdb` dep → `default-features=false`; new `udf` feature = `["dep:duckdb",
  "duckdb/vscalar", "dep:ix-math", "dep:ndarray"]` (vscalar implies vtab; **no
  bundled** — `cargo check --features udf` = **8.7s, no C++ build**).
- `duck` = `["udf", "duckdb/bundled", "duckdb/json", ...bench deps]`.
- `mod udf` → `pub mod udf` gated on `feature="udf"`.
Regression: `cargo test -p ix-duck --features duck` → **12 tests + doc-test pass**,
behavior unchanged.

### Phase 2 — `ix-duck-ext` crate + entrypoint. ✅ **DONE 2026-06-15.**
New `crates/ix-duck-ext` (cdylib), `#[duckdb_entrypoint_c_api(ext_name="ix",
min_duckdb_version="v1.0.0")]` → `ix_duck::udf::register_all(&con)`. **Excluded**
from the workspace (root `Cargo.toml` `exclude=[...]`) so `cargo build --workspace`
never pulls DuckDB — preserves the ix build invariant. Confirmed `vscalar` compiles
under `loadable-extension` (release build **45s**, no C++).

### Phase 3 — Build target + metadata append + smoke test. ✅ **DONE 2026-06-15.**
`crates/ix-duck-ext/build.ps1`: `cargo build --release` → resolve platform from
`PRAGMA platform` → `append_extension_metadata.py` (vendored from
`duckdb/extension-ci-tools`, MIT) → emits `ix.duckdb_extension`. `-SmokeTest`
`LOAD`s into `duckdb.exe` and asserts `ix_cosine`/`ix_euclidean` = `1.0|0.0|5.0`
— **PASS**. (CI wiring as an opt-in job: not yet done — local script is the gate.)

### Phase 1b — Table functions (`ix_pca_project`, `ix_silhouette`). ✅ **DONE 2026-06-15.**
Implemented in `ix-duck/src/tablefn.rs` (shared by bench + extension):
- `ix_pca_project(json_vectors VARCHAR, n_components BIGINT) → TABLE(row BIGINT,
  coords DOUBLE[])` — wraps `ix_unsupervised::pca::PCA`; LIST<DOUBLE> output via
  `ListVector::child`/`set_entry`, cursor-chunked across output vectors.
- `ix_silhouette(json_vectors VARCHAR, json_labels VARCHAR) → TABLE(row BIGINT,
  label BIGINT, silhouette DOUBLE)` — silhouette implemented from scratch (it did
  not exist in ix) over `ix_math::distance::euclidean`; `s=0` for singleton/sole
  cluster. Mean = `AVG(silhouette)`.
- `udf` feature gained `ix-unsupervised` + `serde_json` (still no bundled engine).
- **16 bench tests pass** (+4 new). Loadable smoke test now asserts both surfaces:
  scalars `1.0|0.0|5.0`, table fns `4|1.0` — **PASS** in `duckdb.exe`.

### Phase 4 — Docs.
`ix/docs/DUCKDB.md` (or extend it): the `-unsigned` launch flag, the version-pin
rule, where the artifact lands, the `LOAD '<abs-path>'` one-liner. Note the GA
side (`GaDuckLens --appender` for domain *data*) so the split is discoverable.

## 6. Success criteria (mechanizable)

```
duckdb -unsigned -c "LOAD '<path>/ix.duckdb_extension';
  SELECT ix_cosine([1.0,0.0],[1.0,0.0]) AS cos,
         ix_euclidean([0.0,0.0],[3.0,4.0]) AS l2;"
# expect:  cos = 1.0   l2 = 5.0
```
- `ix-duck` `duck` bench + its UDF tests still green (no regression).
- ix default `cargo build --workspace` unchanged (extension is opt-in).
- README documents version-pin + unsigned flag.

## 7. One-way doors / revisit triggers (rule 6)

- **Version pin (1.5.3).** Per-build one-way door. Revisit trigger: any
  `duckdb.exe` or `ix-duck` crate version bump → rebuild + re-pin, or migrate the
  entrypoint to the ABI-stable **C Extension API** so one artifact spans versions.
- **New crate `ix-duck-ext`.** Two-way door (deletable; off the default build).
- **Signing.** Staying unsigned = local/dev only. If this ever needs frictionless
  `INSTALL ix; LOAD ix;` from a repo, that's the DuckDB community-extension
  signing process — a separate, larger decision. Out of scope here.

## 8. Effort

~half a day: Phase 0 spike (2h, gates the rest) + ~3h for refactor/crate/CI/docs
if the spike is green. Cross-repo touch is **ix-only**; GA unaffected except this
plan doc + a one-line pointer from GA's DuckDB docs.

## 9. Open questions for sign-off

1. **Repo for the plan of record:** keep this in GA `docs/plans/` (where you
   plan), or mirror into `ix/docs/plans/` since impl lands there? (Default: mirror
   a copy into ix at Phase 1 so ix CI/review sees it.)
2. **Build orchestration:** `xtask` vs `cargo make` vs a `Scripts/*.ps1` in ix —
   which matches ix's existing convention? (Spike will reveal what `ix-duck`
   already uses.)
3. Ship `ix_pca_project` / `ix_silhouette` in the same extension once scalar
   proves out, or keep the first artifact scalar-only? (Default: scalar-only v0.1.)
