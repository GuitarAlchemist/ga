---
title: "Process-wide memory store + anonymous traffic = cross-conversation leak through skill regex parsers"
date: 2026-05-07
problem_type: "architecture"
component: "GA.Business.ML.Agents.Hooks.MemoryHook / MemoryStore"
symptoms:
  - "ChordSubstitutionSkill returns a two-chord comparison (`G7 → A relationship analysis: Backdoor Dominant`) when the user asked for a single-chord substitution (`Tritone substitution for G7`)"
  - "Soak run shows skills routing correctly via SemanticIntentRouter but emitting wrong-shape answers"
  - "Reproducible only after the chatbot has answered any high-confidence question — fresh installs are fine"
  - "Same input via REST `/api/chatbot/chat` and via the SignalR hub get different answers across sessions"
  - "`~/.ga/memory.json` grows unboundedly and silently corrupts under concurrent writes"
tags:
  - "chatbot"
  - "memory"
  - "anonymous-traffic"
  - "cross-tenant-leak"
  - "regex-parser"
  - "hook-mutation"
severity: "critical"
related_docs:
  - "docs/plans/2026-05-06-skills-orchestration-architecture.md"
related_prs:
  - "#151"
---

# Process-wide memory store leaks into anonymous prompts via `MemoryHook`

## Symptoms

The chatbot's skill outputs are *almost* right but not quite. A user prompt of `"Tritone substitution for G7"` correctly routes to `skill.chordsubstitution` (good), but the skill returns a TWO-chord comparison (`**G7** → **A** relationship analysis ★★ **Backdoor Dominant**`) instead of substitution suggestions for G7 alone. Where does the `A` come from?

Tracing the `ExtendedChordSymbol` regex on the literal input `"Tritone substitution for G7"` returns exactly one match: `G7`. There is no second chord in the user's text. So the `chords.Count == 2` branch should not have fired.

The phantom `A` came from outside the user's message — `MemoryHook.OnRequestReceived` had prepended a `[memory:response_*]` block from a prior unrelated conversation that contained `"A"` somewhere, and the regex picked it up as a second match.

## Root cause

`MemoryHook` is wired in `GaPlugin.Register` and runs unconditionally on every request:

```csharp
public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
{
    var matches = memoryStore.Search(ctx.CurrentMessage);
    if (matches.Count == 0) return Task.FromResult(HookResult.Continue);

    var contextBlock = string.Join("\n", matches.Take(3).Select(m => $"[memory:{m.Key}] {m.Content}"));
    var enriched = $"[Relevant context from memory]\n{contextBlock}\n\n{ctx.CurrentMessage}";
    return Task.FromResult(HookResult.Mutate(enriched));
}
```

`MemoryStore` persists to `~/.ga/memory.json` — process-wide, **not session-scoped**. Every chatbot turn with `Confidence ≥ 0.7` and `Length ≥ 100` writes a new entry keyed by correlation ID. Subsequent searches return whatever overlaps with the new user's prompt by substring. The assistant text from any prior conversation can land in any new conversation's prompt before any skill sees it.

For the chatbot that backs `demos.guitaralchemist.com/chatbot/`, this is a multi-tenant data leak: User A's response text is fed verbatim into User B's prompt context.

The regex parsers in `ChordSubstitutionSkill` (and similar skills like `ChordInfoSkill`, `IntervalSkill`) operate on the **mutated** message — they treat the entire injected memory block as user input. Any chord-shaped substring in the memory poisons the parse.

## Cross-cutting consequence

`MemoryHook` participates in a generic hook contract. Any hook that mutates `ctx.CurrentMessage` exposes the same risk to every downstream consumer. The skill never knows the message was rewritten.

`ProductionOrchestrator` keeps `OriginalMessage` separate, but the orchestrator-skill interface only passes `CurrentMessage`. There is no path for a skill to ask "what did the user actually type?".

## Fix (PR #151 — minimum viable correctness)

1. **Gate retrieval behind a config flag** (`Memory:EnrichOnRetrieve`, default `false`). The hook noops by default until session-scoped memory ships.
2. **Make persistence atomic** — `MemoryStore.Save` now uses `SemaphoreSlim` + temp-file + `File.Move(overwrite: true)` so concurrent writes can't truncate the JSON. `Load` quarantines corrupted files to `memory.json.corrupt-<timestamp>` instead of silently zeroing.
3. **LRU eviction at 10k entries** — the file cannot grow unboundedly even if persistence is ever re-enabled.
4. **Clear the corrupted store** — back up `~/.ga/memory.json` and reset before re-deploying.

## Fix layer 2 (deferred — task #82)

Make `MemoryStore` session-scoped. Plumb `SessionId` through `ChatHookContext`. `MemoryHook.Search` filters by SessionId; `Write` persists under that key. Then re-enable retrieval safely. Until then, persistence still runs but no read path uses it.

## Detection

Two-line repro:

1. POST any prompt that elicits a high-confidence response (`/api/chatbot/chat` "what notes are in Cmaj7" returns ChordInfoSkill at `Confidence=1.0`).
2. POST a different prompt to the same endpoint (`Tritone substitution for G7`). The skill output references chord names from prompt #1 even though the new message doesn't mention them.

If the answer contains chord references that aren't in the input, you've hit the leak.

## Pattern: hooks that mutate the message exposed to skill parsers

Anywhere a skill parses the message with regex, substring search, or a dumb tokenizer, an upstream `OnRequestReceived` mutation can poison the parse. Three rules:

1. **Skills that parse should use `OriginalMessage`, not `CurrentMessage`.** The hook contract should expose both. (Today only the orchestrator sees them separately.)
2. **Hooks that prepend context should sandbox it.** Wrap injected blocks in syntactic markers a skill parser can recognize and skip (`<<<context>>>...<<<endcontext>>>`).
3. **Persistent state in a multi-tenant chatbot must be tenant-scoped at the storage layer.** Process-wide stores work for single-user CLIs and are footguns for public hubs. The flag pattern is a stopgap, not a fix.

## Pattern: an "opt-in flag" without supporting plumbing is a footgun

Defaulting `Memory:EnrichOnRetrieve` to `false` is correct for now. But any operator who flips it to `true` re-introduces the leak verbatim. The flag has zero session-scoping behind it. Document the dependency loudly:

```csharp
// MemoryHook.cs
// Off by default — retrieval depends on session-scoped MemoryStore (task #82).
// Flipping this to true in a multi-user deployment recreates the cross-conversation
// leak documented in docs/solutions/architecture/2026-05-07-process-wide-memory-store-leaks.md
private readonly bool _enrichOnRetrieve =
    configuration.GetValue<bool?>("Memory:EnrichOnRetrieve") ?? false;
```

## Why this stayed latent

For the duration of single-user development, the operator was the only "tenant". Every memory entry was the operator's own past response. The leak was indistinguishable from "the chatbot remembers context", which is the marketed feature.

The bug surfaced only when:
- Multiple soak runs (different prompts) accumulated cross-topic responses, and
- A new skill (`ChordSubstitutionSkill`) ran a regex that picked up chord names anywhere in its input.

Single-tenant testing of any chatbot feature that touches a process-wide store is structurally insufficient. Test plan addendum for any hook that mutates messages: run two unrelated prompts back-to-back and assert the second answer's evidence does not reference the first prompt's nouns.
