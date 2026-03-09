---
title: "feat: Progression Completion Skill (Help me finish this progression)"
type: feat
status: completed
date: 2026-03-08
---

# feat: Progression Completion Skill

## Overview

Given 2–3 chords from a guitarist, suggest 2–3 natural completions that cadence correctly in the same key. The feature is surfaced via the chatbot orchestrator (as an `IOrchestratorSkill`), via an AG-UI custom event for frontend clients, and via a new `ga complete` CLI command.

## Problem Statement

A guitarist types: "Help me finish this progression — Am F C?" or "I have G D Em, what should come next?" or "Am F C — how do I end this?".

Currently:
- No `IOrchestratorSkill` handles progression continuation queries.
- The RAG/Ollama path produces non-deterministic, harmonically ungrounded completions.
- `ContextualChordService.GetChordsForKeyAsync()` and `KeyIdentificationService` are already in the codebase but are not wired together for this use case.
- There is no AG-UI event for streaming completion suggestions to frontend clients.
- The `ga` CLI has no `complete` command.

## Proposed Solution

Implement a `ProgressionCompletionSkill` in `Common/GA.Business.ML/Agents/Skills/` that:

1. Triggers on continuation/completion phrases and a detected chord sequence.
2. Calls `KeyIdentificationService.Identify(chords)` to detect the key deterministically.
3. Calls `ContextualChordService.GetChordsForKeyAsync()` to retrieve the diatonic set for that key.
4. Builds an LLM prompt grounded in the diatonic set, asking Claude to suggest 2–3 completions with cadence explanation.
5. Emits an AG-UI `ga:completion-suggestions` custom event — an array of `{ chords: string[], explanation: string }` objects — for the frontend to render inline chord diagrams.
6. Registers as a `domain.progressionCompletion` closure in `GaClosureRegistry` so the CLI can invoke it.

The skill follows the same hybrid pattern as `KeyIdentificationSkill`: domain computation (key detection + diatonic set) is deterministic; the LLM is used only to select and explain the completion candidates from the pre-computed diatonic set.

## Technical Approach

### New Files

| File | Purpose |
|---|---|
| `Common/GA.Business.ML/Agents/Skills/ProgressionCompletionSkill.cs` | `IOrchestratorSkill` implementation |

### Files to Change

| File | Change |
|---|---|
| `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` | Register `ProgressionCompletionSkill` as scoped |
| `Apps/GaCli/Program.fs` | Add `ga complete <chords...>` command wired to `domain.progressionCompletion` closure |

### Skill Registration

```csharp
// In GaPlugin.cs — add alongside KeyIdentificationSkill registration
services.AddScoped<ProgressionCompletionSkill>();
services.AddScoped<IOrchestratorSkill>(sp => sp.GetRequiredService<ProgressionCompletionSkill>());
```

### CanHandle Triggers

```csharp
private static readonly Regex CompletionTrigger = new(
    @"\b(finish|complete|end|continue|extend|what comes next|next chord|help me finish|how do i end|what should follow)\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

private static readonly Regex ChordSequencePattern = new(
    @"\b[A-G][b#]?(?:m|maj|dim|aug|7|maj7|m7|m7b5|dim7)?\b",
    RegexOptions.Compiled);

public bool CanHandle(string message) =>
    CompletionTrigger.IsMatch(message) &&
    ChordSequencePattern.Matches(message).Count >= 2;
```

### Domain Pipeline

```
input: "Help me finish Am F C"
  ↓
KeyIdentificationService.ExtractChords(message)   → ["Am", "F", "C"]
KeyIdentificationService.Identify(chords)          → [A minor (3/3), C major (3/3)]
ContextualChordService.GetChordsForKeyAsync("Am")  → [Am, Bm7b5, C, Dm, Em, F, G]
  ↓
LLM prompt (diatonic set grounded) → 2–3 suggestions with cadence type
  ↓
AgentResponse + ga:completion-suggestions event
```

### LLM Prompt Structure

The prompt provides:
- The input chord sequence and detected key(s).
- The full diatonic set (only chords in this set may appear in suggestions).
- Instructions to suggest 2–3 completions, name the cadence type (authentic, half, deceptive, plagal), and give a one-sentence guitarist explanation.
- JSON output schema matching `AgentResponse` format.

### AG-UI Event

The `ExecuteAsync` result includes an `AgentResponse.Data` payload serialized as:

```json
{
  "event": "ga:completion-suggestions",
  "suggestions": [
    { "chords": ["E7"], "explanation": "Authentic cadence (V7–i) — strongest resolution back to Am." },
    { "chords": ["G"], "explanation": "Half cadence (bVII–i loop) — open, floats back to the top." },
    { "chords": ["Dm", "E7"], "explanation": "iv–V7–i turnaround — classic minor-key approach." }
  ]
}
```

Frontend clients subscribed to `ga:completion-suggestions` can render the `chords` arrays as inline VexTab chord diagrams.

### CLI Command

```
ga complete Am F C
```

Output:

```
Progression: Am – F – C  (key: A minor / C major)

Suggested completions:
  1. E7         → authentic cadence (V7 → i)
  2. G           → half cadence (bVII → i loop)
  3. Dm E7       → iv–V7–i turnaround
```

Add to `Program.fs` entry-point dispatch alongside the existing `analyze` command.

### Pattern Reference

- `KeyIdentificationSkill.cs` — same hybrid domain+LLM pattern.
- `ChordSubstitutionSkill.cs` — trigger regex design and `CanHandle` structure.
- `AgentSkillBase.cs` — `ChatAsync` + `ParseStructuredResponse` helpers.

## Acceptance Criteria

- [x] `"Help me finish Am F C"` → returns 2–3 completion options with cadence names.
- [x] `"G D Em — what comes next?"` → detects G major, returns diatonic completions only.
- [x] All suggestions are strictly diatonic to the detected key (no random chord names).
- [x] Response includes the detected key and the Roman numeral of each suggested chord.
- [x] AG-UI event `ga:completion-suggestions` is present in `AgentResponse.Data` as valid JSON.
- [x] `ga complete Am F C` prints the ranked completion list to stdout.
- [x] `KeyIdentificationSkill` continues to fire on "what key am I in?" (no trigger collision).
- [x] `ChordSubstitutionSkill` is unaffected (different trigger keywords).
- [x] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings in touched files.
- [x] Unit tests added in `Tests/Common/GA.Business.ML.Tests/Unit/` covering:
  - [x] `CanHandle` returns `true` for completion phrases with 2+ chords.
  - [x] `CanHandle` returns `false` for unrelated messages.
  - [x] Key detection feeds correct diatonic set into the prompt.

## Dependencies & Prerequisites

| Dependency | Status |
|---|---|
| `KeyIdentificationService` | Exists — `Common/GA.Business.ML/Agents/KeyIdentificationService.cs` |
| `ContextualChordService.GetChordsForKeyAsync()` | Exists — `Apps/ga-server/GaApi/Services/` |
| `AgentSkillBase` | Exists — `Common/GA.Business.ML/Agents/AgentSkillBase.cs` |
| `GaPlugin` (DI registration point) | Exists — `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` |
| `IChatClient` (Ollama / Anthropic) | Injected via DI — no change needed |
| `ga complete` CLI command | New — F# dispatch case in `Program.fs` |

## Implementation Tasks

- [x] Create `ProgressionCompletionSkill.cs` in `Common/GA.Business.ML/Agents/Skills/`.
  - [x] Implement `CanHandle(string message)` with `CompletionTrigger` + chord-count guard.
  - [x] Implement `ExecuteAsync`: extract chords → detect key → fetch diatonic set → build prompt → call LLM → parse response.
  - [x] Serialize `ga:completion-suggestions` into `AgentResponse.Data`.
- [x] Register in `GaPlugin.cs` (scoped, `IOrchestratorSkill`).
- [x] Add `ga complete <chords...>` case in `Apps/GaCli/Program.fs`.
- [x] Write unit tests in `Tests/Common/GA.Business.ML.Tests/Unit/ProgressionCompletionSkillTests.cs`.
- [x] Run `dotnet build AllProjects.slnx -c Debug` and `dotnet test AllProjects.slnx` — both must pass.

## Sources & References

- `KeyIdentificationSkill.cs`: `Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs` — hybrid domain+LLM pattern to follow.
- `ChordSubstitutionSkill.cs`: `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs` — trigger regex design.
- `KeyIdentificationService.cs`: `Common/GA.Business.ML/Agents/KeyIdentificationService.cs` — key detection + diatonic set.
- `GaPlugin.cs`: `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` — DI registration point.
- `Program.fs`: `Apps/GaCli/Program.fs` — CLI command dispatch pattern.
- Arpeggio Advisor companion plan: `docs/plans/2026-03-08-feat-arpeggio-advisor-plan.md`.
