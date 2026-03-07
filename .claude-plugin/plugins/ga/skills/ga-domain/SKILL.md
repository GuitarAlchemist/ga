---
name: "GA Domain Context"
description: "Primes every session with the guitarist's perspective, GA architecture, and how to use the MCP tools during feature development. Load this before working on any Guitar Alchemist feature."
triggers:
  - "guitarist problem"
  - "guitar alchemist feature"
  - "GA backlog"
  - "music theory feature"
  - "chord substitution"
  - "improvisation feature"
---

# GA Domain Context

Guitar Alchemist is **a music platform for guitarists**, not a generic dev project. Every feature decision should start with a real guitarist's pain, verified with the GA MCP tools, then implemented in the GA domain model.

---

## Who We're Building For

A guitarist who:
- Knows songs but not always why they work harmonically
- Learns by ear first, theory second
- Hits a wall: "why does this sound wrong?" / "what can I play here?"
- Wants answers in guitarist vocabulary: chord names, fret positions, tab — not abstract intervals

**North Star question for every feature**: *What does a guitarist do with this?*

---

## GA MCP Tools — Use During Development, Not Just Runtime

The `ga-dsl` MCP server (registered as `mcp__ga-dsl__*`) gives you live music theory during brainstorming and planning. **Call these tools when designing features** to verify your theory is correct before writing code.

### Verification workflow for a guitarist problem

1. **Identify the musical concept** — what chord, scale, or key is involved?
2. **Verify with MCP tools** — call `GaAnalyzeProgression`, `GaChordIntervals`, `GaCommonTones` to ground the feature in real theory
3. **Design the output** — what does the guitarist see? (tab, chord symbol, fretboard, explanation)
4. **Implement in the right layer** — see architecture below

### Key tools and when to use them

| Tool | When to call it |
|------|----------------|
| `GaAnalyzeProgression("Am F C G")` | Whenever a feature involves key detection or Roman numeral labelling |
| `GaDiatonicChords("G", "major")` | When suggesting chords that "fit" a key |
| `GaChordIntervals("Cmaj7")` | When explaining why a chord sounds the way it does |
| `GaCommonTones("G7", "Cmaj7")` | When implementing voice-leading or pivot-chord features |
| `GaTransposeChord("Am7", 7)` | When implementing transposition or capo features |
| `GaRelativeKey("A", "minor")` | When implementing relative key or parallel key features |
| `GaSearchTabs("metallica nothing else matters")` | When a feature needs real-world tab examples |
| `GaInvokeClosure("domain.queryChords", '{"key":"G","scale":"major","quality":"minor"}')` | For advanced diatonic filtering |

---

## Architecture Quick Reference

### Five-layer rule (strict — no exceptions)

```
Layer 1 — GA.Core / GA.Domain.Core      Pure primitives: Note, Interval, PitchClass, Fretboard
Layer 2 — GA.Business.Core              Business logic, chord analysis, scale queries
Layer 3 — GA.Business.Core.Harmony      Voice leading, chord progression analysis
Layer 4 — GA.Business.ML                Embeddings (OPTIC-K), semantic search, agents
Layer 5 — GA.Business.Core.Orchestration High-level workflows, IntelligentBSPGenerator
```

**Rule**: new code goes in the lowest layer that has everything it needs. AI code → Layer 4. Workflows → Layer 5. Pure music math → Layer 1 or 2.

### Where guitarist features typically land

| Problem type | Layer | Example |
|---|---|---|
| "What intervals are in this chord?" | 1–2 | `Chord.Intervals`, `ChordDslService` |
| "Which chords fit this key?" | 2–3 | `DiatonicChordService` |
| "Suggest a substitution" | 3 | `HarmonicTransformationService` |
| "Find similar-sounding songs" | 4 | OPTIC-K embedding + vector search |
| "Generate a practice routine" | 5 | Orchestration + agent composition |

### F# DSL — the closure registry

The GA Language closures (`domain.*`, `tab.*`, `agent.*`) live in `GA.Business.DSL`. They are:
- Registered at module load via `do register()`
- Accessible in-process via `GaClosureRegistry.Global`
- Exposed to Claude Code via the `ga-dsl` MCP server

When adding a new music-theory closure, add it to `DomainClosures.fs` and register it in `register()`.

### OPTIC-K embeddings (216 dimensions)

Used for semantic similarity between chord voicings/BSP rooms. **Never change dimension count** without a full re-index. Structure:
- dims 6–29: STRUCTURE (pitch-class invariants) — weight 0.45
- dims 30–53: MORPHOLOGY (fretboard geometry) — weight 0.25
- dims 54–65: CONTEXT (harmonic function) — weight 0.20
- dims 66–77: SYMBOLIC (manual tags) — weight 0.10

---

## The Compound Engineering Loop for GA

```
Guitarist problem (BACKLOG.md)
    ↓
Brainstorm: call GA MCP tools to verify the music theory
    ↓  GaAnalyzeProgression, GaChordIntervals, GaCommonTones…
Plan: docs/plans/ — what does the guitarist see + how does it map to layers?
    ↓
Implement: lowest layer first, ROP error handling, unit tests
    ↓
Document: update BACKLOG.md (remove idea), link plan to PR
    ↓
Compound: solution is now a closure / endpoint / UI feature
         that makes the NEXT problem easier to solve
```

**Each solved problem expands the closure registry**, which makes future features faster: `GaCommonTones` built for voice-leading also powers pivot-chord detection and substitution suggestions.

---

## Guitarist Vocabulary → GA Concepts

| What a guitarist says | GA concept | Tool / layer |
|---|---|---|
| "What key am I in?" | Key inference from chord set | `GaAnalyzeProgression` |
| "What scale fits here?" | Diatonic scale from key | `GaDiatonicChords` |
| "Why does this note sound outside?" | Chromatic pitch class vs. diatonic set | `GaChordIntervals` + set math |
| "What can I play instead?" | Chord substitution | `GaCommonTones` + `GaRelativeKey` |
| "How do I finger this?" | Fretboard geometry | `GA.Business.Core.Fretboard` layer |
| "Show me a lick" | Tab generation | `tab.generateChord`, VexTabGenerator |
| "Find me a tab" | Internet tab search | `GaSearchTabs` (Archive.org + GitHub) |
| "This sounds jazzy/bluesy" | Stylistic tag | OPTIC-K SYMBOLIC dims |

---

## Before Implementing Any Feature

1. **Call at least one MCP tool** to verify the music theory is correct
2. **Ask**: what does a guitarist do with this output? (tab, chord name, fretboard, text)
3. **Place in the right layer** — never put orchestration in Layer 1-2
4. **ROP all the way** — service methods return `Result<T, TError>`, never throw
5. **Write a test** that uses a real chord symbol or progression, not mocked values
