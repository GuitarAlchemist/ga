---
title: "feat: Progression Completion Skill (Help me finish this progression)"
type: feat
status: active
date: 2026-03-08
---

# feat: Progression Completion Skill

## Overview

A `SKILL.md`-driven skill that answers: **"Help me finish this progression"** — given a partial chord progression, suggest logical next chords using diatonic function, voice leading, and common harmonic idioms.

The `GaProgressionCompletion` MCP tool in `GuitaristProblemTools.cs` already implements the core harmonic engine. It is not wired to any chatbot skill. This plan wires it up with a SKILL.md and zero new C# code.

## Problem Statement

A guitarist types: "Am F C — what comes next?" or "Help me finish this progression" or "I have G D Em, how do I end it?".

Currently:
- No skill handles completion/continuation queries
- `GaProgressionCompletion` MCP tool exists and computes likely next chords — but is only accessible to external MCP clients, not the chatbot
- The RAG/Ollama path produces non-deterministic, often incorrect chord suggestions

## Proposed Solution

A `SKILL.md`-driven skill (`.agent/skills/progression-completion/SKILL.md`) that:
1. Triggers on continuation/completion phrases
2. Calls `GaProgressionCompletion(chords[])` → gets ranked next-chord candidates
3. Calls `GaAnalyzeProgression(chords)` → confirms detected key and Roman numerals
4. Returns 3–5 ranked suggestions with harmonic function explanation

The `SkillMdDrivenSkill` infrastructure handles everything else (Anthropic API, tool loop, MCP wiring).

## Technical Approach

### SKILL.md Frontmatter

```yaml
---
Name: "Progression Completion"
Description: "Suggests logical next chords to complete or extend a partial chord progression"
Triggers:
  - "finish this progression"
  - "complete the progression"
  - "what comes next"
  - "next chord"
  - "help me finish"
  - "how to end"
  - "what chord follows"
  - "continue this progression"
  - "extend this progression"
---
```

### System Prompt (body)

Instructs Claude to:
1. Extract the partial chord progression from the user message
2. Call `GaProgressionCompletion` with the chord array → ranked next-chord candidates
3. Call `GaAnalyzeProgression` to confirm detected key and Roman numerals
4. Optionally call `GaDiatonicChords` for diatonic context if needed
5. Format the response as a ranked list:
   ```
   Progression: Am – F – C (in A minor / C major)

   Suggested next chords:
   1. G  (bVII → I resolution — open, unresolved)
   2. E7 (V7 → i authentic cadence — strongest resolution to Am)
   3. Dm (iv → common pre-dominant move)
   4. Am (return to i — loop-friendly)
   5. C/G (I6/4 → V setup — classical approach)
   ```
6. Add 1–2 sentence context tip (e.g., "E7 gives the most conclusive cadence if you want to resolve; G keeps it open for another pass")

### File to Create

- `.agent/skills/progression-completion/SKILL.md` — skill definition + system prompt

### MCP Tools Used

| Tool | Source | Purpose |
|---|---|---|
| `GaProgressionCompletion` | `GuitaristProblemTools.cs` | Ranked next-chord candidates |
| `GaAnalyzeProgression` | `GaDslTool.cs` | Key detection + Roman numerals |
| `GaDiatonicChords` | `GaDslTool.cs` | Diatonic context if needed |

## Acceptance Criteria

- [ ] `dotnet run --project Apps/GaChatbotCli -- "Am F C — what comes next?"` returns ranked next-chord suggestions
- [ ] Response includes the detected key and Roman numeral analysis
- [ ] Response lists ≥ 3 ranked next-chord options with brief harmonic explanation
- [x] Skill fires on "finish this progression", "what comes next", "next chord", "help me finish" triggers
- [x] `KeyIdentificationSkill` continues to fire on "what key am I in?" (no collision — different triggers)
- [x] Build passes: `dotnet build AllProjects.slnx -c Debug`

## System-Wide Impact

- **No C# changes** — pure SKILL.md addition, auto-loaded by `SkillMdPlugin`
- **No skill registration changes** — `SkillMdPlugin` discovers it automatically via `triggers:`
- **Dependency:** `ANTHROPIC_API_KEY` required (same as all `SkillMdDrivenSkill` instances)
- **No collision** with `KeyIdentificationSkill` — different trigger keywords
- **No collision** with `ChordSubstitutionSkill` — different trigger keywords

## Sources & References

- `GaProgressionCompletion` tool: `GaMcpServer/Tools/GuitaristProblemTools.cs`
- `GaAnalyzeProgression` tool: `GaMcpServer/Tools/GaDslTool.cs`
- Existing SKILL.md with triggers: `.agent/skills/ga/chords/SKILL.md`
- `SkillMdDrivenSkill`: `Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs`
- `SkillMdPlugin` (auto-discovery): `Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs`
- `KeyIdentificationSkill` (reference pattern): `Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs`
- Scale/Mode/Arpeggio Advisor (companion plan): `docs/plans/2026-03-08-feat-scale-mode-arpeggio-advisor-plan.md`
