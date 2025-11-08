# Music Theory DSL - Quick Start Guide

**Status**: Proposal - Awaiting Approval  
**Date**: 2025-11-01

## What is This?

This proposal introduces **Domain-Specific Languages (DSLs)** for Guitar Alchemist, enabling natural syntax for music theory operations instead of verbose command-line parameters or API calls.

## Before & After Examples

### **Chord Progression Generation**

**Before** (Verbose):
```bash
gacli generate-progression --chords ii,V,I --key D --mode minor --output json
```

**After** (Natural DSL):
```bash
gacli generate "ii-V-I in Dm"
```

### **Fretboard Search**

**Before** (Verbose):
```bash
gacli find-chord --name Cmaj7 --strings 2,3,4,5 --max-difficulty 3 --format json
```

**After** (Natural DSL):
```bash
gacli find "all Cmaj7 voicings on strings 2-5 where difficulty < 3"
```

### **API Queries**

**Before** (Verbose):
```
GET /api/chords?name=Cmaj7&strings=2,3,4,5&maxDifficulty=3&format=json
```

**After** (Natural DSL):
```
GET /api/chords?query="find all Cmaj7 voicings on strings 2-5 where difficulty < 3"
```

## 5 Proposed DSLs

### 1. **Chord Progression Notation** (Highest Priority)

**Syntax Examples**:
```
I-IV-V-I in C major
ii-V-I in Dm
Cmaj7 -> Dm7 -> G7 -> Cmaj7
[I, IV, V, I] key=C mode=major tempo=120
```

**Use Cases**:
- GaCLI: `gacli generate "ii-V-I in Dm"`
- API: `POST /api/progressions/generate` with body `{"query": "ii-V-I in Dm"}`
- YAML: `progression_dsl: "I-IV-V-I in C major"`
- Chatbot: "Show me a ii-V-I progression in D minor"

### 2. **Fretboard Navigation**

**Syntax Examples**:
```
find all Cmaj7 voicings on strings 1-4
move to nearest minor chord shape
goto position 5 string 3
filter by difficulty < 3
```

**Use Cases**:
- BSP Explorer: Command palette (Ctrl+P) with DSL syntax
- GaCLI: `gacli navigate "find all Cmaj7 voicings on strings 2-5"`
- API: `GET /api/fretboard/navigate?query="move to nearest minor chord shape"`

### 3. **Scale/Mode Transformations**

**Syntax Examples**:
```
C major -> D dorian
apply harmonic minor to progression
transpose +5 semitones
rotate mode 3
```

**Use Cases**:
- Chatbot: "Apply harmonic minor to this progression"
- API: `POST /api/scales/transform` with body `{"query": "C major -> D dorian"}`
- GaCLI: `gacli transform-scale "transpose +5 semitones"`

### 4. **Grothendieck Monoid Operations**

**Syntax Examples**:
```
ICV(Cmaj7) - ICV(Dm7)
delta(shape1, shape2)
L1_norm(ICV(Cmaj7))
nearby_sets(Cmaj7, distance=2)
```

**Use Cases**:
- GaCLI: `gacli grothendieck "ICV(Cmaj7) - ICV(Dm7)"`
- API: `GET /api/grothendieck/compute?expr="delta(shape1, shape2)"`
- Chatbot: "Calculate ICV(Cmaj7) - ICV(Dm7)"

### 5. **Harmonic Analysis**

**Syntax Examples**:
```
analyze: Cmaj7 -> Dm7 -> G7 -> Cmaj7
find secondary dominants in progression
identify borrowed chords
detect modulations
```

**Use Cases**:
- Chatbot: "Analyze this progression: Cmaj7 -> Dm7 -> G7 -> Cmaj7"
- API: `POST /api/analysis/harmonic` with body `{"query": "find secondary dominants in progression"}`
- GaCLI: `gacli analyze "detect modulations"`

## Implementation Plan

### **Phase 1: Foundation** (2-3 weeks) - **START HERE**

**Goal**: Prove the concept with chord progression DSL

**Tasks**:
1. ✅ Create EBNF grammar for chord progressions (DONE - see `Docs/Grammars/ChordProgression.ebnf`)
2. 🔲 Create `GA.MusicTheory.DSL` F# library project
3. 🔲 Implement parser using FParsec
4. 🔲 Integrate with GaCLI: `gacli generate "ii-V-I in Dm"`
5. 🔲 Add comprehensive unit tests

**Success Criteria**:
- ✅ Parse `"I-IV-V-I in C major"` → `[Cmaj, Fmaj, Gmaj, Cmaj]`
- ✅ GaCLI command works
- ✅ 90%+ test coverage

### **Phase 2: API Integration** (3-4 weeks)

**Goal**: Add DSL support to all 5 use cases

**Tasks**:
1. Create remaining EBNF grammars (fretboard, scale, Grothendieck, analysis)
2. Implement parsers for all DSLs
3. Add DSL query parameter support to GaApi
4. Update Swagger documentation

**Success Criteria**:
- All 5 DSLs have working parsers
- API accepts DSL queries
- Swagger docs include DSL examples

### **Phase 3: Advanced Features** (4-6 weeks) - **OPTIONAL**

**Goal**: Add fractal grammars, LSP, advanced integrations

**Tasks**:
1. Fractal grammars for recursive harmonic structures
2. LSP server for VS Code (syntax highlighting, auto-completion)
3. Chatbot DSL command recognition
4. BSP Explorer command palette

**Success Criteria**:
- Fractal grammar models recursive progressions
- VS Code provides auto-completion
- Chatbot executes DSL commands

## Fractal Grammar Opportunities

The TARS fractal grammar system enables **recursive harmonic structures**:

### **Example: Jazz Turnaround Fractal**

```fractal
fractal jazz_turnaround {
    pattern = "ii-V-I"
    recursive = "ii-V-[recursive]"
    dimension = 1.5
    depth = 3
    transform scale 0.5  // Each level is half the duration
}
```

**Output**:
- **Depth 0**: `ii-V-I`
- **Depth 1**: `ii-V-ii-V-I`
- **Depth 2**: `ii-V-ii-V-ii-V-I`
- **Depth 3**: `ii-V-ii-V-ii-V-ii-V-I`

### **Applications**:
1. **Recursive Harmonic Structures** - Nested turnarounds, circle of fifths
2. **Fractal Chord Voicings** - Drop 2, Drop 3, Drop 2+4 transformations
3. **Melodic Fractal Patterns** - Self-similar motif expansion
4. **Rhythmic Fractals** - Recursive subdivision patterns

## Architecture

### **New Library**: `GA.MusicTheory.DSL`

**Project Structure**:
```
GA.MusicTheory.DSL/
├── Grammars/
│   ├── ChordProgression.ebnf
│   ├── FretboardNavigation.ebnf
│   ├── ScaleTransformation.ebnf
│   ├── GrothendieckOperations.ebnf
│   └── HarmonicAnalysis.ebnf
├── Parsers/
│   ├── ChordProgressionParser.fs
│   ├── FretboardNavigationParser.fs
│   ├── ScaleTransformationParser.fs
│   ├── GrothendieckOperationsParser.fs
│   └── HarmonicAnalysisParser.fs
├── Types/
│   ├── GrammarTypes.fs
│   ├── ParseResult.fs
│   └── DslCommand.fs
├── Adapters/
│   └── TarsGrammarAdapter.fs  # Adapted from TARS
└── GA.MusicTheory.DSL.fsproj
```

**Dependencies**:
- `GA.Business.Core` (for domain types: Chord, Scale, Note)
- `FParsec` (for parsing)
- `FSharp.Core`

## TARS Grammar System (Source)

The TARS repository (`C:\Users\spare\source\repos\tars`) provides:

1. **EBNF Grammar Support** - Full parsing and management
2. **Fractal Grammar System** - Self-similar, recursive structures
3. **Multi-Language Support** - 9 languages (F#, C#, Python, Rust, JS, TS, PowerShell, Bash, SQL)
4. **Grammar Resolution** - External files, inline definitions, RFC integration
5. **Metadata System** - Versioning, hashing, tagging
6. **Transformation Engine** - Scale, rotate, translate, compose, recursive

**Key Files**:
- `Tars.Engine.Grammar/GrammarSource.fs` - Grammar types and utilities
- `Tars.Engine.Grammar/GrammarResolver.fs` - Grammar resolution
- `Tars.Engine.Grammar/FractalGrammar.fs` - Fractal grammar engine
- `Tars.Engine.Grammar/LanguageDispatcher.fs` - Multi-language support

## Documentation

### **Created Documents**:
1. ✅ **`Docs/MUSIC_THEORY_DSL_PROPOSAL.md`** - Complete proposal (300 lines)
2. ✅ **`Docs/TARS_DSL_ANALYSIS_SUMMARY.md`** - Analysis summary with recommendations
3. ✅ **`Docs/Grammars/ChordProgression.ebnf`** - Chord progression grammar (200 lines)
4. ✅ **`Docs/DSL_QUICK_START.md`** - This document

### **Diagrams**:
1. ✅ **DSL Architecture Diagram** - Shows TARS → GA.DSL → Integration Points
2. ✅ **Fractal Grammar Opportunities** - Shows music theory applications

## Next Steps

### **Immediate Actions** (This Week):

1. ✅ **Review Proposal** - Read `Docs/MUSIC_THEORY_DSL_PROPOSAL.md`
2. ✅ **Review Grammar** - Examine `Docs/Grammars/ChordProgression.ebnf`
3. 🔲 **Approve Phase 1** - Decide whether to proceed
4. 🔲 **Create Project** - Set up `GA.MusicTheory.DSL` F# library
5. 🔲 **Implement Parser** - Build chord progression parser with FParsec

### **Week 2-3**:

1. 🔲 **Integrate GaCLI** - Add `generate-progression` command
2. 🔲 **Write Tests** - Comprehensive unit tests for parser
3. 🔲 **User Testing** - Get feedback on syntax and usability
4. 🔲 **Iterate** - Refine grammar based on feedback

### **Week 4+**:

1. 🔲 **Decide on Phase 2** - Based on Phase 1 success
2. 🔲 **Implement Remaining DSLs** - If proceeding with Phase 2
3. 🔲 **API Integration** - Add DSL query support
4. 🔲 **Documentation** - Update all docs with DSL examples

## Benefits

### **User Experience**:
- 🎯 **Natural Syntax**: `"ii-V-I in Dm"` vs. `--progression ii,V,I --key Dm --mode minor`
- 🚀 **Faster Workflow**: Less typing, more intuitive
- 📚 **Self-Documenting**: Syntax reads like music theory textbooks
- 🎨 **Expressive**: Can express complex operations concisely

### **Developer Experience**:
- 🔧 **Proven Technology**: TARS grammar system is battle-tested
- 📈 **Scalable**: Can expand to 5 DSLs and advanced features
- 🧪 **Testable**: FParsec provides excellent testing support
- 🎨 **Fractal Grammars**: Unique opportunity for recursive harmonic modeling

### **Technical Benefits**:
- ✅ **Clean Separation**: DSL library separate from business logic
- ✅ **Independent Versioning**: Can evolve DSL without affecting GA.Business.Core
- ✅ **Reusable**: Could be used by other music theory projects
- ✅ **Type-Safe**: F# + FParsec provide strong type safety

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Complexity Creep** | High | Start simple (chord progressions only), iterate |
| **Parser Performance** | Medium | Use FParsec optimizations, benchmark early |
| **User Adoption** | Medium | Clear docs, examples, tutorials |
| **Maintenance Burden** | Medium | Keep grammars simple, automate testing |

## Recommendation

### ✅ **PROCEED WITH PHASE 1**

**Rationale**:
- TARS provides excellent infrastructure
- High value for user experience
- Phased approach minimizes risk
- Immediate value (chord progressions in 2-3 weeks)

**Next Step**: Create `GA.MusicTheory.DSL` project and implement chord progression parser.

---

## Questions?

For more details, see:
- **Full Proposal**: `Docs/MUSIC_THEORY_DSL_PROPOSAL.md`
- **Analysis Summary**: `Docs/TARS_DSL_ANALYSIS_SUMMARY.md`
- **Chord Progression Grammar**: `Docs/Grammars/ChordProgression.ebnf`

