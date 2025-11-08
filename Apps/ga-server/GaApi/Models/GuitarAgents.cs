namespace GaApi.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
///     Represents the base payload shared across guitar progression agent requests.
/// </summary>
public abstract record GuitarProgressionAgentRequest
{
    /// <summary>
    ///     The starting chord progression expressed as chord symbols.
    /// </summary>
    [Required]
    [MinLength(2)]
    public required List<string> Progression { get; init; }

    /// <summary>
    ///     Optional key centre or modal reference (e.g. "C Major", "A Dorian").
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    ///     Optional stylistic tag such as genre or era (e.g. "neo-soul", "bossa nova").
    /// </summary>
    public string? Style { get; init; }

    /// <summary>
    ///     Free-form notes from the guitarist about their goals or restrictions.
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>
///     Request body used to ask the agent to embellish an existing progression.
/// </summary>
public sealed record SpiceUpProgressionRequest : GuitarProgressionAgentRequest
{
    /// <summary>
    ///     Optional mood or energy target such as "lush", "suspenseful", "floaty".
    /// </summary>
    public string? Mood { get; init; }

    /// <summary>
    ///     When true, keeps the cadence and arrival chords intact.
    /// </summary>
    public bool PreserveCadence { get; init; } = true;

    /// <summary>
    ///     When true, prefers substitutions that remain three frets or fewer away from the source voicing.
    /// </summary>
    public bool FavorCloseVoicings { get; init; } = true;
}

/// <summary>
///     Request body for reharmonisation tasks.
/// </summary>
public sealed record ReharmonizeProgressionRequest : GuitarProgressionAgentRequest
{
    /// <summary>
    ///     Optional description of the desired dramatic arc (for example, "darker bridge", "chromatic lifts").
    /// </summary>
    public string? TargetFeel { get; init; }

    /// <summary>
    ///     When true, keeps the very first chord untouched.
    /// </summary>
    public bool LockFirstChord { get; init; } = true;

    /// <summary>
    ///     When true, keeps the final resolution chord untouched.
    /// </summary>
    public bool LockLastChord { get; init; } = true;

    /// <summary>
    ///     Allows the agent to introduce temporary tonic shifts or modal interchange.
    /// </summary>
    public bool AllowModalInterchange { get; init; } = true;
}

/// <summary>
///     Request body for generating a brand new progression for a given brief.
/// </summary>
public sealed record CreateProgressionRequest
{
    [Required] [MinLength(2)] public required string Key { get; init; }

    /// <summary>
    ///     Optional explicit mode within the key (e.g. "Mixolydian", "Aeolian").
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>
    ///     Musical genre reference (e.g. "lofi hip hop", "bebop", "progressive metal").
    /// </summary>
    public string? Genre { get; init; }

    /// <summary>
    ///     Emotional tone of the progression.
    /// </summary>
    public string? Mood { get; init; }

    /// <summary>
    ///     Skill level of the guitarist (beginner, intermediate, advanced).
    /// </summary>
    public string? SkillLevel { get; init; }

    /// <summary>
    ///     Number of bars to aim for when constructing the progression.
    /// </summary>
    [Range(4, 32)]
    public int Bars { get; init; } = 8;

    /// <summary>
    ///     Optional list of artists, songs, or references that capture the intended vibe.
    /// </summary>
    [MaxLength(8)]
    public List<string>? ReferenceArtists { get; init; }

    /// <summary>
    ///     Additional creative constraints or requests.
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>
///     Result returned by the guitar agent orchestration service.
/// </summary>
public sealed record GuitarAgentResponse(
    string Title,
    string Summary,
    IReadOnlyList<string> Progression,
    IReadOnlyList<GuitarAgentSection> Sections,
    IReadOnlyList<string> PracticeIdeas,
    AgentTokenUsage? TokenUsage,
    bool UsedStructuredOutput,
    string? RawText,
    IReadOnlyList<string> Warnings,
    AgentResponseMetadata Metadata);

/// <summary>
///     Breakdown of a single musical idea suggested by the agent.
/// </summary>
public sealed record GuitarAgentSection(
    string Focus,
    IReadOnlyList<string> Chords,
    string Description,
    IReadOnlyList<string> VoicingTips,
    IReadOnlyList<string> TechniqueTips);

/// <summary>
///     Simplified token usage summary exposed by the API.
/// </summary>
public sealed record AgentTokenUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);

/// <summary>
///     Additional metadata captured from the agent run.
/// </summary>
public sealed record AgentResponseMetadata(string AgentId, DateTimeOffset? CreatedAt, string? ResponseId);
