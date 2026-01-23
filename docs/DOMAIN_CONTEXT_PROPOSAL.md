# Domain Context Extraction Investigation

## Current State

Based on code analysis, there are **partial** context implementations scattered across the codebase:

### Existing Context Concepts

1. **`MusicalContext` enum** (in `EnharmonicNamingService.cs`)
   - Purpose: Determines enharmonic spelling preferences
   - Values: Classical, Jazz, Rock, etc.
   - Used for: Chord naming decisions

2. **"Conversation Context"** (mentioned in CHATBOT_ENHANCEMENTS_SUMMARY.md)
   - Purpose: Track chatbot conversation state
   - Scope: Recent queries, chords, scales, concepts
   - Location: `ConversationContextService` (not found in Common layer)

3. **`PracticeSessionRequest`** (in Personalization)
   - Purpose: User practice session configuration
   - Limited to practice scenarios

### What's Missing: A **Unified Domain Context**

Currently there is **no single, cohesive domain context** that represents:
- ✗ Current instrument & tuning
- ✗ Current key/scale
- ✗ User skill level
- ✗ Preferred notation style (sharps vs flats)
- ✗ Fretboard position/range preferences
- ✗ Musical genre/style context

## Proposed Design: `MusicalSessionContext`

### Layer Placement

```
GA.Domain.Core
  └── Session/                    (NEW)
      └── MusicalSessionContext.cs   ← Pure domain context

GA.Business.Core
  └── Session/                    (NEW)
      └── ISessionContextProvider.cs ← Application interface
```

### Core Context Model

```csharp
namespace GA.Domain.Core.Session;

/// <summary>
/// Represents the current musical context for a user session
/// </summary>
[DomainInvariant("Session must have a valid instrument", "Instrument != null")]
public sealed record MusicalSessionContext
{
    // Instrument Configuration
    public required Instrument Instrument { get; init; }
    public required Tuning Tuning { get; init; }
    public FretboardRange? ActiveRange { get; init; }
    
    // Musical Context
    public Key? CurrentKey { get; init; }
    public Scale? CurrentScale { get; init; }
    public Chord? LastPlayedChord { get; init; }
    
    // Notation Preferences
    public NotationStyle NotationStyle { get; init; } =NotationStyle.Auto;
    public EnharmonicPreference EnharmonicPreference { get; init; } = EnharmonicPreference.Context;
    
    // User Proficiency
    public SkillLevel? SkillLevel { get; init; }
    public HashSet<string> MasteredTechniques { get; init; } = [];
    
    // Style Context
    public MusicalGenre? CurrentGenre { get; init; }
    public PlayingStyle? PlayingStyle { get; init; }
    
    // Factory Methods
    public static MusicalSessionContext Default(Instrument instrument) => new()
    {
        Instrument = instrument,
        Tuning = instrument.DefaultTuning,
        NotationStyle = NotationStyle.Auto
    };
    
    // Scoped operations
    public MusicalSessionContext WithKey(Key key) => this with { CurrentKey = key };
    public MusicalSessionContext WithScale(Scale scale) => this with { CurrentScale = scale };
    public MusicalSessionContext WithTuning(Tuning tuning) => this with { Tuning = tuning };
}

public enum NotationStyle
{
    Auto,          // Context-aware
    PreferSharps,  // Always use sharps when ambiguous
    PreferFlats,   // Always use flats when ambiguous
    ScientificPitch // C4, D5, etc.
}

public enum EnharmonicPreference
{
    Context,    // Use musical context to decide
    Sharps,     // Always prefer sharps
    Flats,      // Always prefer flats
    Simplest    // Fewest accidentals
}

public enum SkillLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

public enum MusicalGenre
{
    Rock, Jazz, Blues, Classical, Metal, Folk, Country, Funk, Soul, RAndB
}

public enum PlayingStyle
{
    Rhythm, Lead, Fingerstyle, Hybrid, Tapping
}

public record FretboardRange(int MinFret, int MaxFret, HashSet<int> AvailableStrings);
```

### Application Service Interface

```csharp
namespace GA.Business.Core.Session;

public interface ISessionContextProvider
{
    /// <summary>
    /// Gets the current musical session context
    /// </summary>
    MusicalSessionContext GetContext();
    
    /// <summary>
    /// Updates the session context
    /// </summary>
    void UpdateContext(Func<MusicalSessionContext, MusicalSessionContext> updateFn);
    
    /// <summary>
    /// Resets context to defaults
    /// </summary>
    void ResetContext();
    
    /// <summary>
    /// Raised when context changes
    /// </summary>
    event EventHandler<MusicalSessionContext> ContextChanged;
}
```

## Benefits

### 1. **Chatbot Intelligence**

```csharp
// Chatbot can understand context
var context = _sessionContext.GetContext();

// "Show me chords" → filtered by current key
var chords = _chordService.GetChordsInKey(context.CurrentKey);

// "What scale am I in?" → direct answer
if (context.CurrentScale != null)
    return $"You're currently in {context.CurrentScale.Name}";

// "Transpose up a step" → knows what to transpose
if (context.LastPlayedChord != null)
    return context.LastPlayedChord.Transpose(Interval.MajorSecond);
```

### 2. **Smart Defaults & Filtering**

```csharp
// Voicing search respects context
var voicings = _voicingService.SearchVoicings(new VoicingQuery
{
    Chord = requestedChord,
    Tuning = context.Tuning,              // From context
    FretRange = context.ActiveRange,       // From context
    MaxStretch = context.SkillLevel.GetMaxStretch(), // Skill-aware
    Style = context.PlayingStyle           // Style-aware
});
```

### 3. **Personalized Notation**

```csharp
// Enharmonic spelling respects preferences
var noteName = context.EnharmonicPreference switch
{
    EnharmonicPreference.Context => GetContextualName(pitchClass, context.CurrentKey),
    EnharmonicPreference.Sharps => pitchClass.ToSharpNote().Name,
    EnharmonicPreference.Flats => pitchClass.ToFlatNote().Name,
    _ => GetSimplestName(pitchClass)
};
```

### 4. **UI State Management**

```csharp
// UI components can subscribe to context
_sessionContext.ContextChanged += (sender, newContext) =>
{
    UpdateFretboardDisplay(newContext.Tuning);
    UpdateKeyIndicator(newContext.CurrentKey);
    FilterChordPalette(newContext.SkillLevel);
};
```

## Implementation Phases

### Phase 1: Core Context Model
- [ ] Create `MusicalSessionContext` record in `GA.Domain.Core`
- [ ] Add supporting enums and value objects
- [ ] Write unit tests for context mutations

### Phase 2: Application Service
- [ ] Create `ISessionContextProvider` interface in `GA.Business.Core`
- [ ] Implement in-memory provider for single-user scenarios
- [ ] Implement persisted provider for multi-session scenarios

### Phase 3: Integration
- [ ] Integrate with chatbot for context-aware responses
- [ ] Update UI components to use context
- [ ] Migrate existing scattered context usage

### Phase 4: Advanced Features
- [ ] Context history/undo
- [ ] Context presets ("Jazz Session", "Metal Riffing")
- [ ] Context serialization for save/load
- [ ] Multi-user context isolation (for server scenarios)

## Context Scoping Strategies

### 1. **Per-User Scoped** (Web/Chatbot)
```csharp
services.AddScoped<ISessionContextProvider, UserSessionContextProvider>();
```

### 2. **Singleton** (Desktop App)
```csharp
services.AddSingleton<ISessionContextProvider, InMemorySessionContextProvider>();
```

### 3. **Transient** (Stateless/API)
```csharp
// Context passed explicitly in requests
public IActionResult Search([From Body] SearchRequest request)
{
    var context = request.Context ?? MusicalSessionContext.Default();
    // ...
}
```

## Related Patterns to Consider

### 1. **Builder Pattern** for Complex Contexts
```csharp
var context = MusicalSessionContext.Builder()
    .WithInstrument(Instruments.StandardGuitar)
    .WithTuning(Tunings.Standard)
    .InKey(Keys.C.Major)
    .ForSkillLevel(SkillLevel.Intermediate)
    .Build();
```

### 2. **Context Stack** for Temporary Changes
```csharp
using (var tempContext = _sessionContext.Push(ctx => ctx.WithKey(Keys.D.Major)))
{
    // Temporarily in D major
    var chords = GetDiatonicChords();
} // Reverts to previous key
```

### 3. **Context Snapshots** for Comparison
```csharp
var beforeTranspose = _sessionContext.CreateSnapshot();
TransposeEverything(Interval.PerfectFifth);
var afterTranspose = _sessionContext.CreateSnapshot();
var diff = beforeTranspose.Compare(afterTranspose);
```

## Questions to Consider

1. **Persistence**: Should context be saved between sessions?
2. **Scope**: Per-tab, per-browser, or per-user account?
3. **Synchronization**: Multi-device sync for logged-in users?
4. **Defaults**: Smart defaults based on user history/ML?
5. **Validation**: Should certain context combinations be invalid?

## Recommendation

**YES**, this is a valuable abstraction that should be formalized. The proposed `MusicalSessionContext` would:

1. ✅ Centralize scattered context tracking
2. ✅ Enable smarter chatbot responses
3. ✅ Improve UI state management
4. ✅ Support personalization
5. ✅ Maintain domain purity (pure data, no services)

**Next Steps**:
1. Review and refine the `MusicalSessionContext` model
2. Create implementation plan
3. Build Phase 1 (Core Model)
4. Integrate with chatbot as proof-of-concept
