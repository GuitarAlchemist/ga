namespace GA.Business.AI.Interpretation;

using GA.Business.Core.Atonal;
using System.Collections.Generic;

/// <summary>
/// Analysis results for Neo-Riemannian operations.
/// </summary>
public record NeoRiemannianResult(
    string Operation,
    string SourceTriad,
    string TargetTriad,
    int CommonTones,
    string Explanation);

/// <summary>
/// Rich interval content analysis for human-readable explanations.
/// </summary>
public record IntervalContentAnalysis(
    int TritoneCount,
    int SemitoneCount,
    int PerfectFifthCount,
    bool HasQuartalContent,
    string DissonanceLevel,
    string CharacterDescription);

/// <summary>
/// Modal interchange analysis for borrowed chord suggestions.
/// </summary>
public record ModalInterchangeAnalysis(
    string SourceMode,
    string ParallelMode,
    IReadOnlyList<string> BorrowedDegrees,
    IReadOnlyList<string> SuggestedBorrowedChords,
    string Explanation);

/// <summary>
/// Set complement analysis.
/// </summary>
public record SetComplementAnalysis(
    PitchClassSet OriginalSet,
    PitchClassSet ComplementSet,
    int ComplementCardinality,
    IReadOnlyList<string> ComplementNotes);

/// <summary>
/// A rich, narrative description of a musical object (chord, voicing, or progression).
/// </summary>
public record HarmonicStory(
    string Subject,
    string Title,
    string Narrative,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> SuggestedContexts);

