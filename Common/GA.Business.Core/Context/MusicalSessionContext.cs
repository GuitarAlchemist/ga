namespace GA.Business.Core.Context;

using System.Collections.Immutable;
using JetBrains.Annotations;
using GA.Domain.Core.Design.Attributes;
using GA.Domain.Core.Design.Schema;
using GA.Domain.Core.Instruments;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Core.Theory.Tonal.Scales;

/// <summary>
/// Represents the current musical context for a user session
/// </summary>
/// <remarks>
/// This is a pure domain object that captures the state of a musical learning/practice session.
/// It includes instrument configuration, musical context (key/scale), notation preferences,
/// and user proficiency information.
/// </remarks>
[PublicAPI]
[DomainInvariant("Session must have a valid tuning", "Tuning != null")]
[DomainRelationship(typeof(Tuning), RelationshipType.IsChildOf, "Session references a specific tuning")]
[DomainRelationship(typeof(Key), RelationshipType.IsChildOf, "Session may be in a specific key")]
[DomainRelationship(typeof(Scale), RelationshipType.IsChildOf, "Session may reference a scale")]
[DomainRelationship(typeof(Chord), RelationshipType.IsChildOf, "Session tracks last played chord")]
public sealed record MusicalSessionContext
{
    // ===== Instrument Configuration =====
    
    /// <summary>
    /// Current tuning being used
    /// </summary>
    public required Tuning Tuning { get; init; }
    
    /// <summary>
    /// Optional fretboard range constraint for searches and visualization
    /// </summary>
    public FretboardRange? ActiveRange { get; init; }
    
    // ===== Musical Context =====
    
    /// <summary>
    /// Current key (if any)
    /// </summary>
    public Key? CurrentKey { get; init; }
    
    /// <summary>
    /// Current scale being explored or practiced
    /// </summary>
    public Scale? CurrentScale { get; init; }
    
    /// <summary>
    /// Last chord that was played or referenced
    /// </summary>
    public Chord? LastPlayedChord { get; init; }
    
    // ===== Notation Preferences =====
    
    /// <summary>
    /// Notation style preference
    /// </summary>
    public NotationStyle NotationStyle { get; init; } = NotationStyle.Auto;
    
    /// <summary>
    /// Enharmonic spelling preference
    /// </summary>
    public EnharmonicPreference EnharmonicPreference { get; init; } = EnharmonicPreference.Context;
    
    // ===== User Proficiency =====
    
    /// <summary>
    /// User's skill level (if known)
    /// </summary>
    public SkillLevel? SkillLevel { get; init; }
    
    /// <summary>
    /// Techniques the user has mastered
    /// </summary>
    public ImmutableHashSet<string> MasteredTechniques { get; init; } = ImmutableHashSet<string>.Empty;
    
    // ===== Style Context =====
    
    /// <summary>
    /// Current musical genre context
    /// </summary>
    public MusicalGenre? CurrentGenre { get; init; }
    
    /// <summary>
    /// Preferred playing style
    /// </summary>
    public PlayingStyle? PlayingStyle { get; init; }
    
    // ===== Factory Methods =====
    
    /// <summary>
    /// Creates a default session context with standard guitar tuning
    /// </summary>
    public static MusicalSessionContext Default() =>
        new()
        {
        Tuning = Tuning.Default,
        NotationStyle = NotationStyle.Auto,
        EnharmonicPreference = EnharmonicPreference.Context
    };
    
    // ===== Scoped Operations (Immutable Updates) =====
    
    /// <summary>
    /// Returns a new context with the specified key
    /// </summary>
    public MusicalSessionContext WithKey(Key key) => this with { CurrentKey = key };
    
    /// <summary>
    /// Returns a new context with the specified scale
    /// </summary>
    public MusicalSessionContext WithScale(Scale scale) => this with { CurrentScale = scale };
    
    /// <summary>
    /// Returns a new context with the specified tuning
    /// </summary>
    public MusicalSessionContext WithTuning(Tuning tuning) => this with { Tuning = tuning };
    
    /// <summary>
    /// Returns a new context with the specified chord
    /// </summary>
    public MusicalSessionContext WithChord(Chord chord) => this with { LastPlayedChord = chord };
    
    /// <summary>
    /// Returns a new context with the specified skill level
    /// </summary>
    public MusicalSessionContext WithSkillLevel(SkillLevel skillLevel) => this with { SkillLevel = skillLevel };
    
    /// <summary>
    /// Returns a new context with the specified genre
    /// </summary>
    public MusicalSessionContext WithGenre(MusicalGenre genre) => this with { CurrentGenre = genre };
    
    /// <summary>
    /// Returns a new context with the specified fretboard range
    /// </summary>
    public MusicalSessionContext WithRange(FretboardRange range) => this with { ActiveRange = range };
    
    /// <summary>
    /// Returns a new context with the specified notation style
    /// </summary>
    public MusicalSessionContext WithNotationStyle(NotationStyle style) => this with { NotationStyle = style };
    
    /// <summary>
    /// Returns a new context with a mastered technique added
    /// </summary>
    public MusicalSessionContext WithMasteredTechnique(string technique) => this with
    {
        MasteredTechniques = MasteredTechniques.Add(technique)
    };
    
    /// <summary>
    /// Returns a new context with multiple mastered techniques
    /// </summary>
    public MusicalSessionContext WithMasteredTechniques(IEnumerable<string> techniques) => this with
    {
        MasteredTechniques = [.. techniques]
    };
}
