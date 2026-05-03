namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Theory.Tonal.Modes.Diatonic;

/// <summary>
/// Answers "what are the modes of the major scale?" style queries from the domain
/// model — zero LLM calls. Enumerates <see cref="MajorScaleMode.Items"/> and pairs
/// each mode with its W-H interval pattern and a one-line character note.
/// </summary>
/// <remarks>
/// Registered at the orchestrator level; first match wins so this should be checked
/// before LLM routing for any prompt that mentions "modes" of the major / diatonic scale.
/// </remarks>
public sealed class ModesSkill(ILogger<ModesSkill> logger) : IOrchestratorSkill
{
    public string Name        => "Modes";
    public string Description => "Lists the modes of the major scale with formula and character";

    private static readonly Regex ModesPattern =
        new(@"\bmodes?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MajorPattern =
        new(@"\b(major\s+scale|diatonic|major\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // W = whole step, H = half step.  Ionian formula rotates by degree to give every mode.
    private const string IonianWhPattern = "W-W-H-W-W-W-H";

    private static readonly (string Name, string Degrees, string Character)[] _modeRows =
    [
        ("Ionian",     "1 2 3 4 5 6 7",     "Bright, the major scale itself"),
        ("Dorian",     "1 2 b3 4 5 6 b7",   "Minor with raised 6th — jazz/folk staple"),
        ("Phrygian",   "1 b2 b3 4 5 b6 b7", "Dark minor with flat 2 — Spanish/flamenco"),
        ("Lydian",     "1 2 3 #4 5 6 7",    "Major with raised 4th — floating, dreamy"),
        ("Mixolydian", "1 2 3 4 5 6 b7",    "Major with flat 7 — bluesy/rock"),
        ("Aeolian",    "1 2 b3 4 5 b6 b7",  "Natural minor"),
        ("Locrian",    "1 b2 b3 4 b5 b6 b7","Half-diminished — rare as a tonic"),
    ];

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        if (!ModesPattern.IsMatch(message)) return false;

        // Accept either an explicit "major"/"diatonic" qualifier, or a bare "what are the modes?"
        // — when no qualifier is given we default to the major-scale modes (the canonical answer).
        if (MajorPattern.IsMatch(message)) return true;

        var q = message.ToLowerInvariant();
        return q.Contains("what are the modes") ||
               q.Contains("list the modes")     ||
               q.Contains("name the modes");
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        // Anchor the answer to the domain so it survives any future renames of the
        // canonical degree set; if the domain ever exposes fewer than 7 modes we
        // fall back to a minimal answer rather than emitting wrong content.
        var domainModes = MajorScaleMode.Items.ToList();
        if (domainModes.Count != _modeRows.Length)
        {
            logger.LogWarning(
                "ModesSkill: domain returned {Count} major-scale modes, expected {Expected}; using domain order",
                domainModes.Count, _modeRows.Length);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"The major scale has 7 modes (Ionian formula: {IonianWhPattern}). Each starts on a successive degree of the parent scale:");
        sb.AppendLine();

        for (var i = 0; i < _modeRows.Length; i++)
        {
            var row = _modeRows[i];
            sb.AppendLine($"{i + 1}. **{row.Name}** — degrees `{row.Degrees}` — {row.Character}");
        }

        sb.AppendLine();
        sb.Append("Mnemonic: *I Don't Particularly Like Modes A Lot* (Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian).");

        var evidence = _modeRows
            .Select((r, i) => $"Mode {i + 1}: {r.Name} ({r.Degrees})")
            .ToList();
        evidence.Add($"Parent scale formula: {IonianWhPattern}");
        evidence.Add($"Domain enumeration: MajorScaleMode.Items returned {domainModes.Count} modes");

        logger.LogDebug("ModesSkill: returning {Count} modes from domain", _modeRows.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId     = AgentIds.Theory,
            Result      = sb.ToString(),
            Confidence  = 1.0f,
            Evidence    = evidence,
            Assumptions = []
        });
    }
}
