---
title: "MemoryHook conflates transcript log with durable memory store"
date: 2026-05-11
problem_type: "architecture"
component: "GA.Business.ML.Agents.Hooks.MemoryHook / MemoryStore"
symptoms:
  - "`~/.ga/memory.json` is 100% `type=response` entries after normal chatbot use"
  - "Memory curator (`ga-memory curate`) finds no curatable content because every entry is a transient chat log"
  - "Retrieval injection (`Memory:EnrichOnRetrieve=true`) injects past Q&A pairs that are rarely useful as durable context"
  - "The 10k-entry store cap evicts oldest entries by Timestamp — durable facts (if any are written) get evicted by sheer volume of response logs"
tags:
  - "chatbot"
  - "memory"
  - "memoryhook"
  - "transcript-log"
  - "curator"
  - "architectural-conflation"
severity: "medium"
related_docs:
  - "docs/plans/2026-05-10-feat-dreams-lite-memory-curator-plan.md"
  - "docs/solutions/architecture/2026-05-07-process-wide-memory-store-leaks-into-anonymous-prompts.md"
related_prs:
  - "#157"
  - "#160"
  - "#163"
  - "#166"
  - "#170"
  - "#171"
---

# MemoryHook conflates transcript log with durable memory store

## Symptom

Running the memory curator (PR #166) against `~/.ga/memory.json` (PR #170 end-to-end smoke):

```
$ ga-memory curate
Loaded 86 entries from C:\Users\spare\.ga\memory.json
```

Type distribution of those 86 entries:

```
response             86
```

**100% of entries are `type=response`.** No `fact`, no `preference`, no `focus`, no durable knowledge. The curator was designed to merge duplicates, replace stale entries, and surface emergent insights from durable memory. Pure transient logs are the wrong shape — there's nothing to curate.

## Root cause

`Common/GA.Business.ML/Agents/Hooks/MemoryHook.cs:125-177` writes a `type=response` entry on every chat turn that satisfies a confidence threshold:

```csharp
public Task<HookResult> OnResponseSent(ChatHookContext ctx, ...)
{
    if (ctx.Response is not { Confidence: >= 0.7f } response) return HookResult.Continue;
    if (response.Result.Length < 100) return HookResult.Continue;
    ...
    memoryStore.Write(
        sessionId: sessionId,
        key: $"response_{correlationId:N}",
        type: "response",
        content: $"Q: {originalMessage}\nA: {resultSnippet}",
        tags: [agentId]);
}
```

This is a **transcript log**, not a **durable memory** entry. The store ends up storing:

| What's there | What we want there |
|---|---|
| Every Q&A pair the chatbot has answered | User preferences (instrument, genre, level) |
| Stamped with the responding agent's ID | Validated facts the user confirmed |
| Truncated to 500 chars | Compact, durable nuggets |
| Keyed by `correlationId` (one per request) | Keyed by topic / fact |

The MemoryStore was originally designed as the durable knowledge backing — what the chatbot has *learned* about the user, not what it *said* to the user. The hook conflates the two.

## Why this matters

Three downstream effects:

1. **Curator is useless on this data.** PR #170's smoke surfaced this — Sonnet 4.6 was given 86 transient entries to bookkeep. The diff-discipline gap that PR #170 documented (one entry unaccounted for) was partly because there was no useful curation to do, just exhaustive copying.

2. **Retrieval injection is noisy.** When `Memory:EnrichOnRetrieve=true` eventually ships, the chatbot will inject past Q&A pairs into its own future prompts — "here's something I told someone earlier" — rather than durable preferences. That's barely useful at best, actively misleading at worst (stale answers re-injected).

3. **Eviction destroys durable memory.** The 10k-entry cap evicts oldest by Timestamp. If a user takes the time to record a real preference ("I play fingerstyle"), and then has 10k chat turns, the preference is evicted by the volume of response logs.

## Recommended fix path

Three options ranked by scope:

### Option A — Stop writing response entries automatically (smallest)

Delete the auto-write in `OnResponseSent`. Persist only when an explicit signal is present (e.g., a hook-tagged "save this" or an MCP tool call). Pro: simplest, eliminates the conflation. Con: no transcript log at all — debuggability drop.

### Option B — Split the store (medium)

Two stores backing two purposes:
- `MemoryStore` — durable memory (fact, preference, focus). Smaller, slower-growing, curator-eligible.
- `ChatTranscriptStore` — Q&A logs (response). High-volume, separate file path, never queried by retrieval, IS the input for Phase 2 of the curator's `IChatTranscriptStore` slot.

Pro: clean separation, matches the curator's existing two-input shape. Con: a new file, migration story for existing `~/.ga/memory.json` (mostly response entries that should move to the transcript store).

### Option C — Smarter extraction (largest)

`OnResponseSent` calls a small LLM ("did this response surface a durable fact about the user?") and only persists when yes. Pro: matches what ChatGPT's "improved memory" does. Con: another LLM call per response — cost + latency, and another model accuracy dependency.

**Recommendation: Option B.** Aligns with the curator's existing architecture (transcript slot is already in `IChatTranscriptStore`). Lowest-friction migration. Sets up Option C as a future enhancement (the extraction step runs against the transcript store, writes high-confidence extractions to the memory store).

## Workaround until the fix lands

PR #171 adds a default type filter to `ga-memory curate` — skips `type=response` by default. Operator can opt in with `--include-responses` if they really want to curate transcripts. This is a **CLI-side workaround**, not the architectural fix.

## Open follow-up

- Audit what entry types the chatbot actually accumulates in production (probably still 100% response). If true → urgent for B.
- Inventory other callers of `MemoryStore.Write` to see if any already write durable types. If yes → preserving those is a Option B migration constraint.
- Decide whether to migrate existing response entries to a new transcript store on first-run, or just leave them (10k cap will age them out).

## Why this wasn't caught earlier

The audit-style test coverage (`MemoryHookSessionPlumbingTests`, `MemoryMcpToolsSC001Tests`) verifies *isolation* and *safety* — that one session's writes don't leak to another, that the SC-001 origin tag is honored. None of it asked the higher-level question: **is the hook writing the right *kind* of data?** That's exactly the kind of architectural concern a Dreams-style curator surfaces by running against the actual store. **Ship-and-verify worked as intended.**
