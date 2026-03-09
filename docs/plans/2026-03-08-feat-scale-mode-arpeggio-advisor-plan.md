---
title: "feat: Scale/Mode/Arpeggio Advisor Skill"
type: feat
status: active
date: 2026-03-08
---

# feat: Scale/Mode/Arpeggio Advisor Skill

## Overview

`KeyIdentificationSkill` already identifies the key from a chord progression. The next natural question is: **"What should I play over it?"** — which arpeggio fits Am, which mode works over F C G?

The `GaArpeggioSuggestions` MCP tool in `GuitaristProblemTools.cs` already computes this. It is not wired to any chatbot skill yet. This plan wires it up via a `SKILL.md`-driven skill that calls the tool and returns a guitarist-friendly per-chord breakdown.

## Problem Statement

A guitarist types: "Am F C G — what should I solo over?" or "which arpeggio fits each chord in my progression?"

Currently:
- `KeyIdentificationSkill` fires on "what key" phrases — does NOT fire on "what to play", "what arpeggio", "what mode"
- `GaArpeggioSuggestions` MCP tool exists and returns correct data — but is only accessible to external MCP clients (Claude Code), not the chatbot
- The RAG/Ollama path produces non-deterministic suggestions with no guarantee of harmonic correctness

## Proposed Solution

A `SKILL.md`-driven skill (`.agent/skills/arpeggio-advisor/SKILL.md`) that:
1. Triggers on improvisation/arpeggio/mode phrases
2. Calls `GaArpeggioSuggestions(chords[], key?)` → gets per-chord arpeggio + mode mapping
3. Calls `GaAnalyzeProgression(chords)` → confirms key
4. Returns a formatted table: chord → arpeggio → mode → target notes

The `SkillMdDrivenSkill` infrastructure handles everything else (Anthropic API, tool loop, MCP wiring).

## Technical Approach

### SKILL.md Frontmatter

```yaml
---
Name: "Arpeggio Advisor"
Description: "Suggests arpeggios, modes, and target notes for improvisation over a chord progression"
Triggers:
  - "what arpeggio"
  - "what mode"
  - "what scale should i play"
  - "what to solo"
  - "improvise over"
  - "solo over"
  - "what to play over"
  - "practice arpeggio"
---
```

### System Prompt (body)

Instructs Claude to:
1. Extract chord symbols from the message
2. Call `GaArpeggioSuggestions` with the chord array → get per-chord breakdown
3. Call `GaAnalyzeProgression` to confirm detected key and Roman numerals
4. Format the response as a markdown table:
   ```
   Progression in A minor:
   | Chord | Roman | Arpeggio  | Mode     | Target Notes    |
   |-------|-------|-----------|----------|-----------------|
   | Am    | i     | Am pentatonic | Aeolian | A C E           |
   | F     | bVI   | F major   | Lydian   | F A C E         |
   | C     | bIII  | C major   | Ionian   | C E G           |
   | G     | bVII  | G major   | Mixolydian | G B D        |
   ```
5. Add 2-3 sentence guitarist tip (connect positions, target chord tones on strong beats)

### File to Create

- `.agent/skills/arpeggio-advisor/SKILL.md` — skill definition + system prompt

### MCP Tools Used

| Tool | Source | Purpose |
|---|---|---|
| `GaArpeggioSuggestions` | `GuitaristProblemTools.cs` | Per-chord arpeggio + mode + target notes |
| `GaAnalyzeProgression` | `GaDslTool.cs` | Key detection + Roman numerals |
| `GaDiatonicChords` | `GaDslTool.cs` | Diatonic context if needed |

## Acceptance Criteria

- [ ] `dotnet run --project Apps/GaChatbotCli -- "Am F C G — what arpeggio should I use?"` returns a per-chord breakdown
- [ ] Response includes arpeggio name, mode name, and target notes for each chord
- [ ] Response includes detected key
- [x] Skill fires on "solo over", "what mode", "what arpeggio", "improvise over" triggers
- [x] `KeyIdentificationSkill` continues to fire on "what key am I in?" (no collision — different triggers)
- [x] Build passes: `dotnet build AllProjects.slnx -c Debug`

## System-Wide Impact

- **No C# changes** — pure SKILL.md addition, auto-loaded by `SkillMdPlugin`
- **No skill registration changes** — `SkillMdPlugin` discovers it automatically via `triggers:`
- **Dependency:** `ANTHROPIC_API_KEY` required (same as all `SkillMdDrivenSkill` instances)
- **No collision** with `KeyIdentificationSkill` — different trigger keywords

## Sources & References

- `GaArpeggioSuggestions` tool: `GaMcpServer/Tools/GuitaristProblemTools.cs`
- `GaAnalyzeProgression` tool: `GaMcpServer/Tools/GaDslTool.cs`
- Existing SKILL.md with triggers: `.agent/skills/ga/chords/SKILL.md`
- `SkillMdDrivenSkill`: `Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs`
- `SkillMdPlugin` (auto-discovery): `Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs`
- `KeyIdentificationSkill` (reference pattern): `Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs`
