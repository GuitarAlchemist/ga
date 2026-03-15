# Ralph Prompt: Probabilistic Grammars for Guitar Alchemist

## Goal

Add probabilistic grammar weights to Guitar Alchemist's music theory DSL so
that chord progressions, scale transformations, and voicing selections are
guided by learned statistical preferences rather than uniform rule application.

## Context

TARS (F# repo) has implemented a probabilistic grammar layer:
- **WeightedGrammar**: Beta-Binomial Bayesian weight updates, softmax selection
- **ReplicatorDynamics**: Evolutionary game theory for grammar rule competition
- **Constrained decoding**: EBNF grammar → vLLM guided_decoding

Guitar Alchemist already has:
- 12+ EBNF grammars in `Common/GA.Business.DSL/Grammars/` (ChordProgression,
  ScaleTransformation, ChordSymbol, VexTab, etc.)
- FParsec-based recursive descent parsing
- F# source generators from EBNF (`GA.Business.Core.Generated`)
- OPTIC-K 216-dim musical embeddings for vector search
- Atonal set theory via Grothendieck monoid operations
- GuitarChordProgressionMCTS tool for harmonizing melodies

## Architecture

### Project: `GA.Business.ProbabilisticGrammar` (NEW F# Library)

Create `Common/GA.Business.ProbabilisticGrammar/`:

```
WeightedMusicRule.fs    -- Music-domain weighted grammar rules
MusicReplicator.fs      -- Replicator dynamics for music rule competition
ConstrainedGeneration.fs -- Probabilistic grammar-guided generation
HarmonicFitness.fs      -- Domain-specific fitness functions
```

**WeightedMusicRule.fs** — Music-aware probabilistic rules:
```fsharp
type MusicRuleSource =
    | ChordGrammar       // from ChordProgression.ebnf
    | ScaleGrammar       // from ScaleTransformation.ebnf
    | VoicingGrammar     // from ExtendedVoicings.ebnf
    | HarmonyConstraint  // from domain invariants

type WeightedMusicRule =
    { RuleId: string
      Production: string        // EBNF production text
      Alpha: float; Beta: float // Beta-Binomial parameters
      Weight: float             // current probability weight
      Source: MusicRuleSource
      MusicalContext: string option } // e.g. "key=C major", "style=jazz"

// Bayesian update from user feedback or harmonic analysis
val bayesianUpdate: rule: WeightedMusicRule -> success: bool -> WeightedMusicRule

// Context-aware softmax (temperature varies by musical context)
val softmaxByContext: rules: WeightedMusicRule list -> context: string -> float list

// Select next production weighted by learned preferences
val selectWeighted: rules: WeightedMusicRule list -> rng: Random -> WeightedMusicRule
```

**MusicReplicator.fs** — Genre/style evolution:
```fsharp
type MusicSpecies =
    { RuleId: string
      Proportion: float
      Fitness: float
      Genre: string option }  // "jazz", "classical", "blues"

// Replicator dynamics: rules that produce pleasant progressions grow
val step: species: MusicSpecies list -> dt: float -> MusicSpecies list

// Find stable musical idioms (ESS = established musical patterns)
val detectStableIdioms: species: MusicSpecies list -> threshold: float -> MusicSpecies list

// Full evolution from listening history / user preferences
val evolveFromPreferences: rules: WeightedMusicRule list -> outcomes: (string * bool) list -> SimResult
```

**HarmonicFitness.fs** — Domain-specific fitness for grammar rules:
```fsharp
// Fitness based on music theory quality metrics
val chordProgressionFitness: progression: Chord list -> key: Key -> float
  // Factors: voice leading smoothness, harmonic tension/resolution,
  //          functional harmony (T-PD-D-T), common-tone connections

val voicingFitness: voicing: Voicing -> context: HarmonicContext -> float
  // Factors: hand span, string crossing, open string resonance,
  //          interval quality, doubling rules

val scaleChoiceFitness: scale: Scale -> chord: Chord -> style: string -> float
  // Factors: avoid notes, characteristic tones, style appropriateness
```

**ConstrainedGeneration.fs** — Probabilistic chord/scale generation:
```fsharp
// Generate chord progression using weighted grammar rules
val generateProgression:
    grammar: WeightedMusicRule list ->
    key: Key ->
    length: int ->
    style: string ->
    Chord list

// Generate scale sequence for improvisation over changes
val generateScaleChoices:
    grammar: WeightedMusicRule list ->
    changes: (Chord * int) list ->  // chord + duration in beats
    Scale list

// Constrained voicing search (grammar + MCTS)
val searchVoicings:
    grammar: WeightedMusicRule list ->
    chord: Chord ->
    tuning: Tuning ->
    maxFret: int ->
    Voicing list
```

### Wire into GuitarChordProgressionMCTS

The existing `GuitarChordProgressionMCTS` tool should use weighted grammar
rules to bias MCTS selection:

- UCB1 exploration should incorporate rule weights as prior probabilities
- Rollout policy should sample from softmax distribution over grammar rules
- Backpropagation should update rule weights via Bayesian update

### Wire into GA.Business.DSL

Extend the existing DSL infrastructure:

1. **Grammar weight files** alongside EBNF: `ChordProgression.weights.json`
   storing learned Beta-Binomial parameters per production
2. **Parser integration**: When multiple productions match, use weighted
   selection instead of first-match
3. **Source generator update**: Generate weight-aware parser variants

### Wire into GA.AI.Service

Add endpoints to the AI microservice:

- `POST /api/grammar/update-weights` — Bayesian update from user feedback
- `GET /api/grammar/weights/{grammarName}` — Current weight distribution
- `POST /api/grammar/evolve` — Run replicator dynamics on rule ecosystem
- `POST /api/grammar/generate` — Probabilistic generation

### IR Types (from Probabilistic Grammars Notebook)

Add music-domain IR types for constrained LLM output:

```fsharp
type PerformanceIntent =
    { Style: string           // "jazz", "classical", "blues"
      Tempo: int              // BPM
      DynamicRange: string    // "pp" to "ff"
      ArticulationHints: string list }

type HarmonyConstraint =
    { Key: string
      Mode: string
      AvoidNotes: string list
      PreferredIntervals: string list
      VoiceLeadingMaxDistance: int }  // semitones

type TechniquePlan =
    { Technique: string       // "fingerpicking", "strumming", "hybrid"
      PositionRange: int * int // fret range
      StringSet: int list      // which strings
      Difficulty: float }      // 0.0 to 1.0
```

### Persistence

- Weight files: `~/.ga/grammar_weights/` directory
- Per-grammar JSON: `{ "grammar": "ChordProgression", "rules": [...] }`
- Versioned with timestamp for A/B comparison

## Tests

Create `Tests/GA.Business.ProbabilisticGrammar.Tests/`:
- `WeightedMusicRuleTests.fs`: bayesianUpdate, softmax, selectWeighted
- `MusicReplicatorTests.fs`: step, detectStableIdioms, evolveFromPreferences
- `HarmonicFitnessTests.fs`: progression/voicing/scale fitness scoring
- `ConstrainedGenerationTests.fs`: generateProgression, searchVoicings
- `IntegrationTests.fs`: end-to-end grammar load → weight → generate → validate

## Cross-Repo Integration

- TARS can call GA's grammar weights endpoint to learn music-domain preferences
- GA can call TARS's replicator dynamics for fast multi-rule evolution
- MachinDeOuf provides Rust-speed MCTS for voicing search via `machin grammar search`

## Completion Signal

<promise>PROBABILISTIC GRAMMARS COMPLETE</promise>

Output this promise tag when:
1. `GA.Business.ProbabilisticGrammar` project builds
2. All test projects pass
3. Weight persistence working
4. At least one grammar (ChordProgression) has weighted generation working
5. `dotnet build AllProjects.slnx` succeeds
