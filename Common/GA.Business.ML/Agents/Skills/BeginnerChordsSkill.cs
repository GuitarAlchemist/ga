namespace GA.Business.ML.Agents.Skills;

using System.Text;

/// <summary>
/// Returns a curated catalogue of open-position guitar chords for beginners —
/// zero LLM calls. Replies to "easy chords", "beginner chords", "first chords
/// to learn" etc. with the chord name, diagram, and a one-line note about
/// fingering or common usage.
/// </summary>
/// <remarks>
/// Chord diagrams use the standard 6-string format <c>E A D G B e</c>
/// (low-to-high), where each token is the fret to play (0 = open string,
/// x = muted). The eight chords here are the standard "first eight" most
/// guitar curricula start with.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("BeginnerChordsSkill", "catalog")]
public sealed class BeginnerChordsSkill(ILogger<BeginnerChordsSkill> logger) : IOrchestratorSkill
{
    public string Name        => "BeginnerChords";
    public string Description =>
        "Returns a curated list of open-position guitar chords for beginners — " +
        "C, G, D, A, E, Am, Em, Dm — with diagrams and a one-line tip per chord. " +
        "Pure catalog lookup, no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Show me some easy beginner chords",
        "What are the first chords I should learn?",
        "Give me some simple guitar chords for a beginner",
        "I'm just starting out — what chords should I practice?",
        "List basic open chords",
        "Easy chords for a new guitarist",
        "What are the most common beginner chords?",
    ];

    /// <summary>
    /// Frets are listed low-to-high (E A D G B e). 'x' = mute, '0' = open.
    /// </summary>
    private static readonly (string Name, string Diagram, string Note)[] _chords =
    [
        ("C major",  "x-3-2-0-1-0", "First-position major. Watch the muted low E."),
        ("G major",  "3-2-0-0-0-3", "Use ring/middle/pinky on E-A-e for cleaner switching to D."),
        ("D major",  "x-x-0-2-3-2", "Compact triad — handy for 'D shape' barre training later."),
        ("A major",  "x-0-2-2-2-0", "Index/middle/ring on D-G-B; or barre with one finger across the 2nd fret."),
        ("E major",  "0-2-2-1-0-0", "Strongest, fullest open chord — every string rings."),
        ("A minor",  "x-0-2-2-1-0", "Same shape as E, moved one string up; bedrock minor sound."),
        ("E minor",  "0-2-2-0-0-0", "Easiest chord on the guitar — two fingers, six strings ringing."),
        ("D minor",  "x-x-0-2-3-1", "Compact like D major; good for swapping with C and G in folk songs."),
    ];

    public bool CanHandle(string message) => false; // semantic-routing only — see ExamplePrompts

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here are eight open-position chords every beginner should learn first. Diagrams are low-to-high (E A D G B e); `0` = open string, `x` = mute.");
        sb.AppendLine();

        for (var i = 0; i < _chords.Length; i++)
        {
            var (name, diagram, note) = _chords[i];
            sb.AppendLine($"{i + 1}. **{name}** — `{diagram}` — {note}");
        }

        sb.AppendLine();
        sb.Append("Practice tip: drill C ↔ G ↔ D ↔ Am ↔ Em as a smooth loop. Those five cover most folk, pop, and country songs in major keys. Add A, E, and Dm next.");

        var evidence = _chords.Select(c => $"{c.Name}: {c.Diagram}").ToList();
        evidence.Add($"Catalog size: {_chords.Length} chords");

        logger.LogDebug("BeginnerChordsSkill: returned {Count} catalog entries", _chords.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId     = AgentIds.Theory,
            Result      = sb.ToString(),
            Confidence  = 1.0f,
            Evidence    = evidence,
            Assumptions = ["Standard tuning EADGBe, open position"],
        });
    }
}
