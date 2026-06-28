namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Primitives.Notes;

/// <summary>
/// Answers "what is the interval between C and G?" / "C to G interval" /
/// "distance from F# to D" style queries from the domain — zero LLM calls.
/// Uses <see cref="Note.Sharp"/> / <see cref="Note.Flat"/> / <see cref="Note.Accidented"/>
/// extension methods to compute the simple interval and report quality + size + semitones.
/// </summary>
/// <remarks>
/// Registered at the orchestrator level. Returns the interval name (e.g. "P5"),
/// expanded form ("perfect fifth"), and semitone count as evidence.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("Interval", "interval")]
public sealed class IntervalSkill(ILogger<IntervalSkill> logger) : IOrchestratorSkill
{
    public string Name        => "Interval";
    public string Description =>
        "Names the interval between two single pitches (C to G = perfect " +
        "fifth, F# to D = minor sixth, A to E = perfect fifth). Returns " +
        "interval quality, size, and semitone count. Pure domain " +
        "computation, zero LLM calls.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What is the interval between C and G?",
        "Distance from F# to D",
        "Interval from C to E",
        "How many semitones from A to E?",
        "What's the interval between D and Bb?",
        // Definitional pattern — "what is/what's a [interval name]". Was
        // losing to CircleOfFifthsSkill (perfect-fifth keyword overlap) and
        // ScaleInfoSkill ("up from [note]" → scale-y). Added 2026-05-12 to
        // close in-2 and in-4 misroutes.
        "What's a perfect fifth?",
        "What is a major sixth?",
        "Define a minor third",
        // "[interval name] up/above/below [note]" pattern.
        "Minor third up from D",
        "Perfect fourth above C",
        "Major sixth above G",
        // v0.5 corpus expansion (2026-05-12): semitone-count paraphrases.
        // Bare "What is a tritone" was REMOVED here after it pulled the
        // cosine for "tritone substitution for D7" away from
        // chordsubstitution. The "augmented fourth definition" form
        // covers the same concept without the chord-substitution overlap.
        "augmented fourth definition",
        "semitones in a major sixth",
    ];

    // Capture two note names with optional accidentals — order matters, "from X to Y"
    // is the natural reading direction; we treat the first as the lower note.
    // Examples that should match:
    //   "what is the interval between C and G"
    //   "interval from C to G"
    //   "interval C to G"
    //   "C to G interval"
    //   "distance from F# to D"
    private static readonly Regex IntervalPattern = new(
        @"\b([A-Ga-g][#b]?)\s*(?:to|and)\s+([A-Ga-g][#b]?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var q = message.ToLowerInvariant();
        // Require an interval-intent keyword AND a "X to Y" / "X and Y" pair to
        // avoid grabbing scale or chord questions that happen to mention two notes.
        var hasIntent = q.Contains("interval") || q.Contains("distance");
        return hasIntent && IntervalPattern.IsMatch(message);
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var match = IntervalPattern.Match(message);
        if (!match.Success)
            return Task.FromResult(CannotHelp("Could not parse two note names from your question."));

        if (!IntervalNaming.TryParseNote(match.Groups[1].Value, out var note1) ||
            !IntervalNaming.TryParseNote(match.Groups[2].Value, out var note2))
        {
            return Task.FromResult(CannotHelp(
                $"I don't recognise \"{match.Groups[1].Value}\" or \"{match.Groups[2].Value}\" as a standard note name. " +
                "Try notes like C, F#, Bb, etc."));
        }

        // Note.Accidented.GetInterval is the canonical interval-between-two-notes API.
        var interval = note1.GetInterval(note2);
        var name1    = IntervalNaming.FormatNote(match.Groups[1].Value);
        var name2    = IntervalNaming.FormatNote(match.Groups[2].Value);
        var qualName = IntervalNaming.QualityLongName(interval.Quality.ToString());
        var sizeName = IntervalNaming.SizeOrdinalName(interval.Size.Value);
        var direction = note1.PitchClass.Value <= note2.PitchClass.Value ? "above" : "below or above";

        logger.LogDebug(
            "IntervalSkill: {Note1} → {Note2} = {Interval} ({Semitones} semitones)",
            name1, name2, interval.Name, interval.Semitones.Value);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = $"From **{name1}** to **{name2}** is a **{qualName} {sizeName}** ({interval.Name}, {interval.Semitones.Value} semitones).",
            Confidence = 1.0f,
            Evidence   =
            [
                $"Lower note: {name1}",
                $"Upper note: {name2} ({direction} the lower)",
                $"Interval: {interval.Name} ({qualName} {sizeName})",
                $"Semitones: {interval.Semitones.Value}",
            ],
            Assumptions = [],
        });
    }

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Theory,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"],
    };
}
