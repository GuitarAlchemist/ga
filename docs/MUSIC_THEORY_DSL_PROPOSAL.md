# Music Theory DSL Proposal for Guitar Alchemist

**Date**: 2025-11-01  
**Status**: Proposal  
**Based on**: TARS Grammar System Analysis

## Executive Summary

This proposal outlines the creation of Domain-Specific Languages (DSLs) for Guitar Alchemist, leveraging grammar extraction techniques from the TARS project. The DSLs will provide formal, parseable syntax for chord progressions, fretboard navigation, scale transformations, and harmonic analysis.

## TARS Grammar System Analysis

### Available Capabilities

The TARS repository (`C:\Users\spare\source\repos\tars`) contains a comprehensive grammar system with the following features:

#### 1. **EBNF Grammar Support**
- **Location**: `Tars.Engine.Grammar/GrammarSource.fs`
- **Features**:
  - External grammar file resolution (`.tars` files)
  - Inline grammar definitions
  - Grammar metadata (versioning, hashing, tagging)
  - RFC-based grammar extraction
  - Multi-format output (EBNF, ANTLR, JSON, XML, GraphViz, SVG)

#### 2. **Fractal Grammar System**
- **Location**: `Tars.Engine.Grammar/FractalGrammar.fs`
- **Features**:
  - Self-similar, recursive grammar structures
  - Mathematical fractal properties (dimension, scaling, iteration depth)
  - Transformations: Scale, Rotate, Translate, Compose, Recursive, Conditional
  - L-System integration for biological/recursive modeling
  - Visualization support (SVG, GraphViz)

#### 3. **Multi-Language Support**
- **Location**: `Tars.Engine.Grammar/LanguageDispatcher.fs`
- **Supported Languages**: F#, C#, Python, Rust, JavaScript, TypeScript, PowerShell, Bash, SQL
- **Features**: `LANG("LANGUAGE")` blocks for polyglot development

#### 4. **Grammar Resolution**
- **Location**: `Tars.Engine.Grammar/GrammarResolver.fs`
- **Features**:
  - External file resolution (`.tars/grammars/` directory)
  - Inline definition fallback
  - Grammar indexing and versioning
  - Hash-based change detection

### Reusable Components

The following TARS components can be adapted for Guitar Alchemist:

| Component | TARS File | Adaptation for GA |
|-----------|-----------|-------------------|
| **Grammar Types** | `GrammarSource.fs` | Define music theory grammar types |
| **EBNF Parser** | `GrammarResolver.fs` | Parse chord progression syntax |
| **Metadata System** | `GrammarMetadata` type | Version music theory grammars |
| **Fractal Engine** | `FractalGrammar.fs` | Model recursive harmonic structures |
| **Transformation System** | `FractalTransformation` type | Transform chord progressions, scales |

## Proposed Music Theory DSLs

### 1. **Chord Progression Notation DSL** (Highest Priority)

#### Syntax Examples:
```
I-IV-V-I in C major
ii-V-I in Dm
Cmaj7 -> Dm7 -> G7 -> Cmaj7
[I, IV, V, I] key=C mode=major
```

#### EBNF Grammar:
```ebnf
progression = roman_progression | chord_progression ;
roman_progression = roman_chord, { "-", roman_chord }, "in", key, mode ;
chord_progression = chord, { "->", chord } ;
roman_chord = [ accidental ], roman_numeral, [ quality ] ;
chord = note, quality ;
key = note ;
mode = "major" | "minor" | "dorian" | "phrygian" | ... ;
quality = "maj7" | "m7" | "7" | "dim" | "aug" | ... ;
roman_numeral = "I" | "II" | "III" | "IV" | "V" | "VI" | "VII" | "i" | "ii" | ... ;
note = "C" | "D" | "E" | "F" | "G" | "A" | "B" ;
accidental = "#" | "b" ;
```

#### Use Cases:
- GaCLI: `gacli generate-progression "ii-V-I in Dm"`
- API: `GET /api/progressions?query="I-IV-V-I in C major"`
- YAML: `progression: "I-IV-V-I in {key}"` (dynamic generation)
- Chatbot: "Show me a ii-V-I progression in D minor"

### 2. **Fretboard Navigation DSL**

#### Syntax Examples:
```
find all Cmaj7 voicings on strings 1-4
move to nearest minor chord shape
goto position 5 string 3
filter by difficulty < 3
```

#### EBNF Grammar:
```ebnf
navigation_command = find_command | move_command | goto_command | filter_command ;
find_command = "find", [ "all" ], chord, "voicings", "on", string_range ;
move_command = "move", "to", [ "nearest" ], chord_filter, "shape" ;
goto_command = "goto", "position", number, "string", number ;
filter_command = "filter", "by", filter_expression ;
string_range = "strings", number, "-", number ;
chord_filter = quality | "major" | "minor" | "dominant" | "diminished" ;
filter_expression = property, operator, value ;
property = "difficulty" | "stretch" | "barres" | "open_strings" ;
operator = "<" | ">" | "=" | "<=" | ">=" ;
```

#### Use Cases:
- BSP Explorer: Command palette with DSL syntax
- GaCLI: `gacli navigate "find all Cmaj7 voicings on strings 2-5"`
- API: `GET /api/fretboard/navigate?query="move to nearest minor chord shape"`

### 3. **Scale/Mode Transformation DSL**

#### Syntax Examples:
```
C major -> D dorian
apply harmonic minor to progression
transpose +5 semitones
rotate mode 3
```

#### EBNF Grammar:
```ebnf
transformation = scale_transform | mode_transform | transpose_transform | rotate_transform ;
scale_transform = scale, "->", scale ;
mode_transform = "apply", mode, "to", target ;
transpose_transform = "transpose", interval ;
rotate_transform = "rotate", "mode", number ;
scale = note, scale_type ;
scale_type = "major" | "minor" | "harmonic minor" | "melodic minor" | ... ;
interval = [ "+" | "-" ], number, "semitones" ;
target = "progression" | "chord" | "melody" ;
```

#### Use Cases:
- Chatbot: "Apply harmonic minor to this progression"
- API: `POST /api/scales/transform` with body `{"query": "C major -> D dorian"}`
- GaCLI: `gacli transform-scale "transpose +5 semitones"`

### 4. **Grothendieck Monoid Operations DSL**

#### Syntax Examples:
```
ICV(Cmaj7) - ICV(Dm7)
delta(shape1, shape2)
L1_norm(ICV(Cmaj7))
nearby_sets(Cmaj7, distance=2)
```

#### EBNF Grammar:
```ebnf
monoid_operation = icv_operation | delta_operation | norm_operation | nearby_operation ;
icv_operation = "ICV", "(", chord, ")", [ binary_op, "ICV", "(", chord, ")" ] ;
delta_operation = "delta", "(", shape_ref, ",", shape_ref, ")" ;
norm_operation = "L1_norm", "(", icv_expression, ")" ;
nearby_operation = "nearby_sets", "(", chord, ",", "distance", "=", number, ")" ;
binary_op = "+" | "-" | "*" ;
shape_ref = identifier | chord ;
icv_expression = icv_operation | identifier ;
```

#### Use Cases:
- GaCLI: `gacli grothendieck "ICV(Cmaj7) - ICV(Dm7)"`
- API: `GET /api/grothendieck/compute?expr="delta(shape1, shape2)"`
- Chatbot: "Calculate ICV(Cmaj7) - ICV(Dm7)"

### 5. **Harmonic Analysis DSL**

#### Syntax Examples:
```
analyze: Cmaj7 -> Dm7 -> G7 -> Cmaj7
find secondary dominants in progression
identify borrowed chords
detect modulations
```

#### EBNF Grammar:
```ebnf
analysis_command = analyze_command | find_command | identify_command | detect_command ;
analyze_command = "analyze", ":", chord_sequence ;
find_command = "find", analysis_target, "in", "progression" ;
identify_command = "identify", chord_type ;
detect_command = "detect", harmonic_feature ;
chord_sequence = chord, { "->", chord } ;
analysis_target = "secondary dominants" | "tritone substitutions" | "modal interchange" ;
chord_type = "borrowed chords" | "pivot chords" | "passing chords" ;
harmonic_feature = "modulations" | "tonicizations" | "cadences" ;
```

#### Use Cases:
- Chatbot: "Analyze this progression: Cmaj7 -> Dm7 -> G7 -> Cmaj7"
- API: `POST /api/analysis/harmonic` with body `{"query": "find secondary dominants in progression"}`
- GaCLI: `gacli analyze "detect modulations"`

## Integration Points

### 1. **YAML Configuration Files** (e.g., `AtonalTechniques.yaml`)

**Current**:
```yaml
techniques:
  - name: "Twelve-Tone Row"
    progression: ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
```

**Enhanced with DSL**:
```yaml
techniques:
  - name: "Twelve-Tone Row"
    progression_dsl: "chromatic_row starting_at C"
  - name: "Modal Interchange"
    progression_dsl: "I-IV-bVII-IV in C major with borrowed_from=C minor"
```

### 2. **GaCLI Command-Line Interface**

**Current**:
```bash
gacli find-chord --name Cmaj7 --strings 2,3,4,5 --max-difficulty 3
```

**Enhanced with DSL**:
```bash
gacli find "all Cmaj7 voicings on strings 2-5 where difficulty < 3"
gacli generate "ii-V-I in Dm"
gacli navigate "move to nearest dominant chord"
```

### 3. **API Query Languages**

**Current**:
```
GET /api/chords?name=Cmaj7&strings=2,3,4,5&maxDifficulty=3
```

**Enhanced with DSL**:
```
GET /api/chords?query="find all Cmaj7 voicings on strings 2-5 where difficulty < 3"
POST /api/progressions/generate
Body: {"query": "ii-V-I in Dm"}
```

### 4. **BSP DOOM Explorer Navigation**

**Current**: Click-based navigation only

**Enhanced with DSL**:
- Command palette (Ctrl+P): Type "goto nearest minor chord"
- Search bar: "find all dominant 7th chords"
- Navigation commands: "move up one floor" or "teleport to Cmaj7"

### 5. **Chatbot Natural Language Processing**

**Current**: Free-form natural language only

**Enhanced with DSL**:
- Structured commands: "Show me ICV(Cmaj7) - ICV(Dm7)"
- Precise queries: "Generate ii-V-I in Dm"
- Mathematical operations: "Calculate delta(shape1, shape2)"

## Implementation Approach

### Phase 1: Foundation (Immediate Value - 2-3 weeks)

**Goal**: Create basic DSL infrastructure with chord progression support

**Tasks**:
1. Create `GA.MusicTheory.DSL` F# library project
2. Adapt TARS `GrammarSource.fs` for music theory grammars
3. Create EBNF grammar for chord progression notation
4. Implement parser using FParsec
5. Integrate with GaCLI for `generate-progression` command
6. Add unit tests for parser

**Deliverables**:
- `GA.MusicTheory.DSL.fsproj` project
- `ChordProgressionGrammar.ebnf` file
- `ChordProgressionParser.fs` module
- GaCLI integration demo
- Unit tests

**Success Criteria**:
- Can parse "I-IV-V-I in C major" and generate chord sequence
- GaCLI command works: `gacli generate "ii-V-I in Dm"`
- 90%+ test coverage for parser

### Phase 2: API Integration (Medium-term - 3-4 weeks)

**Goal**: Add DSL query support to GaApi

**Tasks**:
1. Create fretboard navigation grammar and parser
2. Create scale transformation grammar and parser
3. Add DSL query parameter support to API controllers
4. Implement query language for chord/scale searches
5. Add Grothendieck monoid operation syntax
6. Update API documentation (Swagger)

**Deliverables**:
- `FretboardNavigationGrammar.ebnf`
- `ScaleTransformationGrammar.ebnf`
- `GrothendieckOperationsGrammar.ebnf`
- Updated API controllers with DSL support
- Swagger documentation updates

**Success Criteria**:
- API accepts DSL queries: `GET /api/chords?query="find all Cmaj7 voicings on strings 2-5"`
- All 5 DSLs have working parsers
- API documentation includes DSL syntax examples

### Phase 3: Advanced Features (Long-term - 4-6 weeks)

**Goal**: Add fractal grammars, LSP, and advanced integrations

**Tasks**:
1. Implement fractal grammars for recursive harmonic structures
2. Create LSP server for VS Code integration
3. Add chatbot DSL command recognition
4. Implement BSP Explorer command palette
5. Add syntax highlighting for DSL files
6. Create grammar visualization tools

**Deliverables**:
- Fractal grammar support for recursive progressions
- LSP server for VS Code
- Chatbot DSL integration
- BSP Explorer command palette
- VS Code extension for syntax highlighting
- Grammar visualization (railroad diagrams)

**Success Criteria**:
- Can model recursive harmonic structures with fractal grammars
- VS Code provides auto-completion and validation for DSL
- Chatbot recognizes and executes DSL commands
- BSP Explorer has working command palette

## Architecture Decision: Separate Library vs. Integrated

### Recommendation: **Separate Library** (`GA.MusicTheory.DSL`)

**Rationale**:
- **Clean Separation**: Grammar/parsing concerns separate from business logic
- **Independent Versioning**: Can evolve DSL without affecting GA.Business.Core
- **Testability**: Easier to test DSL in isolation
- **Reusability**: Could be used by other music theory projects
- **Single Responsibility**: Each library has one clear purpose

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
- `GA.Business.Core` (for domain types: Chord, Scale, Note, etc.)
- `FParsec` (for parsing)
- `FSharp.Core`

## Language Server Protocol (LSP) Considerations

### Benefits:
- **Syntax Highlighting**: Color-code chord symbols, Roman numerals, scale names
- **Auto-completion**: Suggest chord progressions, scale names, fretboard positions
- **Real-time Validation**: Check if chord exists, validate key signatures, verify voicings
- **Hover Documentation**: Show chord diagrams, scale patterns, interval structures
- **Go-to-Definition**: Jump to chord shape definitions, scale patterns, progression templates

### Implementation Effort:
- **High**: LSP server implementation is complex
- **Medium-High**: Requires separate process/server
- **Medium**: Maintenance overhead for protocol updates

### Recommendation:
- **Phase 1-2**: Focus on EBNF grammars and basic parsing
- **Phase 3**: Add LSP if there's user demand
- **Priority**: GaCLI and API integration provide more immediate value

## Success Metrics

### Phase 1:
- ✅ Chord progression DSL working in GaCLI
- ✅ 90%+ test coverage for parser
- ✅ User can generate progressions with natural syntax

### Phase 2:
- ✅ All 5 DSLs have working parsers
- ✅ API accepts DSL queries
- ✅ Swagger documentation includes DSL examples

### Phase 3:
- ✅ Fractal grammar support implemented
- ✅ LSP server provides auto-completion and validation
- ✅ Chatbot recognizes DSL commands
- ✅ BSP Explorer has command palette

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Complexity Creep** | High | Start with simplest DSL (chord progressions), iterate |
| **Parser Performance** | Medium | Use FParsec optimizations, benchmark early |
| **User Adoption** | Medium | Provide clear documentation, examples, tutorials |
| **Maintenance Burden** | Medium | Keep grammars simple, automate testing |
| **TARS Dependency** | Low | Adapt code, don't depend on TARS directly |

## Next Steps

1. **Get User Approval**: Review this proposal and decide on Phase 1 scope
2. **Create GA.MusicTheory.DSL Project**: Set up F# library with FParsec
3. **Implement Chord Progression Grammar**: Start with highest-value DSL
4. **Integrate with GaCLI**: Add `generate-progression` command
5. **Gather Feedback**: Test with users, iterate on syntax
6. **Expand to Phase 2**: Add remaining DSLs based on feedback

## Conclusion

The TARS grammar system provides a solid foundation for creating music theory DSLs in Guitar Alchemist. By adapting TARS's EBNF grammar support, fractal grammar system, and multi-language capabilities, we can create powerful, parseable syntax for chord progressions, fretboard navigation, scale transformations, and harmonic analysis.

The proposed phased approach provides immediate value (chord progressions in GaCLI) while keeping the door open for advanced features (fractal grammars, LSP server). Creating a separate `GA.MusicTheory.DSL` library ensures clean separation of concerns and independent evolution.

**Recommendation**: Proceed with Phase 1 to validate the approach and gather user feedback before committing to the full implementation.

