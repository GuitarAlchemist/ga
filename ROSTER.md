> Roster content lives here, not in AGENTS.md (which auto-mirrors CLAUDE.md for codex parity).

# ROSTER.md — Specialized agent roster for ga (C#)

This file is the per-agent contract for the ga repository. Project conventions,
build commands, and collaboration discipline live in `CLAUDE.md` — do NOT
duplicate them here. `AGENTS.md` is auto-synced from `CLAUDE.md` by
`Scripts/sync-agents-md.ps1` (called from `.githooks/pre-commit`) so codex /
OpenAI tooling and Claude read identical instructions; this roster is the
ga-specific overlay.

## Confidence thresholds (Demerzel-aligned, apply to every agent)

| Confidence | Action |
|---|---|
| ≥ 0.90 | Autonomous action |
| ≥ 0.70 | Proceed with note |
| ≥ 0.50 | Confirm before acting |
| ≥ 0.30 | Escalate to human |
| < 0.30 | Do not act |

These thresholds match the hexavalent certainty bands in
`ix/docs/contracts/2026-05-24-ai-annotation.contract.md`. When an agent attaches
an `@ai:` annotation with `conf:<num>`, the number determines the band and the
band determines whether the agent may act, must confirm, or must escalate.

## Roster

### chatbot

**Role:** SSE chat ingress + skill orchestration for guitaralchemist.com. Hosts
the `/api/chatbot` SSE endpoint (`Apps/ga-server/GaApi/Controllers/ChatbotController.cs`),
the Blazor-side experience (`Apps/GaChatbot/`), the API layer
(`Apps/GaChatbot.Api/`), and the CLI driver (`Apps/GaChatbotCli/`). Fans out to
ML skills under `Common/GA.Business.ML/Agents/Skills/` (top traffic:
`ChordVoicingsSkill`), which call into the OPTIC-K index reader
(`Common/GA.Business.ML/Search/OptickIndexReader.cs`).

**Boundaries:**
- MAY modify: chatbot Apps, ML skills under `Common/GA.Business.ML/Agents/`,
  prompt templates, SSE response shaping.
- MUST NOT modify: OPTIC-K dimension or partition layout (one-way door — see
  `CLAUDE.md` § OPTIC-K), lower layers (Core / Domain / Analysis).
- MUST honour: hexavalent confidence thresholds above for any action that
  mutates user-visible behaviour.

**Verified by:** `Tests/` chatbot suites; `state/quality/chatbot-qa/` daily
snapshots; `Apps/GaChatbot/groundedness_bench.jsonl`.

### qa-architect

**Role:** Produces QA verdicts consumed by the Demerzel tribunal. Surface is
`Apps/GaQaMcp/` — an MCP server exposing QA tools. Emits artifacts conforming
to `docs/contracts/2026-05-02-qa-verdict.contract.md` (schema:
`docs/contracts/qa-verdict.schema.json`); Demerzel's IXQL pipelines orchestrate
the tribunal that aggregates these verdicts.

**Boundaries:**
- MAY modify: `Apps/GaQaMcp/Tools/`, verdict-shaping logic, schema-conformant
  artifact emission.
- MUST NOT modify: `qa-verdict.schema.json` locked fields without cross-repo
  coordination with Demerzel.
- MUST honour: confidence thresholds above; a < 0.50 verdict requires confirm
  rather than auto-publish.

**Verified by:** schema-validation tests against `qa-verdict.schema.json`;
`state/quality/` artifacts produced on each cycle.

### sentinel

**Role:** *Aspirational — no in-repo ga implementation yet.* The Sentinel
persona is defined Demerzel-side (governance constitutions); ga's intended
counterpart would be a structural-quality monitor in the spirit of sentrux,
running over `state/quality/` snapshots and emitting algedonic signals per
`docs/contracts/2026-05-24-algedonic-channel.contract.md` (schema:
`docs/contracts/algedonic-signal.schema.json`). Until built, the closest
proxies in ga are the `state/quality/` daily snapshots and the
`ViolationMonitor`-style checks scattered in the orchestration layer.

**What would need to be built:**
- An `Apps/GaSentinel/` (or equivalent) service.
- Subscriber to `state/quality/` filesystem events.
- Algedonic-channel emitter (pain / pleasure signals) per the contract above.

**Confidence thresholds:** when implemented, same Demerzel-aligned bands above.

## Cross-repo contracts ga consumes / produces

| Contract | Direction | Path |
|---|---|---|
| ai-annotation v2 (hexavalent `@ai:` syntax) | consume (C# code writes annotations using this) | `../ix/docs/contracts/2026-05-24-ai-annotation.contract.md` |
| optick-sae artifact | consume | `docs/contracts/2026-05-02-optick-sae-artifact.contract.md` |
| optick-weights config | consume | `docs/contracts/2026-04-27-optick-weights-config.contract.md` |
| QA verdict | produce | `docs/contracts/2026-05-02-qa-verdict.contract.md` |
| algedonic-channel | produce (when sentinel exists) | `docs/contracts/2026-05-24-algedonic-channel.contract.md` |
| ga-dsl-eval | produce | `docs/contracts/2026-05-06-ga-dsl-eval-contract.md` |
| ga-loop-driver | produce | `docs/contracts/2026-05-10-ga-loop-driver.contract.md` |
| overseer-halt-marker | produce | `docs/contracts/2026-05-16-overseer-halt-marker.contract.md` |

## Hexavalent invariant comment syntax (REQUIRED in C# code)

When making a claim about code behaviour, attach an `@ai:` annotation:

```csharp
// @ai:invariant arr is sorted ascending [T:test conf:0.95 src:tests/SearchTests.cs:42]
public static int BinarySearch(int[] arr, int target) { ... }
```

**Hexavalent truth values:** `T` (True), `P` (Probable), `U` (Unknown),
`D` (Doubtful), `F` (False), `C` (Contradictory).

**Certainty markers:** `test` | `formal-proof` | `manually-reviewed` |
`assumed` | `uncertain` | `inferred` | `dismissed` | `detected-by-sentrux`.

**Confidence:** 0.0–1.0; band determines agent action per the thresholds above.

**Kinds:** `invariant`, `assumption`, `hypothesis`, `contract`, `smell`,
`decision`, `hint`, `business-value`, `hot-path`.

Full schema: `../ix/docs/contracts/ai-annotation.schema.json`.

## Appendix — audit of existing `@ai:` annotations in C# (snapshot 2026-05-24)

**Adoption status:** 7 annotations found across 7 files. All 7 use kind
`business-value`. Zero `invariant`, `assumption`, `hypothesis`, `contract`,
`smell`, `decision`, `hint`, or `hot-path` annotations exist in C# source. All
7 annotations are syntactically valid (truth value `T`, certainty
`manually-reviewed`, confidence 0.90–0.95, `src:product-owner@2026-05-24`).

**Valid (7 / 7):**

| File | Line | Kind | Conf |
|---|---|---|---|
| `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` | 26 | business-value | 0.95 |
| `Apps/ga-server/GaApi/Controllers/VoicingsController.cs` | 14 | business-value | 0.90 |
| `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs` | 21 | business-value | 0.90 |
| `Common/GA.Business.ML/Agents/Skills/ChordVoicingsSkill.cs` | 14 | business-value | 0.92 |
| `Common/GA.Business.ML/Search/OptickIndexReader.cs` | 21 | business-value | 0.95 |
| `Common/GA.Domain.Core/Theory/Harmony/Chord.cs` | 18 | business-value | 0.90 |
| `Common/GA.Domain.Services/Chords/CanonicalChordRecognizer.cs` | 17 | business-value | 0.95 |

**Malformed (0 / 7):** none.

**Coverage gap (not fixed in this PR — audit only):** schema adoption is narrow
— one kind out of nine. High-value follow-up candidates:

- `@ai:invariant` on `EmbeddingSchema.TotalDimension == 240` and the ROOT
  partition slot range (228–239) — these are one-way doors per `CLAUDE.md`.
- `@ai:invariant` on `CanonicalChordRecognizer` (PC-set-only ranking; bass
  lives in `SlashSuffix`) — exactly the invariant memorialised in
  `feedback_recognizer_bass_not_in_ranking.md`.
- `@ai:contract` on the producers of `state/quality/optick-sae/<date>/optick-sae-artifact.json`
  to lock the schema reference at the call site.

These are audit findings, not changes — fixing them is a separate PR.
