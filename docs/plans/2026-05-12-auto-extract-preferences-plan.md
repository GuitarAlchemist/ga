---
title: Auto-extract preferences from conversation — design plan
date: 2026-05-12
status: draft
type: feature-design
reversibility: two-way-door (feature-flagged, off by default)
revisit-trigger: when ≥3 users explicitly ask for "auto-remember" behavior, OR when MemoryStore.Stats shows a chronic gap between session length and durable-memory entry count
related:
  - docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
  - PR #172 (option C audit)
  - PR #185 (Data forward — the regression-pin lesson)
  - PR #192 (orchestrator-level priority gate)
---

# Auto-extract preferences from conversation

## Premise

Today's durable-memory writes require an **explicit user phrasing** —
`remember that I prefer drop-2 voicings`, `save this: my main guitar is
a Telecaster`, etc. The `RememberThisSkill` is intentionally
explicit-only per the PR #172 audit. That's the safe default and it's
working.

But users implicitly state preferences all the time:

> "I'm working through Charlie Parker's Omnibook this month and my
> hands are tiring out on the dense chord changes."

Three implicit facts in one sentence (focus = Charlie Parker, focus =
this month, problem = fatigue on dense chord changes), and none of them
get persisted unless the user wraps them in a remember-phrasing.

**This plan proposes:** an opt-in, feature-flagged extraction service
that detects implicit preferences/focus statements and surfaces them as
**candidates** for the user to confirm — never silently persists.

---

## Why this was deferred

The PR #172 audit identified option C (auto-extraction) as a
trust-leak vector:

> The chatbot inferring "the user prefers X" and silently writing it
> is a trust-leak vector that bypasses both SC-001's MCP-write gate
> and the session-scoping defense.

The two specific risks:

1. **SC-001 bypass** — `MemoryWriteHook` refuses writes from
   non-allowed types and refuses writes without an explicit session
   scope. An auto-extractor that wires directly to `MemoryStore.Write`
   would route around that gate.
2. **Trust** — if the chatbot extracts and persists "the user prefers
   X" based on a misread of the conversation, the user has no way to
   know it happened and no way to undo it without explicit retrieval +
   manual cleanup.

**The plan must address both** before any extraction code lands.

---

## Threat model (revised, post-#192)

Three additional threats surfaced since the original audit:

3. **Session-scoping bypass on extraction** — if extraction runs in a
   background queue separated from the user's request lifecycle,
   `ChatHookContext.SessionId` is not guaranteed to be available. A
   global write would land the entry in the wrong partition. (Same
   class of bug as the PR #157 SessionId plumbing.)
4. **Orchestrator dispatch order** (lesson from PR #192) — any new
   extraction path that fires from the orchestrator's hook chain MUST
   yield to explicit memory-write requests if both fire on the same
   message. Otherwise we re-introduce the priority bug PR #192 fixed.
5. **Multi-LLM review escape hatch** — extraction decisions should be
   reviewable in the chat trace. Without that surface, a silent
   regression in the extraction-classifier (e.g. false-positive on
   "I'm working on stuff" classified as a focus) would be invisible
   until it pollutes durable memory.

---

## Design (proposed, not implemented)

### Storage type — `candidate-preference`

New `MemoryEntry.Type` value: `candidate-preference` (or
`candidate-focus`, `candidate-fact`). NOT added to
`MemoryWriteHook.AllowedTypes` — the hook refuses to persist these
under the normal path. Candidate entries live in a separate field on
`AgentResponse` and never reach `MemoryStore.Write` unless the user
confirms.

This keeps the SC-001 defense intact: there's no code path where a
new write reaches the store without going through the existing
allowed-types gate.

### Extraction trigger

A new `IChatHook` — `CandidatePreferenceExtractionHook` — that fires
on `OnAfterSkill`. It:

1. Reads `ChatHookContext.OriginalMessage`.
2. Runs a lightweight LLM call against a calibrated prompt
   (`grounded/auto-extract-preferences.md`) that returns either
   `null` or a structured `CandidatePreference` record.
3. If non-null, attaches the candidate to `ChatHookContext.Trace` for
   downstream UI surfacing.

**Cost gate:** the hook is gated by:
- `AutoExtract:Enabled = false` (default off, opt-in per host)
- `AutoExtract:MinTurnInterval = 5` (don't re-extract every turn)
- `AutoExtract:MaxConfidence = 0.95` (refuse high-confidence
  extractions — those are the ones most likely to be silently wrong)

### User confirmation flow

When a candidate is attached to the response, the chat UI surfaces a
banner:

> 💾 I noticed you mentioned **working on Charlie Parker's Omnibook**.
> Should I remember that as a current focus?
> [Yes, remember it] [Not now] [Don't ask again this session]

Picking "Yes, remember it" fires a normal `RememberThisSkill` call
with the candidate's content reconstructed as an explicit remember
phrasing. The write goes through `MemoryWriteHook` like any other —
SC-001 defense applies.

"Not now" discards the candidate.

"Don't ask again this session" sets a session-scoped flag in
`MemoryHook`'s context that suppresses extraction for the rest of the
session.

### Observability

Every extraction decision (positive AND negative) emits a trace step:
```
auto-extract.evaluated session=<id> turn=<n>
  prompt-length=120
  candidates=1
  highest-confidence=0.72
  decision=surface-as-banner
```

This is the surface that lets multi-LLM review catch regressions in
the classifier prompt. A spike in `decision=auto-suppress
high-confidence` or a drop in `candidates=0` for sessions that
historically generated candidates is a signal to re-tune the prompt.

### Telemetry baselines

Before merging:
- Baseline `MemoryStore.Stats(sessionId)` distribution from 30 days of
  anonymous chatbot traffic — establishes the natural rate of
  preference accumulation.
- Baseline `RememberThisSkill` invocation rate — the explicit baseline
  this feature is supposed to augment, not replace.

After merging (with flag off, instrumentation-only):
- Track `auto-extract.candidates` rate for 7 days. Establish a
  calibration curve: what fraction of candidates would the user
  accept?

Only flip the flag on after the calibration curve looks stable and
the LLM-judge precision passes 80% on a manually-labeled sample of
candidate extractions.

---

## Reversibility

**Two-way door** at the feature-flag level. Setting
`AutoExtract:Enabled = false` (the default) is a complete revert —
no extraction runs, no candidates surface, no trace steps emit.

**One-way door** if the storage type `candidate-preference` ever
gets added to `MemoryWriteHook.AllowedTypes`. That would change the
SC-001 defense surface and would need a separate sign-off. The plan
explicitly proposes keeping `candidate-preference` OUT of the
allowed-types list — the user's confirmation reconstructs a normal
remember-request, which IS in the allowed list.

---

## What this plan does NOT propose

- **Silent persistence.** Candidates never write to MemoryStore
  without user confirmation. Period.
- **Background extraction.** Extraction runs synchronously in the
  hook chain, in the same request lifecycle as the user's message.
  No queue, no worker, no separate process. Keeps SessionId scoping
  tractable.
- **Cross-session extraction.** Each session's extraction state
  (suppression, candidate history) lives in `ChatHookContext`, not
  in `MemoryStore`. No cross-session leak vector.
- **Auto-update on existing entries.** If the user confirms a
  candidate that "updates" an existing memory entry, that's still a
  normal RememberThisSkill write — same idempotency rules
  (content-derived key → overwrite).

---

## Implementation phases

### Phase 0 — design review (THIS DOCUMENT)
Get user authorization on the design surface. **Do not write code
until this lands.**

### Phase 1 — instrumentation only
Implement `CandidatePreferenceExtractionHook` behind
`AutoExtract:Enabled = false` (default off). The hook runs the
extraction LLM call and emits trace steps but does NOT surface
candidates in the UI. This gives 7 days of calibration data.

### Phase 2 — UI surfacing
Add the candidate banner in the chat UI. Still gated by the flag.
Manually labeled precision/recall measurement against a sample of
real chatbot traffic.

### Phase 3 — flag flip
Default to on for new sessions only (existing sessions keep their
current behavior). Monitor `auto-extract.confirmed-rate` for 14
days. If it drops below 30%, the classifier prompt needs work and
the flag flips back off.

### Phase 4 — full default-on
After Phase 3 passes the 14-day window with confirmed-rate ≥ 30%.

---

## Why "ship the design first"

The PR #185 → PR #192 sequence taught a lesson:
*the costly bugs are the ones that touch the orchestrator's dispatch
chain in subtle ways.* This feature touches:
- The hook chain (new hook with new trace data)
- The orchestrator's priority order (must yield to explicit
  RememberThisSkill)
- The MemoryStore allowed-types gate (extends without breaking)
- The UI/chat-surface contract (new banner type)

Getting the design right before any code lands costs hours; getting
it wrong costs weeks of regression and trust-rebuild.

This plan asks for **explicit user authorization** before Phase 1
implementation begins.

---

## Open questions

1. **Which embedder / model for the extraction call?** Same as
   `SemanticIntentRouter` (Ollama `nomic-embed-text`) won't work —
   classification needs a chat-capable model, not a pure embedder.
   Options: a small local chat model (llama3.2:3b), or a single
   `IChatClient` call against the host's default chat model.
   Recommended: use the host's `IChatClient` so latency stays in
   line with normal chat turns.
2. **Calibration corpus.** Phase 1 needs ~100 labeled prompts where
   each prompt is paired with the expected candidate (or "no
   candidate"). Who labels them? Recommendation: extract the labeling
   work into a separate doc and let multi-LLM review propose
   candidates against a sample of recent chatbot traffic.
3. **Per-user opt-out.** Some users will never want auto-extraction.
   The session-scoped "don't ask again" flag handles the in-session
   case; do we also need a per-user persistent opt-out? Probably yes,
   but adds a new piece of state outside `MemoryStore` (or inside, as
   a `user-config` entry type). Defer to Phase 2 if needed.

---

## Decision point

**Authorization requested for Phase 1 instrumentation only.** Phase 1
ships:
- `CandidatePreferenceExtractionHook` (instrumentation-only, flag off)
- The `grounded/auto-extract-preferences.md` calibrated prompt
- Trace step emission (`auto-extract.evaluated`)
- Telemetry baseline collection
- NO UI changes
- NO writes to MemoryStore

Phase 2+ require separate sign-off after Phase 1's calibration data
is in hand.
