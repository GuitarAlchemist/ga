# Chatbot SKILL.md Migration — Completion Report & Next-Session Handoff

**Status**: Complete (2026-05-03). All 10 `IOrchestratorSkill` implementations have SKILL.md equivalents.
**Spec**: `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md` (Phase 2, plus the bonus MCP-tool-exposure workstream).
**Reversibility**: Each PR was a two-way door. C# `IOrchestratorSkill` implementations all stay in place as deterministic fast paths.

## What landed

13 PRs merged to `main` on 2026-05-03:

| PR | Type | Topic |
|---|---|---|
| #72 | Infra | Semantic router diagnostics + tie-break + embedding timeout |
| #74 | Catalog | beginner-chords + canonical `skills/` resolver |
| #75 | Catalog | progression-mood + porting policy locked |
| #76 | MCP tool | `ga_interval_compute` |
| #78 | MCP tool | `ga_scale_get_notes` + `McpEchoSanitizer` extracted |
| #79 | Hardening | Tool-name uniqueness, body cap, min trigger length |
| #80 | MCP tool | `ga_chord_info` |
| #81 | Catalog | modes (re-classified from computation) |
| #83 | MCP tool | `ga_fret_span` |
| #85 | MCP tool | `ga_chord_substitutions` + `ga_chord_compare` |
| #88 | MCP tool | `ga_key_identify` (first hybrid port) |
| #89 | Hybrid reuse | progression-completion → reuses `ga_key_identify` |

## Migration map (final state)

```
skills/
├── qa-architect/              SKILL.md   (catalog — instructions for QA Architect agent)
├── beginner-chords/           SKILL.md   (catalog — 8 open-position chord diagrams)
├── progression-mood/          SKILL.md   (catalog — darken/brighten technique catalog)
├── modes/                     SKILL.md   (catalog — 7-mode major-scale catalog)
├── interval/                  SKILL.md   (tool-driven — calls ga_interval_compute)
├── scale-info/                SKILL.md   (tool-driven — calls ga_scale_get_notes)
├── chord-info/                SKILL.md   (tool-driven — calls ga_chord_info)
├── fret-span/                 SKILL.md   (tool-driven — calls ga_fret_span)
├── chord-substitution/        SKILL.md   (tool-driven — calls ga_chord_substitutions / ga_chord_compare)
├── key-identification/        SKILL.md   (tool-driven, hybrid — calls ga_key_identify)
└── progression-completion/    SKILL.md   (tool-driven, hybrid — REUSES ga_key_identify)

Common/GA.Business.ML/Agents/Mcp/
├── McpEchoSanitizer.cs        (shared — 16-char clamp + control-char strip)
├── IntervalMcpTools.cs        (1 method — ga_interval_compute)
├── ScaleMcpTools.cs           (1 method — ga_scale_get_notes)
├── ChordMcpTools.cs           (1 method — ga_chord_info)
├── FretSpanMcpTools.cs        (1 method — ga_fret_span)
├── ChordSubstitutionMcpTools.cs  (2 methods — ga_chord_substitutions, ga_chord_compare)
└── KeyIdentificationMcpTools.cs  (1 method — ga_key_identify)

Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs
└── McpToolTypes registers all 6 tool classes
```

## Patterns established (one-way doors)

### Catalog vs computation policy (PR #75)

Codified in `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md` §"Porting policy: catalog vs. computation skills":

- **Catalog skills** (pure data, no domain types touched) → SKILL.md only. Body carries the data.
- **Computation skills** (domain types, regex parsing, pitch-class arithmetic) → MCP tool exposes the deterministic operation; thin SKILL.md instructs the LLM to call it. Never let the LLM recall computed answers from training data.
- **Hybrid skills** (deterministic detection + LLM phrasing) → split: detection becomes the MCP tool, phrasing rules move to the SKILL.md body.

### MCP tool naming (PRs #76 → #88)

- Wire name: `ga_<topic>_<verb>` (snake_case)
- Method name: `<Topic><Verb>` (PascalCase) on a `<Topic>McpTools` class
- One class per topic; multiple methods OK when they share parsers / state / DI deps
- All inputs length-guarded; all error echoes go through `McpEchoSanitizer`
- `Result` records carry an `Error` branch invariant: `Error != null ⇒ all other fields are empty/zero`
- `Failure` static factory on every Result type

### MCP DI wiring (PR #85 — bug fix)

`InProcessMcpToolsProvider.StartInProcessServerAsync` now scans every registered tool type's longest constructor and forwards each parameter type from the host service provider. Adding a new tool with new DI deps "just works" without touching this file.

### SKILL.md hardening (PR #79)

- Body capped at 32 KB (rejects file outright if exceeded).
- Trigger length minimum 3 chars (silently drops shorter; loader emits a warning when all triggers got dropped).
- Canonical `skills/` resolved at the repo `.git` root; legacy `.agent/skills/` kept as fallback until all skills migrate.

## Bugs caught by multi-LLM review

Recorded in `memory/feedback_multi_llm_review_pays_off.md` and `memory/project_chatbot_skills_migration_2026_05_03.md`. 9 real bugs caught across the workstream (each would have shipped silently). See those memory files for the full list — they're load-bearing learnings that should propagate beyond this migration.

## Workstreams deferred

These were intentionally not done in this session and are good candidates for follow-up sessions:

### 1. Live smoke test (blocked on Ollama)

Once Ollama unwedges, exercise each SKILL.md end-to-end through the LLM path. Currently blocked because Ollama on this host is wedged for chat-model loading (8.2 GB GPU memory held; `/api/ps` shows `models: []` even after restart attempts; embeddings work, chat does not).

**How to start:** Restart Ollama (user must do this manually — denied earlier in session); run the chatbot smoke set against `/api/chatbot/chat` with one query per ported skill. Compare the trace's `agentId` and routing metadata against the C# fast-path baseline.

### 2. C# skill retirement

Migration recommendation says "C# skills coexist until parity is proven." We have unit-test parity but not production-telemetry parity. Decision criterion not yet specified.

**How to start:** Pick a metric with the user before starting (suggested: "X consecutive verdict cycles where SKILL.md output and C# output match for a fixed prompt set"). Then instrument the orchestrator to log both paths' results when both fire, accumulate over a soak period, then retire C# when threshold is met.

### 3. `Spell()` helper extraction (deferred from PR #80 review)

`ChordInfoSkill.cs` and `ChordMcpTools.cs` both have byte-identical `Spell(root, pitchClass, letterSteps)` enharmonic-aware spelling logic. Extract to a shared helper in `Common/GA.Business.Core` (parsing is pure domain, fits the layer boundary).

**Effort**: small; mostly mechanical. Add tests that both call sites continue to work.

### 4. Reparse-point dereferencing on env-var override (deferred from PR #74 review)

`SKILLMD_SKILLS_PATH` env var traversal check is lexical; doesn't dereference NTFS junctions / reparse points / symlinks. The lexical defense (prefix-collision check after trailing-separator append) covers most realistic cases.

**When to do it:** before opening `skills/` to non-employee contributors or any external skill registry. Until then, committer-trust model is sufficient.

### 5. Migrate `MemoryMcpTools` to `ga_memory_*` prefix (deferred from PR #78 review)

`MemoryMcpTools` has methods `MemorySearch`, `MemoryWrite`, `MemoryRead`, `MemoryStats` with no `ga_` prefix. The new `ga_*` convention from this migration suggests aligning. Touches consumer code (any caller depending on exact wire names).

**Effort**: small; mostly renames. Verify no Demerzel or external consumer depends on the unprefixed names first.

## How to continue this work

The migration is structurally complete. **No active work blocks**. When you next pick up chatbot work:

1. Read `memory/project_chatbot_skills_migration_2026_05_03.md` for the full state snapshot.
2. Read `memory/feedback_multi_llm_review_pays_off.md` for the multi-LLM review pattern that paid off this session.
3. Pick a workstream from the deferred list above. The natural next-up is #1 (live smoke) once Ollama is restarted, or #3 (`Spell()` extraction) if Ollama remains wedged.

If you're starting fresh on something else entirely, the migration is "done" enough to leave alone — the SKILL.md files coexist with the C# skills as planned.
