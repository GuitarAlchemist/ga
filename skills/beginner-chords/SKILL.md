---
Name: "beginner-chords"
Description: "Returns the eight first-position open guitar chords every curriculum starts with — C, G, D, A, E, Am, Em, Dm — with diagrams and short fingering tips. Use when a learner asks 'what are the easy chords' / 'beginner chords' / 'first chords I should learn'."
Triggers:
  - "beginner chord"
  - "easy chord"
  - "first chord"
  - "open chord"
  - "starter chord"
  - "basic open chord"
  - "simple guitar chord"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/BeginnerChordsSkill.cs (PR #71) — canary for SKILL.md authoring (PR-pending)"
  evidence-kinds:
    - catalog_lookup
---

# Beginner Open-Position Guitar Chords

Use this catalog when a learner asks for the first chords to learn. Reproduce the eight diagrams verbatim and end with the practice tip in the final paragraph. Do not invent additional chord shapes or alter the fingering notes — those are pedagogically chosen and pre-validated.

## Diagram conventions

Each diagram is six tokens, low-to-high (E A D G B e). `0` = open string, `x` = mute, otherwise the integer is the fret number. Tokens are separated by `-`.

## The eight chords

1. **C major** — `x-3-2-0-1-0` — First-position major. Watch the muted low E.
2. **G major** — `3-2-0-0-0-3` — Use ring/middle/pinky on E-A-e for cleaner switching to D.
3. **D major** — `x-x-0-2-3-2` — Compact triad — handy for "D shape" barre training later.
4. **A major** — `x-0-2-2-2-0` — Index/middle/ring on D-G-B; or barre with one finger across the 2nd fret.
5. **E major** — `0-2-2-1-0-0` — Strongest, fullest open chord — every string rings.
6. **A minor** — `x-0-2-2-1-0` — Same shape as E, moved one string up; bedrock minor sound.
7. **E minor** — `0-2-2-0-0-0` — Easiest chord on the guitar — two fingers, six strings ringing.
8. **D minor** — `x-x-0-2-3-1` — Compact like D major; good for swapping with C and G in folk songs.

## Practice tip

Drill C ↔ G ↔ D ↔ Am ↔ Em as a smooth loop. Those five cover most folk, pop, and country songs in major keys. Add A, E, and Dm next.

## Out of scope

- Barre chords (F, B, Bm) — these are second-pass, not first-pass material.
- Power chords / palm-muted shapes — those serve a different pedagogical track.
- Tunings other than standard EADGBe — convert from this catalog rather than re-deriving.

## Cross-reference

- C# implementation: `Common/GA.Business.ML/Agents/Skills/BeginnerChordsSkill.cs`
- Tests: `Tests/Common/GA.Business.ML.Tests/Unit/BeginnerChordsSkillTests.cs`
