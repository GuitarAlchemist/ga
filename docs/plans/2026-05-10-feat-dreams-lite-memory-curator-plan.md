---
title: Dreams-lite — periodic curation pass over the chatbot MemoryStore
date: 2026-05-10
type: feat
reversibility: two-way door (curation produces a candidate store; original is never mutated)
revisit_trigger: when (a) Anthropic Dreams beta access lands AND our store fits its size budget, OR (b) curator output quality drops below the manual-review-acceptance bar three runs in a row
status: draft
---

# Dreams-lite — periodic curation pass over the chatbot MemoryStore

## Why this exists

Anthropic's **Dreams** Research Preview (`dreaming-2026-04-21` beta) addresses a real pathology: managed-agent memory stores accumulate duplicates, contradictions, and stale entries over many sessions, and there's no built-in compaction step. Dreams runs an offline pipeline that reads a memory store + up to 100 session transcripts and produces a *new* store with duplicates merged, stale entries replaced, and emergent insights surfaced. The input is never mutated.

Our `MemoryStore` (`Common/GA.Business.ML/Agents/Memory/MemoryStore.cs`) will hit the same pathology the moment `Memory:EnrichOnRetrieve=true` flips on. We already see the shape of the problem in two-day-old conversations: "user plays Strat" vs "user plays Stratocaster" gets stored as two facts; preferences that get overridden later (`"focus: bebop" → "focus: fingerstyle"`) both linger.

Dreams the API isn't applicable directly because (a) it operates on Anthropic *managed-agents* memory stores (we use a file-backed `MemoryStore` instead) and (b) it's gated behind a Research Preview access form. But the **shape** of the pipeline is reproducible against any store, and we already have the dependencies (Anthropic API client via Microsoft.Extensions.AI, Claude Opus 4.7 access, JSON serialization).

So: ship "dreams-lite" — a local analog wired to our existing infrastructure. Get the compounding-knowledge benefit now. Migrate to managed Dreams later if/when we adopt managed-agents API.

## What it does

A single command (or scheduled L2 step) that:

1. Reads `~/.ga/memory.json` (current `MemoryStore` on disk).
2. Reads the last N chat sessions from a transcript log (TBD — for v0.1, mock with hand-curated fixtures; v0.2 plumbs from production once chat-transcript logging exists).
3. Sends both to Claude (Opus 4.7 or Sonnet 4.6) with a curation prompt that mirrors Dreams' contract:
   - Merge duplicate entries (same fact stated different ways).
   - Replace stale or contradicted entries with the latest value.
   - Surface emergent insights (patterns across sessions).
   - Never invent facts not grounded in inputs.
4. Writes the candidate v2 store to `~/.ga/memory.v2-candidate-<timestamp>.json` (or a configurable path) — **never overwrites** the live store.
5. Emits a diff summary (counts: kept, merged, replaced, new, dropped) and a structured ledger entry.
6. Operator reviews → `ga-memory promote <candidate-path>` swaps the live store atomically (with backup of the previous one).

The shape mirrors Dreams' "lifecycle" semantics: pending → running → completed/failed, with an `outputs[].memory_store_path` rather than a managed `memory_store_id`. Operator review is the audit gate. Same two-way door guarantee.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  Common/GA.Business.ML/Agents/Memory/Curator/                       │
│  ├─ MemoryCurator.cs              ← orchestrates the pass           │
│  ├─ MemoryCurationRequest.cs      ← input record (store + sessions) │
│  ├─ MemoryCurationResult.cs       ← output record (candidate + diff)│
│  ├─ CurationPromptBuilder.cs      ← builds the system + user prompt │
│  └─ CurationDiff.cs               ← kept/merged/replaced/new/dropped│
├─────────────────────────────────────────────────────────────────────┤
│  Apps/GaMemoryCli/ (new thin console host)                          │
│  ├─ ga-memory curate              ← run pipeline, write candidate   │
│  ├─ ga-memory diff <candidate>    ← show what changed               │
│  └─ ga-memory promote <candidate> ← atomic swap with backup         │
│  (separate project, NOT inside GaChatbotCli — curation has no       │
│   IHarmonicChatOrchestrator dependency, only IChatClient + Store)   │
├─────────────────────────────────────────────────────────────────────┤
│  state/quality/memory-curator/<date>/                               │
│  ├─ run-<id>.json                 ← ledger entry (input/output/diff)│
│  └─ memory.v2-candidate-<id>.json ← candidate store                 │
└─────────────────────────────────────────────────────────────────────┘
```

Single dependency: existing `IChatClient` from `Microsoft.Extensions.AI` (we already inject Claude Opus 4.7 via this in `Common/GA.Business.ML/Agents/`).

## Prompt contract

The curation prompt is the load-bearing part. Draft (subject to iteration):

```
SYSTEM:
You are a memory curator. You receive an existing MemoryStore (JSON array of
MemoryEntry records: Key, Type, Content, Tags, Timestamp, SessionId) and
optional past chat transcripts. Produce a NEW MemoryStore that:
1. Merges duplicate entries (same fact stated different ways).
2. Replaces entries contradicted by later evidence (use Timestamp).
3. Surfaces new memory entries supported by recurring patterns in transcripts.
4. NEVER invents facts not grounded in inputs.
5. Preserves SessionId scope — never moves a session-scoped entry to global
   or vice versa.

Return a JSON object: { entries: [...], diff: { kept, merged, replaced, new, dropped } }.
Every change in `diff` must reference the input Keys it derived from.

USER:
<existing store JSON>
<session transcripts JSON>
<operator instructions (optional, ≤2KB)>
```

The `diff` requirement is the audit trail — every output entry traces to inputs. Operator review compares input to output line-by-line via the `diff` keys, not by re-reading the whole store.

## Acceptance criteria (Phase 1 — v0.1)

- [ ] `MemoryCurator.CurateAsync(MemoryCurationRequest)` returns `MemoryCurationResult` with a candidate store and structured diff.
- [ ] CLI `ga-memory curate` runs the pipeline against the live `~/.ga/memory.json` and writes a candidate file. **Never mutates the input file.**
- [ ] CLI `ga-memory promote <candidate>` swaps stores atomically with a `.bak` of the prior live store.
- [ ] Ledger entry written to `state/quality/memory-curator/<date>/run-<id>.json` (schema: `memory-curator-run.schema.json` — sibling to `gate-ledger.schema.json`).
- [ ] Unit tests with fixture stores covering: duplicate merge, contradiction resolution, session-scope preservation, refusal to invent.
- [ ] Smoke test against a real `~/.ga/memory.json` snapshot — operator confirms output quality is "merge-worthy" before we wire any automation.

Not in scope for v0.1: production transcript ingestion (mock fixtures only); auto-promote; scheduled runs; UI surface; cross-session sensitive-data redaction. Each is its own phase.

## Phasing

| Phase | Scope | Out of scope |
|---|---|---|
| **0 — this plan** | One-page design + sign-off | Code |
| **1 — v0.1 spike** | `MemoryCurator` + CLI + fixture tests + manual smoke | Production transcripts; scheduling |
| **2 — v0.2 transcripts** | Plumb real chat-transcript log into curation input | Auto-promote |
| **3 — v0.3 scheduled** | Optional L2 `/chatbot-iterate` step (weekly) with explicit operator-review gate before any promote | Removing the review gate |
| **4 — v0.4 production** | Quality metric (curator-acceptance rate); SLOs; promote-on-green threshold | — |

## One-way doors

None at v0.1 — curator writes to a candidate file; original store untouched; promote is reversible via the `.bak`. The candidate JSON file is local; nothing leaves the workspace unless the operator manually shares it.

## Open questions (need answers before Phase 1)

1. **Model choice.** Opus 4.7 (better synthesis, ~5× cost) or Sonnet 4.6 (faster, cheaper, may miss subtle merges)? Recommendation: start with Sonnet 4.6 for cost; promote to Opus 4.7 if the diff quality is poor on the first three runs.
2. **Where to inject the transcript dependency.** For v0.1 we use fixture JSON files; for v0.2 we need a `ChatTranscriptStore` that captures `ChatHookContext` history. Do we add this proactively in v0.1 even though it's not wired? Recommendation: yes — header-only interface so v0.2 is a drop-in.
3. **Operator-review UI.** Plain CLI diff for v0.1. Should we plumb to Prime Radiant dashboard in v0.3? Recommendation: defer to v0.4 — terminal review is fine while we're calibrating the prompt.

## Dreams-direct migration path (parallel option C)

If Anthropic Dreams beta access lands and our `MemoryStore` size fits the input budget, we can also try the real Dreams API against `MEMORY.md` (Claude Code's auto-memory at `C:/Users/spare/.claude/projects/.../memory/`). That doesn't replace this plan — it's a *second* application of the same idea to a different memory store (Claude Code's, not GA's chatbot's). The prompt contract designed here is reusable as a Dreams `instructions` value.

## Refs

- Dreams docs: https://platform.claude.com/docs/en/managed-agents/dreams
- Adjacent: `Common/GA.Business.ML/Agents/Memory/MemoryStore.cs` (target store)
- Adjacent: `Common/GA.Business.ML/Agents/Hooks/MemoryHook.cs` (writes that accumulate)
- Adjacent: `docs/solutions/architecture/2026-05-07-process-wide-memory-store-leaks-into-anonymous-prompts.md` (why the store has session scoping)
- Related: `BACKLOG.md` Chatbot Track (P1 items around memory)
