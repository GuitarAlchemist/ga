# Chatbot Agents & Skills Context

> Fresh-session orientation for `Common/GA.Business.ML/Agents/`. Read this BEFORE touching anything in this subsystem.

## What this subsystem is

The chatbot's deterministic-first skill surface. Each `IOrchestratorSkill` is a single intent ("show me Cmaj7 voicings", "what scale fits a G7", "transpose Am to D"). Skills compute answers from the GA domain (layer 2-3), never from an LLM at this layer. The `SemanticIntentRouter` ranks skills by embedding similarity over each skill's `Description` + `ExamplePrompts`; a legacy `CanHandle` foreach is the keyword fallback. Multi-step agent work (TabAgent, TheoryAgent, etc.) lives alongside, but new natural-language capabilities should ship as skills, not agents.

## Key invariants (DO NOT VIOLATE)

- **No LLM fallback at the skill layer.** When a skill can't find a chord/mode/intent in the user's message, return an `AgentResponse` with low confidence and `Result` text — do NOT call `IChatClient`. The orchestrator owns LLM fallback.
- **Case-sensitive chord regex.** `[A-G]` (no `RegexOptions.IgnoreCase`). Bare lowercase "a"/"e" in normal English ("show me a shape") MUST NOT trigger a music-theory skill. See `ChordVoicingsSkill.ChordSuffixRegex`.
- **Routing is deterministic music theory only.** Per memory rule `feedback_skill_routing_semantic`: skills keep their `CanHandle`; embedding/agentic dispatch is only for multi-step agent work. Do not delete `CanHandle` from a skill.
- **Cancellation token threaded through every async.** Every `await` inside `ExecuteAsync` MUST forward `cancellationToken`. The chatbot timeout cascade depends on this.
- **`Confidence` is a float in [0,1].** Returning `> 0.85` lets `SemanticRouter` skip the LLM second-pass — claim that only when the domain answer is unambiguous.
- **Skill lifetime matches dependencies.** `Singleton` for pure-domain skills; `Scoped` for skills that depend on `IChatClient` (which is Scoped). `KeyIdentificationSkill` and `ProgressionCompletionSkill` are the canonical Scoped examples.
- **`AgentResponse.Data` is the structured payload.** UI (chat panel, voicing diagrams) reads `Data`, not `Result` markdown. Always include a typed object — never `null` when there's data to show.
- **Don't change OPTIC-K dimensions from a skill.** Read `EmbeddingSchema.TotalDimension`; never hardcode. Skills that touch voicing search must use `EnhancedVoicingSearchService` + `MusicalQueryEncoder` so dimensions stay aligned.

## The 5-10 files that matter

- `IOrchestratorSkill.cs` — the contract. `Name`, `Description`, `ExamplePrompts`, `CanHandle`, `ExecuteAsync`.
- `Skills/ChordVoicingsSkill.cs` — canonical "domain-retrieval skill" template (2026-05-16). Parser → encoder → vector search → response. Read this first.
- `Skills/ImprovisationSkill.cs` — canonical "domain-classifier skill" template (2026-05-16, #219). Pure switch over quality enum, no I/O. Read second.
- `Skills/SkillMdDrivenSkill.cs` + `SkillMdDrivenWrapperBase.cs` — Path B (LLM-in-the-loop) wrapper for DSL-eval-backed skills (`TransposeSkill`, `CommonTonesSkill`, `DiatonicChordsSkill`).
- `SemanticRouter.cs` — embedding-based agent router. Caches agent embeddings + a 256-entry query embedding cache.
- `Intents/SemanticIntentRouter.cs` — the corresponding skill router (lives one layer up at the IIntent surface).
- `AgentConstants.cs` — agent IDs (`tab`, `theory`, `voicing`, `qa-architect`, …). Magic-string-free.
- `IAgentSkill.cs` + `AgentSkillBase.cs` — older per-agent skill abstraction; new work uses `IOrchestratorSkill`.
- `Hooks/*` — `PromptSanitizationHook`, `MemoryHook`, `MemoryWriteHook`, `ObservabilityHook`. Execute in registration order at each lifecycle point.

## How to add a new chatbot skill

1. **Create the C# file** under `Skills/<Name>Skill.cs`. Implement `IOrchestratorSkill`. Use `ChordVoicingsSkill` (retrieval) or `ImprovisationSkill` (classifier) as your template — match their case-sensitive chord regex pair `[A-G][#b]?(?:maj|min|m|M|dim|aug|sus|add|alt|°|Δ|\d)\w*` + the spaced-quality form.
2. **Write 6-12 `ExamplePrompts`** that span phrasings real users will type. The embedding router scores against these — too few examples = misroutes.
3. **`CanHandle` is the keyword-fallback gate.** Intent keyword + chord-shaped token. Never lowercase-match a bare letter.
4. **Register in `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs`** via `services.AddOrchestratorSkillIntent<YourSkill>()`. Pass `ServiceLifetime.Scoped` only if your ctor takes `IChatClient`.
5. **Add tests** under `Tests/Common/GA.Business.ML.Tests/Unit/<Name>SkillTests.cs` mirroring `ChordVoicingsSkillTests.cs` — cover `CanHandle` positive + lowercase-article negative + `ExecuteAsync` happy path + no-intent branch.
6. **Add a fixture** in `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml` only via the prompt-corpus runbook — see `docs/runbooks/chatbot-improvement-loop.md`. (Do NOT edit `prompts.yaml` directly without the runbook flow.)
7. **No SKILL.md required** for pure-C# skills. The `skills/<name>/SKILL.md` files are only for the Path B DSL-eval skills (transpose, common-tones, diatonic-chords) and the catalog graduates.

## What NOT to do here

- Don't add `RegexOptions.IgnoreCase` to chord regex. Whole subsystem depends on case-sensitivity. (Hardened in PR #251.)
- Don't call `IChatClient` from a new skill. If you need explanation, return structured `Data` and let the orchestrator narrate.
- Don't register a new skill anywhere except `GaPlugin.cs`. Two registration points = duplicate intent embeddings = misroutes.
- Don't substring-assert against LLM-generated wording in tests (the failure that birthed `SetTheoryEquivalenceSkill` 2026-05-13). Assert against `AgentResponse.Data`/`Evidence`.
- Don't change `IOrchestratorSkill` without coordinating — every skill implements it. The `ExamplePrompts` default-empty member is the safe extension pattern.
- Don't refactor `MemoryHook` to also write durable memory. That conflation was the bug fixed at `docs/solutions/architecture/2026-05-11-memoryhook-conflates-transcript-log-with-durable-memory.md` — `MemoryWriteHook` owns durable writes.
- Don't change OPTIC-K dimensions to "make voicing search work." The corpus-tagging mismatch (see voicing solutions doc) is fixed by retry-without-filters, not by re-indexing.

## Where to look for related context

- Parent: [/CLAUDE.md](../../../../CLAUDE.md) — Karpathy rules, layer model, OPTIC-K invariants.
- Sibling: [`../../../Apps/GaChatbot.Api/CONTEXT.md`](../../../Apps/GaChatbot.Api/CONTEXT.md) — the host that wires this up.
- Sibling: [`../../GA.Business.Core.Orchestration/Plugins/CONTEXT.md`](../../GA.Business.Core.Orchestration/Plugins/CONTEXT.md) — where skills get registered.
- Architecture: [`docs/architecture/layers.md`](../../../../docs/architecture/layers.md) — layer-4 rule (AI code lives here).
- Solutions: `docs/solutions/architecture/2026-05-08-voicing-search-corpus-tagging-mismatch.md`, `docs/solutions/architecture/2026-05-11-memoryhook-conflates-transcript-log-with-durable-memory.md`, `docs/solutions/runtime-errors/enricher-substring-on-wrong-field-cmaj7-jazz.md`.
- Migration plan (still canonical): `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md`.
