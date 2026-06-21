# GA → TARS Claim — Cross-Repo Contract

**Version:** 0.1.0 (draft, pending sign-off)
**Schema version:** 1
**Status:** Draft (design grilled 2026-06-20; tracer-bullet predicate = `pitch_classes`). NOT frozen. **Keystone corrected 2026-06-20 — see banner below.**
**Producers:** GA chatbot retrieval/answer path — `Common/GA.Business.ML/Agents/*` (claim extraction + **GA-side contradiction detection**)
**Consumers:** GA claim consistency checker (detection + `state/quality/consistency/` output). **Optional** downstream: TARS as a contradiction *ledger* (`../tars/`).

> **⚠ Keystone correction (2026-06-20):** Reading the TARS source showed `graph_find_contradictions` /
> `temporal_detect_contradictions` only **return pre-asserted `ContradictedBy` edges** — TARS *stores*
> contradictions, it does **not detect** them from content (ADR-0003 § Correction). So **GA detects** the
> same-`key`-different-`asserted_value` contradiction (a trivial dictionary compare) and emits it to the
> quality surface; TARS is an **optional** sink GA asserts edges into *after* detecting. This contract's
> claim shape is unchanged and still correct — only the consumer role moved from "detector" to "ledger".
**Companion artifacts:** `docs/adr/0003-tars-validates-consistency-not-truth.md`; output envelope `state/quality/consistency/<date>.json` (per `docs/contracts/quality-snapshot.schema.json`)
**Schema file:** [`ga-tars-claim.schema.json`](./ga-tars-claim.schema.json)

---

## 1. Why This Contract Exists

TARS is GA's **longitudinal self-consistency oracle** (ADR-0003): it answers "does the chatbot contradict
itself over time?", not "is this claim correct?". For that to be *sound* rather than a false-positive
swamp, a claim must travel from GA to TARS as a **typed fact** — a canonical key plus the value the
chatbot actually asserted — not as free prose. This contract pins that shape so GA can write claims in
C#/F# and TARS can ingest them with no bespoke glue, and so the predicate vocabulary can grow additively
without breaking either side.

The asserted value is stored **verbatim and is never compared to GA's ground truth** — comparing it to a
"right answer" would turn the oracle into a truth validator, which ADR-0003 explicitly rejects.
Contradiction = two claims with the **same `key`** but **different `asserted_value`** (cross-session), or
a claim whose `asserted_value` differs from its own `trace_value` (intra-response).

## 2. JSON Shape

One JSON object per asserted fact. GA appends these to a per-day JSONL sink
(`state/telemetry/claims/<yyyy-MM-dd>.jsonl`, gitignored); TARS ingests the batch offline.

```json
{
  "schema_version": 1,
  "ts": "2026-06-20T18:30:00Z",
  "session_id": "chatbot-7f3a…",
  "source": "chatbot",
  "predicate": "pitch_classes",
  "subject": "Cmaj7",
  "key": "pitch_classes:Cmaj7",
  "asserted_value": [0, 4, 7, 11],
  "trace_value": [0, 4, 7, 11],
  "prose_span": "Cmaj7 is spelled C, E, G, and B",
  "agent_id": "voicing",
  "model": "qwen2.5:7b"
}
```

### Field semantics

| Field | Req | Meaning |
|---|---|---|
| `schema_version` | ✓ | Contract schema version. Unknown → consumer halts (fail-closed). |
| `ts` | ✓ | ISO-8601 UTC of the response that made the claim. |
| `session_id` | ✓ | Conversation id — scopes "same session" vs "cross-session". |
| `source` | ✓ | `"chatbot"` \| `"mcp"` — which retrieval surface emitted it. |
| `predicate` | ✓ | A registered functional predicate (see §3). Determines `asserted_value` type + how contradiction is computed. |
| `subject` | ✓ | The entity the claim is about, as the chatbot named it (e.g. `"Cmaj7"`, `"Dorian"`). |
| `key` | ✓ | **GA-canonicalized** `predicate:subject` — the contradiction-grouping key. GA folds `"Cmaj7"` and `"C major 7"` to one key via its domain core. This is the ONLY place GA's domain core is consulted. |
| `asserted_value` | ✓ | What the chatbot's **prose** claimed, parsed by GA's recognizers, stored **verbatim** (no ground-truth correction). Type per predicate. |
| `trace_value` | — | The value the agent's **own tool call** computed in the same turn, when present. Enables the intra-response check. Absent for no-tool (pure-LLM) answers. |
| `prose_span` | — | The source sentence — for triage/audit of a flagged contradiction. |
| `agent_id` | — | Which agent answered (router target). |
| `model` | — | LLM that produced the prose — lets contradiction reports attribute drift to a model. |

## 3. Predicate Registry (v1)

Only **functional** (single-valued) predicates GA can canonicalize a key for are consistency-checkable.
Adding a predicate is an **additive, non-breaking** change (no `schema_version` bump).

| `predicate` | `subject` | `asserted_value` type | Canonical key via | Contradiction rule |
|---|---|---|---|---|
| `pitch_classes` | chord symbol | `int[]` (sorted PC set 0–11) | `PitchClassSet` / `ga_chord_to_set` | same `key`, different set |

**v1 ships `pitch_classes` only** — the tracer bullet (ADR-0003). Validate the end-to-end loop by
replaying buggy-era #414 traces and confirming TARS flags the contradiction the fix resolved, *before*
adding `prime_form`, `mode_degree`, `chord_intervals`, … (each a separate row + recognizer).

## 4. One-Way-Door Notes

- `schema_version`, the field names, and the predicate-`asserted_value` type mapping are encoded by TARS.
  Renaming a field or changing a predicate's value type needs a coordinated `schema_version: 2` bump.
- The **predicate set is open** (additive). New predicates are draft until each has a GA recogniser, a
  canonical key function, and a replay-validated positive.
- v0.1.x is a **draft** — do not freeze until a named Phase milestone, per the GA cross-repo convention.
