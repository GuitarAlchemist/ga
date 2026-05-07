---
date: 2026-05-06
status: plan (not yet operationalized)
reversibility: phased; Phase 1 is two-way, Phase 2 introduces a one-way door (closure-registry exposure as MCP)
owners: needs assignment
audience: GA chatbot maintainers / Claude Code authors
predecessor: 2026-05-03-chatbot-agent-framework-migration-recommendation.md
---

# GA Skills Orchestration Architecture

How the GA chatbot's **C# orchestrator** should consume **both Claude-Code-flavoured `.agent/skills/`** and **chatbot-flavoured `skills/` markdown** to produce grounded answers via the **F# DSL closure registry** and **OPTIC-K hybrid search** — without writing 10 keyhole MCP tools per BACKLOG #139.

## TL;DR

- `skills/` (15 canonical, version-controlled): chatbot user-facing skills. Each has a `SKILL.md` and a C# `IOrchestratorSkill` wrapper. The wrappers register in `GaPlugin.cs`; the `SemanticIntentRouter` embeds their `Description` + `ExamplePrompts` and routes by similarity. **Already shipped** through PR #126.
- `.agent/skills/` (28 local, gitignored): Claude Code dev/operational skills. Audited and validated by `Scripts/check-agent-skills.ps1` (PR #141). **Not consumed by the chatbot** — they're for Claude Code itself.
- The chatbot's tool surface is currently 7 MCP tools. The F# DSL has **~40 closures** exposing chord/scale/key/voicing/voice-leading/transpose/polychord/arpeggio/etc. operations that the chatbot cannot reach. Closing that gap is the single highest-leverage move.
- This plan proposes **one new MCP tool** — `ga_dsl_eval(closureName, args)` — that wraps `GaClosureRegistry`. Skills generate a closure invocation instead of orchestrating multiple keyhole tools. **Voicing search is the canary.**

## Current state

### Three tracks of "skills" exist today

| Track | Path | Count | Format | Owner | Used by |
|---|---|---|---|---|---|
| Chatbot canonical | `skills/` (repo) | 15 | SKILL.md + C# wrapper | C# `ProductionOrchestrator` | Live chatbot (port 5252) |
| Chatbot dev drafts | `skills-dev/_pending-tools/` | 13 | DRAFT.md | unblocked when MCP tool ships | nothing yet |
| Claude Code dev | `.agent/skills/` (gitignored) | 28 | SKILL.md | Claude Code CLI | dev-time agent flow |

### Tool surface — chatbot has access to 7 of ~40

The chatbot's `InProcessMcpToolsProvider` registers 7 MCP tools (`ga_chord_info`, `ga_chord_substitutions`, `ga_chord_compare`, `ga_fret_span`, `ga_interval_compute`, `ga_key_identify`, `ga_scale_get_notes`). The GA-DSL MCP plugin (consumed by Claude Code at the dev surface) exposes ~40 — including:

- `ga_search_voicings_by_query` (OPTIC-K hybrid search)
- `ga_voice_leading_pair`
- `ga_easier_voicings`
- `ga_transpose_chord`
- `ga_arpeggio_suggestions`
- `ga_polychord`
- `ga_generate_progression`
- `ga_diatonic_chords`
- `ga_relative_key`
- `ga_common_tones`
- `ga_icv_neighbors`

**The chatbot can't see any of these** — they're in a separate MCP server registered for Claude Code's dev environment, not for the chatbot's in-process tool registry.

### The substrate is already built

- **`Common/GA.Business.DSL/Closures/GaClosureRegistry.fs`** — F# closure registry. Each closure is `{ Name; Category; Description; Tags; InputSchema; OutputType; Exec : Map<string,obj> -> GaAsync<obj> }`. Self-describing; introspectable.
- **`Common/GA.Business.DSL/Closures/BuiltinClosures/`** — `Domain`, `Pipeline`, `Agent`, `Io`, `Tab`. Most music-theory operations live here.
- **`Apps/GaMusicTheoryLsp/`** — F# LSP for music-theory DSL editor support (semantic tokens, inlay hints).
- **`Common/GA.Business.DSL/Grammars/*.ebnf`** — 9 grammars: chord/scale/tab/fretboard/Grothendieck/MIDI/practice/VexTab/progression.
- **`Apps/GaChatbot.Api/Services/VoicingSearchWarmupService.cs`** — already loads 4000 OPTIC-K-indexed voicings into memory at chatbot startup (CPU-Parallel strategy, 2-3 second warmup).
- **`Common/GA.Business.ML/Search/EnhancedVoicingSearchService.cs`** — voicing search with strategy interface (CPU/GPU/embedding).

The chatbot has the substrate **but doesn't expose it through the MCP layer that LLM-driven skills can consume.**

## Proposed architecture

### Three-layer composition

```
┌──────────────────────────────────────────────────────────────────────┐
│  LLM-facing surface                                                  │
│  ┌─ skills/ (SKILL.md + C# wrapper) ──────────────────────────────┐  │
│  │  Description + ExamplePrompts → SemanticIntentRouter          │  │
│  │  ExecuteAsync → emits answer using available MCP tools        │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                                ↓
┌──────────────────────────────────────────────────────────────────────┐
│  Tool surface (MCP)                                                  │
│  ┌─ Existing 7 keyhole tools ──┐  ┌─ NEW: ga_dsl_eval ────────────┐  │
│  │ ga_chord_info / etc.         │  │ Wraps GaClosureRegistry       │  │
│  │ Direct domain ops, narrow    │  │ One entry point for ~40 ops   │  │
│  │ contracts                    │  │ LLM picks closure + args      │  │
│  └──────────────────────────────┘  └───────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
                                ↓
┌──────────────────────────────────────────────────────────────────────┐
│  Domain substrate (F# + C#)                                          │
│  ┌─ GaClosureRegistry (F#) ─┐ ┌─ EnhancedVoicingSearchService (C#)─┐  │
│  │ Domain / Pipeline /       │ │ OPTIC-K hybrid search             │  │
│  │ Agent / Io / Tab closures │ │ 4000-doc index in memory          │  │
│  └───────────────────────────┘ └───────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

### What stays the same

- `IOrchestratorSkill` contract (`Name`, `Description`, `ExamplePrompts`, `CanHandle`, `ExecuteAsync`).
- `GaPlugin.cs` registration of skills.
- `SemanticIntentRouter` embedding-based dispatch.
- The 7 keyhole MCP tools — they're useful for narrow, high-signal queries (e.g. *"notes in Cmaj7"* → `ga_chord_info("Cmaj7")` is more direct than going through DSL eval).
- `.agent/skills/` content — Claude-Code-only, owned by Claude Code, validated by the script in PR #141.

### What changes

- **One new MCP tool** in `Common/GA.Business.ML/Agents/Mcp/`: `DslEvalMcpTools.GetClosureSchema(closureName)` + `EvalClosure(closureName, argsJson)`.
- **One new SKILL.md graduation**: `skills-dev/_pending-tools/voicing-search/DRAFT.md` → `skills/voicing-search/SKILL.md` + `VoicingSearchSkill.cs` C# wrapper.
- **Test fixtures**: `DslEvalMcpToolsTests` (per-closure smoke), `VoicingSearchSkillTests` (parity matrix entry, integration test against `EnhancedVoicingSearchService`).

### What's explicitly out of scope (for this plan)

- Migrating all 7 existing keyhole tools to DSL-eval form. They stay as keyhole tools; the DSL is additive.
- Auto-generating SKILL.md from F# closure metadata. Tempting, but the SKILL.md + ExamplePrompts pair carries pedagogy and routing intent that doesn't decompose from a closure schema.
- Cross-orchestrator unification (chatbot ↔ Claude Code seeing each other's skills). Each owns its own skill surface; the boundary is stable.
- Replacing `SemanticIntentRouter` with anything new. The dispatch problem is solved.

## Phasing

### Phase 0 — Inventory and contract design (1-2 hours, two-way door)

Goal: a concrete contract for `ga_dsl_eval` that survives review.

Tasks:
1. Audit `BuiltinClosures/*.fs` to enumerate exactly which closures are domain-relevant for the chatbot vs. Pipeline/Io noise.
2. Decide closure-name canonicalization (kebab? camel? mirroring `ga_*` MCP wire names?).
3. Decide `args` format. Three candidates: a JSON string (most flexible, requires the chatbot LLM to emit valid JSON); a typed key-value list (less flexible, easier to reason about); or per-closure typed contracts (one MCP tool per closure — defeats the purpose).
4. Decide `error` shape: closure-not-found vs. arg-parse-error vs. closure-runtime-error vs. timeout.
5. Decide where to surface closure schemas. The closure registry has `InputSchema : Map<string,string>` — exposing this through `GetClosureSchema` lets the LLM read the contract before invoking.

**Deliverable**: `docs/contracts/2026-05-06-ga-dsl-eval-contract.md` mirroring the existing `qa-verdict.contract.md` / `optick-sae-artifact.contract.md` format. Contract version v0.1, NOT frozen.

### Phase 1 — Implement `ga_dsl_eval` MCP tool (1-2 days, two-way door)

Goal: one MCP tool callable from the chatbot, exposing a curated subset of closures.

Tasks:
1. Add `Common/GA.Business.ML/Agents/Mcp/DslEvalMcpTools.cs`:
   - `GetClosureSchema(closureName)` — returns `{ name, description, inputSchema, outputType }` from `GaClosureRegistry.TryGet`.
   - `EvalClosure(closureName, argsJson)` — JSON-deserialises args into `Map<string,obj>`, runs `closure.Exec`, JSON-serialises the output.
   - `ListClosures()` — returns `{ name, description, category, tags }[]` for the curated subset.
3. Register the tool in `GaPlugin.McpToolTypes`.
4. Curate the visible-to-chatbot set: which closures should the LLM see? Start with **Domain only** (no Io, no Pipeline) — that's safest. Hide closures that touch the network, filesystem, or DB.
5. Write `DslEvalMcpToolsTests` covering: unknown closure, malformed args, successful Domain closure, output serialisation round-trip.

**Deliverable**: PR with the MCP tool + tests. No skill consumes it yet.

**Why this is two-way**: the MCP tool can be removed without affecting any skill. Existing skills don't depend on it.

### Phase 2 — Voicing-search canary (2-3 days, partial one-way door)

Goal: prove the LLM can generate valid DSL closure invocations end-to-end.

Tasks:
1. Identify the right closure for natural-language voicing search. Candidates from the GA-DSL MCP plugin's surface:
   - `ga_search_voicings_by_query` — direct match for the canary.
   - Verify it's wired into `GaClosureRegistry` (or wire it).
2. Graduate `skills-dev/_pending-tools/voicing-search/DRAFT.md` → `skills/voicing-search/SKILL.md`. Update `allowed-tools:` to `ga_dsl_eval` (or keep the original name if exposed directly).
3. Add C# wrapper `VoicingSearchSkill.cs` following the pattern from PR #126. The wrapper's `ExecuteAsync` doesn't compute anything itself — the SKILL.md body teaches the LLM to emit a `ga_dsl_eval("voicing-search", {...})` call.
4. Register in `GaPlugin.cs` and `SkillParityMatrixTests` Contracts.
5. Add `VoicingSearchSkillTests` following the catalog-skill pattern (PR #135's harness).
6. Live retest: hit `/api/chatbot/chat` with *"find me a mellow Cm9 voicing"* — verify routing → DSL eval → ranked voicings → response.

**Deliverable**: PR shipping voicing-search end to end.

**Why this is partial one-way**: once a skill consumes `ga_dsl_eval`, downgrading the contract (renaming closures, restructuring args) becomes a coordinated change. Adding closures stays additive.

### Phase 2 finding (2026-05-06): canary emits docs, not answers

Live retest of the transpose canary surfaced a precondition Phase 3 was
silently assuming. The C# wrapper `TransposeSkill` is registered as
`IIntent` (via `AddOrchestratorSkillIntent<T>`), so the
`SemanticIntentRouter` considers it for routing. The matching
`SkillMdDrivenSkill` instance built from `skills/transpose/SKILL.md` is
registered only as `IOrchestratorSkill` — the router never sees it. So
"transpose Cmaj7 up a perfect fifth" routes to the markdown-emitter
wrapper at confidence 0.83 and the response is the SKILL.md *manual*,
not `Gmaj7`. Anthropic + `ga_dsl_eval` is fully wired but never
invoked.

Phase 3's invocation-success metric needs the LLM-in-the-loop path
actually live before it can measure anything. Two ways to land that:

A. Make `SkillMdDrivenSkill` an `IIntent` too (register it via the
   same adapter as the C# wrappers). Then drop the C# wrapper, since
   it duplicates name/description/examples that already live in the
   SKILL.md frontmatter. Same id collision risk — pick one or the
   other per skill.

B. Have the C# wrapper's `ExecuteAsync` delegate to a
   `SkillMdDrivenSkill` built from the same `SKILL.md`, instead of
   emitting the body verbatim. The wrapper keeps owning routing
   metadata (so `IIntent` registration stays) and the SKILL.md
   becomes the runtime system prompt for the LLM call.

Pick before starting Phase 3 — measuring the markdown-emitter against
the keyhole baseline measures the wrong thing.

### Phase 3 — Measure (one week soak)

Goal: empirically decide whether the DSL pattern beats keyhole tools.

Tasks:
1. Define the success metric. Recommend: **DSL-eval invocation success rate** = (LLM emits valid closure name + args that produce a non-Error result) / (total `ga_dsl_eval` invocations).
2. Run a curated 20-prompt smoke set covering the BACKLOG #139 gaps. Compare:
   - DSL-eval invocation success rate
   - Time-to-first-token vs. keyhole-tool baseline (PR #135 baseline numbers)
   - Bug rate (compared with keyhole-tool baseline)
3. Decision gate:
   - **Proceed** if invocation success ≥ 80% AND latency parity ≥ 75% of keyhole baseline
   - **Pivot** to keyhole tools for closures the LLM can't reliably invoke
   - **Roll back** Phase 2 only if invocation success < 50%

**Deliverable**: `state/quality/dsl-eval/2026-05-XX-soak-results.json` and a follow-up plan amendment.

### Phase 4 — LSP pre-validation (1 day, optional)

Goal: catch malformed closure invocations before the runtime sees them.

Tasks:
1. Wire `Apps/GaMusicTheoryLsp` to expose a `validate(text)` endpoint over a local socket or via a simple HTTP probe.
2. Have `DslEvalMcpTools.EvalClosure` call the LSP for syntax validation BEFORE executing the closure.
3. On validation failure, return a structured error to the LLM with the exact diagnostic — the LLM can self-correct on the next round-trip.

**Why this is optional**: only worth doing if Phase 3's invocation success rate < 80%. If it's already 80%+, the LSP round-trip adds latency without solving a real problem.

### Phase 5 — Generalise (open-ended)

Goal: tackle the remaining 9 BACKLOG #139 gaps using the DSL pattern.

Each gap becomes a 1-2 day iteration: identify the closure, graduate the SKILL.md, add the C# wrapper, register, test, live-verify.

**No new architecture.** This phase is just *applied* Phase 2 across the rest of the gaps. Stop the loop early on any gap that fails Phase 3-style measurement and route it through a keyhole MCP tool instead.

## Reversibility log

| Phase | Decision | Reversibility | Revisit trigger |
|---|---|---|---|
| 0 | DSL-eval contract shape | two-way (it's a doc) | Contract version v0.1; bump to v0.2 if Phase 1 implementation surfaces gaps |
| 1 | `ga_dsl_eval` MCP tool exists | two-way (no skill consumes it) | Remove if Phase 2 fails |
| 2 | Voicing-search skill ships using DSL eval | partial one-way (downgrade is coordinated) | Phase 3 measurement; rollback gate at <50% success |
| 3 | Pattern accepted as the canonical path for new skills | one-way (informs ≥9 future skills) | If 3 of next 9 skills fail Phase-3 success criteria, pivot back to keyhole tools |
| 4 | LSP-coupled validation | two-way (additive) | Only if Phase 3 demands it |

## Test plan

- **Unit**: per-closure round-trip tests (input → JSON → `Map<string,obj>` → closure → JSON → output). Stub-resistant.
- **Integration**: `VoicingSearchSkill` against the live `EnhancedVoicingSearchService` with the warmup-loaded 4000-doc index.
- **Live (gated on Ollama recovery)**: 20-prompt smoke set covering both keyhole-tool gaps and DSL-eval invocations. Success rate becomes Phase 3's decision input.
- **Parity**: extend `SkillParityMatrixTests` to require every new skill (voicing-search and beyond) to have both a SKILL.md and a registered C# wrapper.

## Risks

| Risk | Mitigation |
|---|---|
| LLM struggles to generate valid closure args (especially nested objects) | Phase 0 args-format decision: prefer flat key-value over nested JSON. Phase 4 LSP pre-validation catches syntax errors before execution. |
| Closure registry exposure leaks Pipeline/Io closures with destructive side-effects | Phase 1 curates the visible set. Pipeline / Io / Agent categories explicitly excluded. |
| Latency floor of "schema lookup → eval" is worse than direct keyhole-tool calls | Phase 3 measurement decides. Schema lookup is cacheable (closures don't change at runtime). |
| Closure contract churn (renames, arg-shape changes) breaks shipped skills | Pin contract versions in `docs/contracts/`. Closure renames require a `links.supersedes` migration entry per the cross-repo contract pattern documented in CLAUDE.md. |
| Pro-guitarist gaps from BACKLOG #139 don't all map to existing closures | Where they don't, fall back to building a new closure (additive to F#) rather than a new MCP tool. The parser/exec is shared; the cost is one F# function per gap. |
| Ollama unavailability blocks Phase 3 measurement | Already-known infra issue (`reference_ollama_runner_crash_2026_05_05.md`). Phase 3 needs Ollama; do not start it before Ollama is restored. Static evidence (Phase 1 + Phase 2 unit/integration tests) is unblocked. |

## Out of scope

- **Music-theory DSL grammar editor in the chatbot UI**. The LSP exists; surfacing it through the React chat is a separate UX workstream, not this plan.
- **Per-skill SKILL.md authoring tooling**. The SKILL.md format is stable (PR #113 camelCase parity).
- **Cross-repo contract evolution** (Demerzel / ix integration with the new tool). The DSL eval is in-process; no cross-repo handshake needed for v0.1.
- **Replacing `IOrchestratorSkill`** with `Microsoft.Agents.AI` constructs. That's the broader migration plan (`2026-05-03-...recommendation.md`); this plan does not preempt it.
- **GraphQL exposure** of the DSL. The chatbot speaks MCP; GraphQL is a separate consumer.

## Cross-references

- Predecessor plan: `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md` — describes the broader migration. This plan is a focused workstream within Phase 1's "tool inventory expansion" theme.
- BACKLOG #139 (PR shipped 2026-05-06): 10 pro-guitarist usability gaps. This plan is the substrate that lets ≥7 of them be implemented as additive closures rather than new MCP tools.
- Architecture reminder (CLAUDE.md): five-layer model. The DSL is Layer 3 (Analysis); the closure registry is the boundary between Layer 3 and the orchestration layer (Layer 5). This plan does not violate the layering.
- Skill-stewards loop (`docs/plans/2026-05-05-skill-stewards-team.md`): the loop's `skill-graduator` agent will consume this plan when graduating draft skills that bind to DSL closures.
- PR ledger (today's date 2026-05-06): #141 `Scripts/check-agent-skills.ps1` — orthogonal hygiene work for `.agent/skills/`. Not blocked by or blocking this plan.

---

**Status**: this plan is intentionally not yet operationalized — Phase 0 (contract design) needs explicit sign-off before Phase 1 starts. Open questions for the maintainer:
1. Closure-arg format: flat key-value, JSON string, or per-closure typed contracts?
2. Visible-to-chatbot closure set: Domain only, or Domain + curated Pipeline?
3. Phase 3 success threshold: is 80% invocation success the right bar, or should it be higher?
4. LSP pre-validation: ship in Phase 1 (proactive) or Phase 4 (reactive)?
