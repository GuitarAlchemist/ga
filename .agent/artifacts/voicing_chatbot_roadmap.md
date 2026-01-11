# Voicing Search & Chatbot Implementation Roadmap

> **Goal:** Build a production-quality voicing search system that answers daily guitarist/composer queries with grounded, explainable results.

## Executive Summary

| Phase | Name | Duration | Primary Value |
|-------|------|----------|---------------|
| 0 | Lock the Contract | 1-2 evenings | Stop schema drift, ensure traceability |
| 1 | MongoDB Indexing & Query Primitives | 1 week | "What chord is this?", "Easier version?" |
| 2 | YAML → Knowledge Cards + Hybrid Retrieval | 3-4 days | "What scale works over this?" |
| 3 | Voice-Leading Engine | 1 week | "Smooth ii-V-I voicings around 7th fret" |
| 4 | Neo4j for Harmony Relationships | 1 week | Substitutions, cadences, functional harmony |
| 5 | Embeddings (Optional) | 3-4 days | "Airy, dark, crunchy, filmic" vibe queries |

---

## Phase 0: Lock the Contract

**Goal:** Stop schema drift and make every indexed item attributable.

**Duration:** 1-2 evenings

### 0.1 Define Stable IDs

| ID Type | Format | Example |
|---------|--------|---------|
| `VoicingId` | Hash of tuning + capo + string/fret pattern | `v_eadgbe_0_x32010` |
| `ChordId` | Root + quality template | `chord_C_maj7` |
| `PitchClassSetId` | Existing / Forte / prime form | `pcs_4-26` |
| `TuningId` | String names | `EADGBE`, `DADGAD` |

### 0.2 Freeze Analysis Result Contract

Create a stable `VoicingAnalysisResult` DTO:

```fsharp
type VoicingAnalysisResult = {
    // Provenance
    AnalysisEngine: string
    AnalysisVersion: string
    IndexedAt: DateTime
    
    // Identity (pitch-class set, chord candidates)
    PitchClassSetId: string
    ChordCandidates: RankedChordCandidate list
    RootCandidates: RankedRootCandidate list
    AmbiguityLevel: float  // 0 = certain, 1 = ambiguous
    
    // Realization (physical shape)
    StringFrets: int option array  // [None; Some 3; Some 2; Some 0; Some 1; Some 0]
    MidiNotes: int array
    TuningId: string
    Capo: int
    
    // Playability
    Span: int
    MinFret: int
    MaxFret: int
    BarreRequired: bool
    BarreInfo: string option
    DifficultyScore: float
    StringSet: string  // "All 6", "Top 4", etc.
    
    // Perceptual
    Brightness: float
    Roughness: float
    ConsonanceScore: float
    MudRisk: bool
    Spacing: string  // Close, Mixed, Open
    Register: string  // Low, Mid, High
    
    // Tags
    SemanticTags: string array
    StructuredTags: Map<string, string>
}
```

### 0.3 Add Tripwires (Tests)

- [ ] **Pipeline contract test**: Fails if wrong analyzer is wired
- [ ] **Placeholder detection**: Warns if brightness < 0.1 variance, consonance always ~0.7
- [ ] **Schema migration test**: Ensures new fields don't break old data

### 0.4 Deliverable Checklist

- [ ] Stable `VoicingDoc` schema documented
- [ ] All fields have XML doc comments
- [ ] One passing integration test for full indexing path
- [ ] Version stamp appears in indexed documents

---

## Phase 1: MongoDB Indexing & Query Primitives

**Goal:** Make "what chord is this?", "find easier version", "similar voicings" work fast.

**Duration:** ~1 week

### 1.1 Data Model in MongoDB

#### Collections

| Collection | Purpose |
|------------|---------|
| `voicings` | 2.8M+ voicing shapes with full analysis |
| `chord_cards` | One doc per chord identity (optional, for caching) |
| `configs` | Normalized YAML/TOML entities as typed docs |
| `jobs` | Index run metadata for provenance |

#### `voicings` Document Shape

```json
{
  "_id": "v_eadgbe_0_x32010",
  "ids": {
    "voicingId": "v_eadgbe_0_x32010",
    "pitchClassSetId": "4-26",
    "tuningId": "EADGBE"
  },
  "physical": {
    "stringFrets": [null, 3, 2, 0, 1, 0],
    "diagram": "x-3-2-0-1-0",
    "mutedStrings": [0],
    "openStrings": [3, 4, 5],
    "minFret": 0,
    "maxFret": 3,
    "span": 3
  },
  "sound": {
    "midiNotes": [48, 52, 55, 60, 64, 67],
    "bassNote": 48,
    "bassPitchClass": 0,
    "register": "Mid"
  },
  "identity": {
    "chordCandidates": [
      { "name": "C Major", "root": "C", "quality": "major", "confidence": 0.92 },
      { "name": "Am7 (no root)", "root": "A", "quality": "m7", "confidence": 0.45 }
    ],
    "rootCandidates": [
      { "pitchClass": 0, "score": 0.92 },
      { "pitchClass": 9, "score": 0.45 }
    ],
    "harmonicFunction": "Tonic",
    "isRootless": false
  },
  "perceptual": {
    "brightness": 0.58,
    "roughness": 0.22,
    "consonanceScore": 0.78,
    "mudRisk": false,
    "spacing": "Close"
  },
  "playability": {
    "difficulty": "Beginner",
    "difficultyScore": 0.15,
    "barre": false,
    "barreInfo": null,
    "stretch": 0.3,
    "fingerCount": 3
  },
  "tags": ["beginner", "cowboy-chord", "folk", "open-strings", "tonic"],
  "structuredTags": {
    "genre": "folk",
    "useCase": "campfire",
    "register": "mid"
  },
  "provenance": {
    "analysisEngine": "VoicingAnalyzer.AnalyzeEnhanced",
    "analysisVersion": "2026-01-03-v2",
    "indexedAt": "2026-01-03T22:30:00Z"
  }
}
```

#### Indexes

```javascript
// Compound filters
db.voicings.createIndex({ "ids.tuningId": 1, "physical.minFret": 1, "physical.maxFret": 1 })
db.voicings.createIndex({ "physical.span": 1, "playability.barre": 1 })
db.voicings.createIndex({ "playability.difficulty": 1 })

// Identity queries
db.voicings.createIndex({ "ids.pitchClassSetId": 1 })
db.voicings.createIndex({ "identity.chordCandidates.name": 1 })
db.voicings.createIndex({ "identity.chordCandidates.root": 1 })

// Tags (multi-key)
db.voicings.createIndex({ "tags": 1 })

// Perceptual range queries
db.voicings.createIndex({ "perceptual.brightness": 1 })
db.voicings.createIndex({ "perceptual.consonanceScore": 1 })

// Bass note (for slash chord queries)
db.voicings.createIndex({ "sound.bassPitchClass": 1 })
```

### 1.2 Query Endpoints (CLI/API)

#### `identify` - What chord is this?

```
Input:  frets/strings OR MIDI notes
Output: Ranked chord candidates + explanation + voicing features

Example:
  ga identify --frets x32010
  
  Chord Candidates:
  1. C Major (92% confidence)
     - Root: C (bass)
     - Guide tones: E, G present
     - Function: Tonic
  2. Am7 (no root) (45% confidence)
     - Ambiguous: missing A
```

#### `find-voicings` - How do I play this chord?

```
Input:  chord spec + constraints
Output: Best voicing shapes matching criteria

Example:
  ga find-voicings "Dm7" --no-barre --around-fret 5 --top-4-strings
  
  Top 3 voicings:
  1. x-x-7-5-6-5 (Dm7, Mid-High, difficulty: Intermediate)
  2. x-x-5-5-6-5 (Dm7, Mid, difficulty: Beginner, barre: false)
```

#### `similar-voicings` - Find variations

```
Input:  voicingId + constraints
Output: Nearest neighbors by similarity

Example:
  ga similar --to x32010 --easier
  
  Similar voicings (sorted by similarity):
  1. x32000 (C5, 95% similar, easier: removes E)
  2. x30010 (Cmaj7 shell, 88% similar, jazz voicing)
```

#### `vibe-search` - Find by feel

```
Input:  descriptive text + constraints
Output: Tagged/perceptual matches

Example:
  ga vibe "airy tense neo-soul" --top-4-strings
  
  Matching voicings:
  1. x-x-6-5-6-5 (Dm9, brightness: 0.72, spacing: Open)
  2. x-x-8-7-8-7 (Em9, brightness: 0.68, spacing: Open)
```

### 1.3 Similarity Metric (No Vectors Yet)

```csharp
public static double ComputeSimilarity(VoicingEntity a, VoicingEntity b)
{
    // Pitch class overlap (Jaccard)
    var pcsA = a.PitchClasses.ToHashSet();
    var pcsB = b.PitchClasses.ToHashSet();
    var jaccard = pcsA.Intersect(pcsB).Count() / (double)pcsA.Union(pcsB).Count();
    
    // Perceptual distance
    var perceptualDist = Math.Abs(a.Brightness - b.Brightness) +
                         Math.Abs(a.Roughness - b.Roughness) +
                         Math.Abs(a.ConsonanceScore - b.ConsonanceScore);
    
    // Position distance
    var positionDist = Math.Abs(a.MinFret - b.MinFret) / 12.0;
    
    // Difficulty penalty
    var diffPenalty = a.DifficultyScore > b.DifficultyScore ? 0.1 : 0;
    
    return jaccard * 0.5 - perceptualDist * 0.2 - positionDist * 0.2 - diffPenalty;
}
```

### 1.4 Deliverable Checklist

- [ ] `identify` command works with chord diagram input
- [ ] `find-voicings` returns filtered results under constraints
- [ ] `similar-voicings` returns nearest neighbors
- [ ] All outputs include provenance stamp
- [ ] Basic `vibe-search` with tag matching

---

## Phase 2: YAML → Knowledge Cards + Hybrid Retrieval

**Goal:** Answer "what scale works over this chord?" with citations.

**Duration:** 3-4 days

### 2.1 Typed Loading + Validation

#### YAML Sources

| File | Entity Type | Key Fields |
|------|-------------|------------|
| `Modes.yaml` | Mode definitions | intervals, characteristic tones, avoid notes |
| `Scales.yaml` | Scale families | parent modes, aliases |
| `ModalScalesConfig.yaml` | Mode-chord compatibility | chord qualities, tensions |
| `Instruments.yaml` | Tuning definitions | string pitches, range |
| `IconicChords.yaml` | Famous voicings | song references, genre |

#### Validation Rules

```fsharp
let validateMode (mode: ModeConfig) =
    // Intervals must be within 0-11
    mode.Intervals |> List.forall (fun i -> i >= 0 && i <= 11)
    
let validateScale (scale: ScaleConfig) =
    // Parent mode reference must exist
    ModeCatalog.exists scale.ParentMode
    
let validateTuning (tuning: TuningConfig) =
    // String count must match pitch array
    tuning.StringPitches.Length = tuning.StringCount
```

### 2.2 Build Knowledge Cards

Each entity gets a "card" for RAG:

```json
{
  "_id": "mode_dorian",
  "type": "mode",
  "name": "Dorian",
  "aliases": ["jazz minor", "Dm scale flavor"],
  "intervals": [0, 2, 3, 5, 7, 9, 10],
  "characteristicTones": ["♮6"],
  "avoidNotes": [],
  "commonCadences": ["i-IV", "i-bVII"],
  "cardText": "Dorian is the second mode of the major scale. Its characteristic tone is the raised 6th (♮6), which distinguishes it from natural minor. Common in jazz, funk, and modal rock. Works great over m7 chords. No avoid notes - all tones usable.",
  "guitarTips": "Works great with minor pentatonic + added 6th. Top 4 strings give airy quality.",
  "compatibleChordQualities": ["m7", "m9", "m11", "m13"]
}
```

### 2.3 Chord → Scale Compatibility Engine

```fsharp
module ChordScaleCompatibility =
    
    /// Given chord identity, return compatible modes/scales with reasons
    let findCompatibleScales (chord: ChordIdentity) : CompatibilityResult list =
        let pitchClasses = chord.PitchClassSet
        
        ModeCatalog.all
        |> List.map (fun mode ->
            let scalePC = mode.IntervalsFromRoot chord.Root
            let coverage = pitchClasses |> Set.filter (fun pc -> Set.contains pc scalePC)
            let avoidConflict = mode.AvoidNotes |> List.exists (fun an -> Set.contains an pitchClasses)
            
            {
                Mode = mode
                CoverageScore = float coverage.Count / float pitchClasses.Count
                HasAvoidConflict = avoidConflict
                Reasons = generateReasons mode chord
            }
        )
        |> List.filter (fun r -> r.CoverageScore >= 0.8 && not r.HasAvoidConflict)
        |> List.sortByDescending (fun r -> r.CoverageScore)
```

### 2.4 Deliverable Checklist

- [ ] All YAML files parsed with validation
- [ ] Knowledge cards generated for modes, scales, iconic chords
- [ ] `chord-scales` command returns compatible modes with reasons
- [ ] Cards include guitar-specific tips

---

## Phase 3: Voice-Leading Engine

**Goal:** Answer "give me smooth ii-V-I voicings around 7th fret" musically.

**Duration:** ~1 week

### 3.1 Define TransitionAnalysis

```csharp
public record TransitionAnalysis(
    VoicingEntity From,
    VoicingEntity To,
    int CommonToneCount,
    int TotalMotionSemitones,
    int MaxVoiceLeap,
    bool SameStringContinuity,
    bool BarreIntroduced,
    int SpanChange,
    double OverallCost
);

public static TransitionAnalysis Analyze(VoicingEntity from, VoicingEntity to)
{
    // Find optimal voice assignment (minimize total motion)
    var assignment = FindMinimalMotionAssignment(from.MidiNotes, to.MidiNotes);
    
    var commonTones = assignment.Where(a => a.Motion == 0).Count();
    var totalMotion = assignment.Sum(a => Math.Abs(a.Motion));
    var maxLeap = assignment.Max(a => Math.Abs(a.Motion));
    
    // Ergonomic factors
    var sameStrings = assignment.Where(a => a.FromString == a.ToString).Count();
    var barreIntroduced = !from.BarreRequired && to.BarreRequired;
    var spanChange = Math.Abs(to.Span - from.Span);
    
    // Overall cost (lower = smoother)
    var cost = totalMotion * 0.3 +
               maxLeap * 0.2 +
               (barreIntroduced ? 2 : 0) +
               spanChange * 0.1 -
               commonTones * 0.5 -
               sameStrings * 0.1;
    
    return new TransitionAnalysis(from, to, commonTones, totalMotion, maxLeap, 
                                   sameStrings > 0, barreIntroduced, spanChange, cost);
}
```

### 3.2 Progression Solver

```csharp
public class ProgressionSolver
{
    public VoicingPath Solve(
        ChordIdentity[] progression,
        VoicingConstraints constraints,
        int candidatesPerChord = 10)
    {
        // 1. Generate candidate voicings per chord
        var candidateSets = progression
            .Select(chord => FindVoicings(chord, constraints).Take(candidatesPerChord))
            .ToArray();
        
        // 2. Dynamic programming for minimum cost path
        var dp = new Dictionary<(int chordIndex, string voicingId), (double cost, string[] path)>();
        
        // Initialize first chord
        foreach (var v in candidateSets[0])
            dp[(0, v.Id)] = (0, [v.Id]);
        
        // Forward pass
        for (int i = 1; i < progression.Length; i++)
        {
            foreach (var currV in candidateSets[i])
            {
                var bestPrev = dp
                    .Where(kv => kv.Key.chordIndex == i - 1)
                    .Select(kv => {
                        var prevV = GetVoicing(kv.Key.voicingId);
                        var trans = TransitionAnalysis.Analyze(prevV, currV);
                        return (cost: kv.Value.cost + trans.OverallCost, 
                                path: kv.Value.path.Append(currV.Id).ToArray());
                    })
                    .MinBy(x => x.cost);
                
                dp[(i, currV.Id)] = bestPrev;
            }
        }
        
        // 3. Return best path
        var finalBest = dp
            .Where(kv => kv.Key.chordIndex == progression.Length - 1)
            .MinBy(kv => kv.Value.cost);
        
        return new VoicingPath(finalBest.Value.path, finalBest.Value.cost);
    }
}
```

### 3.3 CLI Command

```
ga voice-lead "Dm7 G7 Cmaj7" --around-fret 7 --no-barre

Optimal Voice-Leading Path:
  Dm7:    x-x-7-5-6-5  (start)
  G7:     x-x-5-7-6-7  (3 common tones, +4 semitones total)
  Cmaj7:  x-x-5-5-5-7  (2 common tones, +3 semitones total)
  
Total motion: 7 semitones
Max leap: 2 semitones
Smoothness score: 0.85
```

### 3.4 Deliverable Checklist

- [ ] `TransitionAnalysis` computes voice-leading metrics
- [ ] `ProgressionSolver` finds optimal paths via DP
- [ ] `voice-lead` CLI command works
- [ ] Explanations include common tones, motion, leaps

---

## Phase 4: Neo4j for Harmony Relationships

**Goal:** Answer "what can I substitute for D7?" with graph traversal.

**Duration:** ~1 week

### 4.1 Graph Model (Keep It Small)

#### Nodes

| Label | Properties | Example |
|-------|------------|---------|
| `ChordIdentity` | root, quality, pitchClassSetId | `(C:major)` |
| `PitchClassSet` | id, forteNumber | `(4-26)` |
| `Mode` | name, familyName, intervals | `(Dorian)` |
| `Scale` | name, parentMode | `(C Dorian)` |
| `Cadence` | name, pattern | `(ii-V-I)` |
| `Function` | name | `(Dominant)` |
| `GenreTag` | name | `(jazz)` |

#### Edges

```cypher
// Functional harmony
(chord)-[:HAS_FUNCTION {inKey: "C"}]->(function)

// Substitutions
(D7)-[:SUBSTITUTES_FOR {type: "tritone"}]->(Ab7)
(Dm7)-[:SUBSTITUTES_FOR {type: "backdoor"}]->(Db7)

// Resolutions
(G7)-[:RESOLVES_TO {strength: 1.0}]->(Cmaj7)
(Db7)-[:RESOLVES_TO {strength: 0.8, type: "backdoor"}]->(Cmaj7)

// Compatibility
(Dm7)-[:COMPATIBLE_WITH]->(Dorian)
(Dm7)-[:COMPATIBLE_WITH]->(Melodic Minor)

// Genre associations
(sus4)-[:COMMON_IN]->(gospel)
(m7b5)-[:COMMON_IN]->(jazz)

// Chord families
(Cmaj7)-[:VARIANT_OF]->(C triad)
(C6)-[:VARIANT_OF]->(C triad)
```

### 4.2 Query Patterns

```cypher
// Tritone substitution
MATCH (orig:ChordIdentity {root: "D", quality: "7"})
      -[:SUBSTITUTES_FOR {type: "tritone"}]->(sub)
RETURN sub

// What resolves to C?
MATCH (chord)-[:RESOLVES_TO]->(target:ChordIdentity {root: "C"})
RETURN chord, rel.type, rel.strength
ORDER BY rel.strength DESC

// Compatible scales for Dm7
MATCH (chord:ChordIdentity {root: "D", quality: "m7"})
      -[:COMPATIBLE_WITH]->(mode:Mode)
RETURN mode.name, mode.characteristicTones
```

### 4.3 Join Back to MongoDB

```csharp
// Graph returns chord identities, Mongo returns realizations
public async Task<VoicingEntity[]> FindSubstitutes(
    string chordName, 
    VoicingConstraints constraints)
{
    // 1. Query Neo4j for substitute chord identities
    var substituteIds = await _neo4j.QueryAsync<string>(
        """
        MATCH (c:ChordIdentity {name: $name})-[:SUBSTITUTES_FOR]->(sub)
        RETURN sub.pitchClassSetId
        """, 
        new { name = chordName });
    
    // 2. Query MongoDB for voicings of those pitch class sets
    var filter = Builders<VoicingEntity>.Filter.In(
        v => v.PitchClassSetId, substituteIds);
    
    return await _mongo.Voicings
        .Find(filter & constraints.ToFilter())
        .SortBy(v => v.DifficultyScore)
        .Limit(10)
        .ToListAsync();
}
```

### 4.4 Deliverable Checklist

- [ ] Neo4j schema with nodes and edges
- [ ] Substitution queries work
- [ ] Cadence pattern matching works
- [ ] Integration with MongoDB for voicing realization

---

## Phase 5: Embeddings (Optional)

**Goal:** Upgrade "airy / dark / crunchy" vibe queries to semantic.

**Duration:** 3-4 days

### 5.1 What to Embed

| Item | Text Source |
|------|-------------|
| Voicing cards | Rendered features + tags as natural language |
| Mode cards | Definition + tips + characteristic tones |
| Iconic chord cards | Song references + genre + description |

### 5.2 Embedding Pipeline

```fsharp
let generateVoicingCardText (v: VoicingEntity) =
    $"""
    {v.ChordName} voicing at fret {v.MinFret}.
    Register: {v.Register}. Spacing: {v.Spacing}.
    Brightness: {v.Brightness:F2}. Roughness: {v.Roughness:F2}.
    {if v.MayBeMuddy then "May sound muddy in low register." else ""}
    Difficulty: {v.Difficulty}. {if v.BarreRequired then "Requires barre." else "No barre needed."}
    Tags: {String.concat ", " v.SemanticTags}
    """

let embedVoicing (v: VoicingEntity) =
    let text = generateVoicingCardText v
    embeddingModel.Embed text
```

### 5.3 Storage Options

| Option | Pros | Cons |
|--------|------|------|
| MongoDB Atlas Vector Search | Single database | Atlas-only, cost |
| Qdrant | Fast, OSS | Extra service |
| Milvus | Scalable | Overkill for 3M docs |

### 5.4 Query Flow

```
User: "airy tense neo-soul"
  ↓
[Embed query]
  ↓
[Vector similarity search] → top 100 candidates
  ↓
[Apply structured filters] → top 10 matching constraints
  ↓
[Return with explanations]
```

### 5.5 Deliverable Checklist

- [ ] Voicing cards embedded and indexed
- [ ] Mode/scale cards embedded
- [ ] `vibe-search` uses semantic similarity
- [ ] Hybrid search: vector + structured filters

---

## Implementation Priority Order

Based on maximum payoff per engineering hour:

1. **Phase 1.1-1.2** (1 week) - MongoDB schema + query endpoints
   - Immediate value: "what chord is this?", "easier version?"
   
2. **Phase 3** (1 week) - Voice-leading solver
   - Biggest "wow" factor for musicians
   - Makes progressions feel musical
   
3. **Phase 2** (3-4 days) - YAML knowledge cards
   - Enables "what scale over this chord?"
   - Grounds answers in your curated data

4. **Phase 4** (1 week) - Neo4j for harmony
   - Only if you actually need graph traversal
   - Substitutions, cadences, functional harmony

5. **Phase 5** (3-4 days) - Embeddings
   - Nice-to-have once structured core is solid
   - Enables natural language vibe queries

---

## Success Criteria

### Phase 1 "Usable Bot" Bar
- [ ] Identify chord from shape reliably (>90% accuracy on common chords)
- [ ] Find voicings by chord + constraints (<500ms)
- [ ] Find easier/similar voicings with explanations
- [ ] All outputs stamped with engine/version

### Phase 3 "Musical" Bar
- [ ] Voice-leading solver gives smooth paths
- [ ] Transition explanations are numeric and coherent
- [ ] ii-V-I progression produces "correct" jazz voicings

### Phase 4 "Composer Brain" Bar
- [ ] Tritone substitutions work
- [ ] Cadence patterns matchable
- [ ] Functional harmony queries return expected results

---

## Files to Create/Modify

### New Files
- [ ] `Common/GA.Business.Core/Fretboard/Voicings/VoicingDoc.cs` - Stable DTO
- [ ] `Common/GA.Business.Core/Fretboard/VoiceLeading/TransitionAnalysis.cs`
- [ ] `Common/GA.Business.Core/Fretboard/VoiceLeading/ProgressionSolver.cs`
- [ ] `Common/GA.Business.Core/Tonal/ChordScaleCompatibility.cs`
- [ ] `GaCLI/Commands/IdentifyCommand.cs`
- [ ] `GaCLI/Commands/VoiceLeadCommand.cs`
- [ ] `GaCLI/Commands/ChordScalesCommand.cs`
- [ ] `Tests/GA.Business.Core.Tests/PipelineContractTests.cs`

### Modify
- [ ] `GA.Data.MongoDB/Models/VoicingEntity.cs` - Add structured tags, nested objects
- [ ] `GaCLI/Commands/SearchVoicingsCommand.cs` - Add similarity search
- [ ] `Common/GA.Business.Core/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs` - Ensure stable output

---

*Last updated: 2026-01-03*
*Status: Phase 0 in progress (schema locked, tripwires pending)*
