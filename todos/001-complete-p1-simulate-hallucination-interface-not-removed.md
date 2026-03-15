---
status: pending
priority: p1
issue_id: "001"
tags: [code-review, security, chatbot, orchestration]
dependencies: []
---

# P1: `simulateHallucination` parameter lives in IGroundedNarrator interface — not removed by plan's fix

## Problem Statement

The plan (Phase 2.5, Task 1) proposes hardcoding `bool forceHallucination = false` in `SpectralRagOrchestrator`. This only removes the local check at one call site. The attack surface — the `simulateHallucination` parameter on the public interface `IGroundedNarrator.NarrateAsync` — remains intact. Any caller that injects `IGroundedNarrator` directly can still pass `simulateHallucination: true`.

## Findings

- **`IGroundedNarrator.cs` line 15**: `Task<string> NarrateAsync(string query, List<CandidateVoicing> candidates, bool simulateHallucination = false);`
- **`OllamaGroundedNarrator.cs` line 26**: accepts and acts on the flag
- **`ExtensionsAINarrator.cs` line 40**: also accepts the flag
- **`MockGroundedNarrator.cs` line 32** (tests): actively injects a fabricated chord symbol when flag is true
- The plan's proposed fix (`bool forceHallucination = false` in the orchestrator) only patches one consumer, not the interface

## Proposed Solutions

### Option A: Remove parameter from interface and all implementations (Recommended)
Remove `bool simulateHallucination` from `IGroundedNarrator.NarrateAsync`, all concrete implementations, and `MockGroundedNarrator`. Rewrite the hallucination simulation test in `GuardrailTests.cs` using a dedicated test double that hard-codes an invalid chord.
- **Pros**: Eliminates the attack surface entirely; cleaner interface
- **Cons**: Requires touching 4 files + 1 test rewrite
- **Effort**: Small (1-2h)
- **Risk**: Low — test change is mechanical

### Option B: Mark parameter `[Obsolete("Debug only — do not use in production")]`
Keep the parameter but mark it obsolete to generate compile warnings at every call site, then remove in a follow-up.
- **Pros**: Non-breaking; surface all callers before removing
- **Cons**: Still callable; does not close the surface
- **Effort**: Trivial
- **Risk**: Low — but incomplete fix

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Apps/GaChatbot/Abstractions/IGroundedNarrator.cs`, `Apps/GaChatbot/Services/OllamaGroundedNarrator.cs`, `Apps/GaChatbot/Services/ExtensionsAINarrator.cs`, `Tests/` mock narrators, `Tests/GuardrailTests.cs`
- **Phase in plan**: Phase 2.5 Task 1 (must fix before extraction)

## Acceptance Criteria
- [ ] `IGroundedNarrator.NarrateAsync` has no `simulateHallucination` parameter
- [ ] All concrete implementations compile without the parameter
- [ ] `GuardrailTests` tests pass using a test-double narrator (not the production flag)
- [ ] No callers pass `simulateHallucination: true` anywhere in the codebase

## Work Log
- 2026-03-03: Identified by security-sentinel review agent

## Resources
- Plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 2.5 Task 1
- Source: `Apps/GaChatbot/Abstractions/IGroundedNarrator.cs:15`
