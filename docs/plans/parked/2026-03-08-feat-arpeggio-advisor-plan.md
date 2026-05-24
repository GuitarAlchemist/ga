---
title: "feat: Arpeggio Advisor Skill (Which arpeggio fits this chord progression?)"
type: feat
status: active
date: 2026-03-08
---

# feat: Arpeggio Advisor Skill

## Overview

Given a chord progression (e.g., `Am F C G`), suggest the arpeggios and scales that work over each chord with mode names and target notes. The feature is surfaced as an `IOrchestratorSkill` in the chatbot orchestrator, as an AG-UI custom event `ga:arpeggio-map` for frontend clients, and as a new `ga arpeggio` CLI command.

## Problem Statement

A guitarist asks: "Am F C G — which arpeggio fits each chord?" or "What scales work over my progression?" or "How do I solo over Am F C G?".

Currently:
- No `IOrchestratorSkill` handles improvisation/arpeggio/mode queries.
- `KeyIdentificationSkill` fires only on "what key" phrases — it does not address "what to play over."
- `GA.Business.Core.Harmony` contains interval analysis and mode classification logic, but it is not wired to any chatbot skill.
- The existing `ga diatonic` command gives the chord set but does not map each chord to a mode or arpeggio.
- The RAG/Ollama path provides non-deterministic, unreliable scale suggestions.

## Proposed Solution

Implement an `ArpeggioAdvisorSkill` in `Common/GA.Business.ML/Agents/Skills/` that:

1. Triggers on improvisation/arpeggio/mode phrases with a detectable chord sequence.
2. Calls `KeyIdentificationService.Identify(chords)` to determine the key deterministically.
3. For each chord, calls `ga diatonic` command output logic (via the registered `domain.diatonicChords` closure) to map the chord to its Roman numeral function and associated mode.
4. Uses interval analysis from `GA.Business.Core.Harmony` to identify compatible scales/modes per chord.
5. Passes the pre-computed per-chord data to an LLM prompt that formats a guitarist-friendly per-chord breakdown.
6. Emits an AG-UI `ga:arpeggio-map` custom event for frontend rendering.
7. Registers a `domain.arpeggioAdvisor` closure in `GaClosureRegistry` so the CLI can invoke it.

The skill follows the same hybrid pattern as `KeyIdentificationSkill` and `ProgressionCompletionSkill`: domain computation (key, mode mapping, interval analysis) is deterministic; the LLM is used only to format and explain the pre-computed result in guitarist-friendly language.

## Technical Approach

### New Files

| File | Purpose |
|---|---|
| `Common/GA.Business.ML/Agents/Skills/ArpeggioAdvisorSkill.cs` | `IOrchestratorSkill` implementation |

### Files to Change

| File | Change |
|---|---|
| `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` | Register `ArpeggioAdvisorSkill` as scoped |
| `Apps/GaCli/Program.fs` | Add `ga arpeggio <chords...>` command wired to `domain.arpeggioAdvisor` closure |

### Skill Registration

```csharp
// In GaPlugin.cs — add alongside KeyIdentificationSkill and ProgressionCompletionSkill
services.AddScoped<ArpeggioAdvisorSkill>();
services.AddScoped<IOrchestratorSkill>(sp => sp.GetRequiredService<ArpeggioAdvisorSkill>());
```

### CanHandle Triggers

```csharp
private static readonly Regex ArpeggioTrigger = new(
    @"\b(arpeggio|arpeggios|solo over|improvise over|what (scale|mode) (should i|do i|to) (play|use)|what to play over|scales? (for|over|on)|mode (for|over)|lick over)\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

private static readonly Regex ChordSequencePattern = new(
    @"\b[A-G][b#]?(?:m|maj|dim|aug|7|maj7|m7|m7b5|dim7)?\b",
    RegexOptions.Compiled);

public bool CanHandle(string message) =>
    ArpeggioTrigger.IsMatch(message) &&
    ChordSequencePattern.Matches(message).Count >= 1;
```

### Domain Pipeline

```
input: "Am F C G — which arpeggio fits each chord?"
  ↓
KeyIdentificationService.ExtractChords(message)    → ["Am", "F", "C", "G"]
KeyIdentificationService.Identify(chords)           → A minor (4/4) / C major (4/4)
domain.diatonicChords("A", "minor")                → [Am, Bm7b5, C, Dm, Em, F, G]
  ↓ for each chord, compute:
    Roman numeral in key (i, bVI, bIII, bVII)
    Associated mode (Aeolian, Lydian, Ionian, Mixolydian)
    Arpeggio pattern (chord tones: 1–3–5 or 1–3–5–7)
    Compatible pentatonic (Am pentatonic, F major pentatonic…)
  ↓
LLM prompt (per-chord data grounded) → formatted table + guitarist tips
  ↓
AgentResponse + ga:arpeggio-map event
```

### Mode-to-Chord Mapping

The skill uses a static, deterministic mapping from Roman numeral degree to mode name within the natural major / natural minor scale. This requires no external service calls:

| Degree (major) | Mode | Degree (minor) | Mode |
|---|---|---|---|
| I | Ionian | i | Aeolian |
| ii | Dorian | ii° | Locrian |
| iii | Phrygian | bIII | Ionian |
| IV | Lydian | iv | Dorian |
| V | Mixolydian | v | Phrygian |
| vi | Aeolian | bVI | Lydian |
| vii° | Locrian | bVII | Mixolydian |

### Per-Chord Output Schema

```json
{
  "event": "ga:arpeggio-map",
  "key": "A minor",
  "chords": [
    {
      "chord": "Am",
      "roman": "i",
      "arpeggio": "Am (A–C–E)",
      "mode": "Aeolian",
      "intervals": ["1", "b3", "5"],
      "suggestedPattern": "A C E A C E (ascending)"
    },
    {
      "chord": "F",
      "roman": "bVI",
      "arpeggio": "F major (F–A–C)",
      "mode": "Lydian",
      "intervals": ["1", "3", "5"],
      "suggestedPattern": "F A C F A C (ascending)"
    }
  ]
}
```

Frontend clients subscribed to `ga:arpeggio-map` can render a per-chord table with mode badges and suggested fretboard positions.

### LLM Prompt Structure

The prompt provides:
- The chord progression and detected key.
- A pre-computed per-chord table (roman numeral, mode, arpeggio tones) — the LLM must not override these.
- Instructions to present the table in readable markdown, add 2–3 guitarist tips (target chord tones on beat 1, connect positions with pentatonic).
- JSON output schema matching `AgentResponse` format.

### CLI Command

```
ga arpeggio Am F C G
```

Output:

```
Progression: Am – F – C – G  (key: A minor)

Chord  | Roman | Arpeggio        | Mode        | Target Notes
-------|-------|-----------------|-------------|-------------
Am     | i     | Am (A–C–E)      | Aeolian     | A  C  E
F      | bVI   | F maj (F–A–C)   | Lydian      | F  A  C
C      | bIII  | C maj (C–E–G)   | Ionian      | C  E  G
G      | bVII  | G maj (G–B–D)   | Mixolydian  | G  B  D

Tip: Target A on beat 1 of Am and C on beat 1 of C — they share the A minor pentatonic.
```

### Pattern Reference

- `KeyIdentificationSkill.cs` — same hybrid domain+LLM pattern.
- `ChordSubstitutionSkill.cs` — trigger regex structure and `AgentResponse` construction.
- `AgentSkillBase.cs` — `ChatAsync` + `ParseStructuredResponse` helpers.

## Acceptance Criteria

- [ ] `"Am F C G — which arpeggio should I use?"` returns a per-chord table with arpeggio, mode, and target notes for each chord.
- [ ] `"How do I solo over Am F C G?"` also triggers the skill (same trigger path).
- [ ] Detected key is included in the response.
- [ ] Mode names are correct for the detected key (Aeolian for i, Lydian for bVI, etc.).
- [ ] AG-UI event `ga:arpeggio-map` is present in `AgentResponse.Data` as valid JSON.
- [ ] `ga arpeggio Am F C G` prints the per-chord table to stdout.
- [ ] `KeyIdentificationSkill` continues to fire on "what key am I in?" (no trigger collision).
- [ ] `ProgressionCompletionSkill` is unaffected (different trigger keywords).
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings in touched files.
- [ ] Unit tests added in `Tests/Common/GA.Business.ML.Tests/Unit/` covering:
  - [ ] `CanHandle` returns `true` for arpeggio/solo/mode phrases with at least one chord.
  - [ ] `CanHandle` returns `false` for key-identification and substitution queries.
  - [ ] Mode mapping returns correct mode names for each Roman numeral in major and minor.

## Dependencies & Prerequisites

| Dependency | Status |
|---|---|
| `KeyIdentificationService` | Exists — `Common/GA.Business.ML/Agents/KeyIdentificationService.cs` |
| `domain.diatonicChords` closure | Exists — registered by `DomainClosures` in `GaClosureRegistry` |
| `GA.Business.Core.Harmony` interval analysis | Exists — Layer 3 analysis project |
| `AgentSkillBase` | Exists — `Common/GA.Business.ML/Agents/AgentSkillBase.cs` |
| `GaPlugin` (DI registration point) | Exists — `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` |
| `IChatClient` (Ollama / Anthropic) | Injected via DI — no change needed |
| `ga arpeggio` CLI command | New — F# dispatch case in `Program.fs` |

## Implementation Tasks

- [ ] Create `ArpeggioAdvisorSkill.cs` in `Common/GA.Business.ML/Agents/Skills/`.
  - [ ] Implement `CanHandle(string message)` with `ArpeggioTrigger` + chord-count guard.
  - [ ] Implement static `RomanToMode` lookup table for major and natural minor scales.
  - [ ] Implement `ExecuteAsync`: extract chords → detect key → compute per-chord Roman/mode/arpeggio → build prompt → call LLM → parse response.
  - [ ] Serialize `ga:arpeggio-map` into `AgentResponse.Data`.
- [ ] Register in `GaPlugin.cs` (scoped, `IOrchestratorSkill`).
- [ ] Add `ga arpeggio <chords...>` case in `Apps/GaCli/Program.fs`.
- [ ] Write unit tests in `Tests/Common/GA.Business.ML.Tests/Unit/ArpeggioAdvisorSkillTests.cs`.
- [ ] Run `dotnet build AllProjects.slnx -c Debug` and `dotnet test AllProjects.slnx` — both must pass.

## Sources & References

- `KeyIdentificationSkill.cs`: `Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs` — hybrid domain+LLM pattern to follow.
- `KeyIdentificationService.cs`: `Common/GA.Business.ML/Agents/KeyIdentificationService.cs` — key and diatonic set detection.
- `ChordSubstitutionSkill.cs`: `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs` — trigger regex and `CanHandle` structure.
- `Program.fs`: `Apps/GaCli/Program.fs` — CLI dispatch pattern (see `cmdDiatonic`, `cmdAnalyze`).
- `GaPlugin.cs`: `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` — DI registration point.
- Progression Completion companion plan: `docs/plans/2026-03-08-feat-progression-completion-plan.md`.
- Voicing by Hand Comfort companion plan: `docs/plans/2026-03-08-feat-voicing-by-hand-comfort-plan.md`.
