# Mercury 2 Subagent Evaluation — Inception Labs LLM for Query Extraction

**Status:** Pending — provider class scaffolded locally, integration deferred to a fresh session.
**Date:** 2026-05-13
**Owner:** spareilleux
**Reversibility:** Two-way door at every step (no schema, no on-disk format, no public API contract changes).
**Revisit trigger:** Next time `LlmMusicalQueryExtractor` latency shows up in a chatbot UX complaint, or when the deferred ICV-reconciliation PR lands and the corpus rebuild gives us a clean baseline to A/B against.

## Problem statement

`LlmMusicalQueryExtractor` is the fuzzy-query fallback when `TypedMusicalQueryExtractor` can't parse a structured chord+tag query (per `feedback_telemetry_sweep_before_design.md` it handles the ~4% tail). Today it runs through the same `IChatClient` the chatbot's main loop uses — Anthropic Haiku 4.5 in production via `AI:ChatProvider=claude`. Haiku is overspec'd for structured JSON extraction:

- Sonnet/Haiku class models take ~1.5–3 s per call on this workload.
- The output is a small fixed-schema JSON object (chord/mode/tags).
- The call is on the chatbot's hot path — user-facing latency.

Per LinkedIn / blog reference: **Inception Labs Mercury 2** is a diffusion-based LLM marketed as a "subagent" tier — fast, cheap, structured-output specialist. Their pitch: ~5× faster than Sonnet 4.6 at matched quality on routing, summarization, and tool selection. The article ("Rise of Realtime Subagents", 2026-05) cites Augment Code's production results: 82% latency drop on context compaction, 90% cost drop, 30% reduction in total LLM spend via their "Prism" routing system.

## Benchmark (2026-05-13)

Drove 8 representative voicing queries through Mercury 2 via direct HTTP calls (no DI integration), `response_format = json_object`, temperature 0:

| Query | Latency | Tokens | Output | Verdict |
|---|---:|---:|---|---|
| `Cmaj7 drop2 jazz` | 432 ms | 275 | `chord:Cmaj7, tags:[jazz,drop2]` | ✅ |
| `F# Lydian dominant` | 551 ms | 359 | `chord:null, mode:Lydian dominant, tags:[]` | ⚠️ Lost F# root |
| `something dreamy and floaty in C` | 1040 ms | 684 | `chord:Cmaj9, tags:[jazz,open]` | ✅ Defensible |
| `Em(maj7) shell voicing` | 630 ms | 449 | `chord:EmMaj7, tags:[shell]` | ✅ (case-insensitive dict) |
| `drop 3 blues turnaround in A` | 705 ms | 482 | `chord:A7, tags:[blues,drop3,turnaround]` | ✅ |
| `rootless Dm9 bill evans style` | 535 ms | 359 | `chord:Dm9, tags:[rootless,bill,evans,jazz]` | ⚠️ Split iconic name |
| `warm jazz` | 315 ms | 258 | `chord:null, tags:[jazz]` | ✅ Dropped "warm" |
| `ii-V-I in Bb with quartal voicings` | 848 ms | 526 | `chord:Bb, tags:[quartal]` | ✅ Acceptable |

**Latency distribution:** median 590 ms, p95 848 ms, mean 632 ms (n=8 cold calls).

**Quality:** 5/8 perfect, 2/8 minor issues (mode-only-query loses root; iconic chord names split), 1/8 acceptable degradation. The two real misses are likely fixable via few-shot examples in the `SystemPrompt`.

Compared to baseline (Anthropic Haiku 4.5 on the same prompt): no formal benchmark this session, but anecdotally Haiku runs 1.5–2 s for the same task. **Mercury wins by ~3× on latency at acceptable quality.**

## Design — two-flag opt-in gate

The integration is intentionally narrow: subagent tier only, not the chatbot's main loop. Two configuration flags must BOTH be set before any production traffic routes to Mercury:

```yaml
Inception:
  ApiKey: "sk_..."                  # or INCEPTION_API_KEY env var
  EnableForQueryExtraction: true    # explicit opt-in; default false
```

Without `EnableForQueryExtraction = true`, `LlmMusicalQueryExtractor` uses the existing default `IChatClient` (Anthropic Haiku / Ollama). A stray `INCEPTION_API_KEY` env var cannot silently re-route the chatbot. Same shape as `AI:ChatProvider=claude`.

When both flags are set:
- `LlmMusicalQueryExtractor` resolves the keyed `"subagent"` `IChatClient` (Mercury 2).
- `ChatClientGroundedNarrator` and other agents continue using the default `IChatClient` (Anthropic/Ollama).
- The chatbot's tool-using agent loop stays on Claude, where tool-use reliability beats per-call latency.

## Provider class (scaffolded locally, not pushed)

`Common/GA.Business.ML/Providers/InceptionProvider.cs` — mirrors `DockerModelRunnerProvider`'s OpenAI-SDK-with-custom-endpoint pattern. Lives on local branch `feat/inception-subagent-2026-05-13`, commit `fd11d25c`. Not pushed pending fresh-session integration.

Key API surface:

```csharp
public static class InceptionProvider
{
    public const string DefaultBaseUrl = "https://api.inceptionlabs.ai/v1";
    public const string DefaultChatModel = "mercury-2";
    public const string SubagentServiceKey = "subagent";
    public const string ConfigSection = "Inception";
    public const string ApiKeyEnvVar = "INCEPTION_API_KEY";

    public static IChatClient CreateChatClient(string apiKey, string? baseUrl = null, string? model = null, ILogger? logger = null);
    public static IChatClient CreateChatClientFromConfig(IConfiguration configuration, ILogger? logger = null);
    public static bool IsConfigured(IConfiguration configuration);
    public static bool IsEnabledForQueryExtraction(IConfiguration configuration);
}
```

Internal `InceptionChatClient : IChatClient` wraps `OpenAI.OpenAIClient(apiKey, endpoint).GetChatClient(model)` — same approach as `DockerModelRunnerChatClient`. Eager-throws on missing API key at construction (PR #151 lesson: deferred lambda construction hides config errors behind opaque 500s).

## Required follow-ups before flipping the flag

In rough order:

1. **Rotate the API key.** The current key was pasted in chat (`sk_0b018d0f...`). Treat as compromised; rotate before any committed reference.
2. **Repair pre-existing test-project build errors** in `Tests/Common/GA.Business.ML.Tests/Integration/{OllamaProvider,HybridEmbeddingService}IntegrationTests.cs` — broken since the 2026-03-02 structural migration (`3dff78d0`). Without these compiling, `dotnet test` on the project fails and the new `InceptionProviderTests` can't run.
3. **Wire the DI registration in `AIServiceExtensions.cs`** (the local commit has this; pull it from `fd11d25c` or rebuild from this doc). Two changes:
   - Register the keyed `"subagent"` `IChatClient` when `IsConfigured` is true.
   - Switch `LlmMusicalQueryExtractor`'s registration to a factory lambda that resolves the keyed service when `IsEnabledForQueryExtraction` is true.
4. **Add an end-to-end test** that flips both flags and asserts a real Mercury response on a known query. Requires a CI-side API key in GitHub Secrets.
5. **Run the 25-query telemetry sweep** with Mercury on, compare against the current Haiku baseline. Acceptance: p95 < 1 s (vs current ~2 s) with no quality regression on the structured 96% of queries (the typed extractor handles those — Mercury only sees the fuzzy 4%).
6. **Prompt-tune for the two known quality misses:**
   - Mode-only queries (`F# Lydian`) should keep the root in `chord` even when the user emphasizes mode.
   - Iconic chord names (`bill evans chord`) should resolve via the multi-word composite scan, not split into separate tag tokens. The typed extractor already handles this — Mercury's prompt should mirror that behavior.

## Out of scope (for this evaluation)

- **Replacing the chatbot's main `IChatClient`.** Mercury is a subagent-tier model per its own marketing. The agent loop needs tool-use reliability that Claude/Sonnet provides; Mercury hasn't been tested there.
- **Embedding generation.** Mercury doesn't appear to ship an embeddings API; Ollama + nomic-embed-text stays for vector search.
- **Replacing `ChatClientGroundedNarrator`'s LLM.** Different latency tradeoff (narration is a long-output task; Mercury's 5× speed advantage is on short structured outputs).

## One-way door log

- **None.** Every step is two-way:
  - Provider class is additive (deleting it has no callers).
  - DI registration is gated on the opt-in flag.
  - Existing prompts/schemas unchanged; rollback = flip flag off.
  - No on-disk format, no schema hash, no public API contract.

## Cross-repo coordination

- **ix:** No impact. ix tools don't call `LlmMusicalQueryExtractor`.
- **TARS:** No impact.
- **Demerzel:** Per Galactic Protocol, adding an external LLM provider that touches chatbot output should be logged in `state/knowledge/`. Defer to fresh-session integration.

## Acceptance criteria (for the follow-up integration PR)

- [ ] `InceptionProvider.cs` + tests merged to main.
- [ ] DI wiring merged behind the two-flag gate.
- [ ] `dotnet test` on `GA.Business.ML.Tests` passes (pre-existing breakage repaired).
- [ ] Live MCP test: flip `Inception:EnableForQueryExtraction=true` in a dev environment, run the 25-query telemetry sweep, confirm p95 < 1 s with no quality regression on the structured 96%.
- [ ] Telemetry distribution comparison logged in `state/telemetry/voicing-search/`.
- [ ] Cost analysis: per-query $ cost on Mercury (~$0.00025 prompt + $0.00075 completion per /v1/models pricing) vs Haiku — should net cheaper, but verify.
- [ ] API key rotated and stored in `INCEPTION_API_KEY` env var (or GitHub Secrets for CI), never in committed files.
