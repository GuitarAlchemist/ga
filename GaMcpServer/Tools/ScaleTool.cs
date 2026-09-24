namespace GaMcpServer.Tools;

using GA.Business.Config;
using GA.Domain.Core.Theory.Atonal;
using ModelContextProtocol;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class ScaleTool
{
    [McpServerTool]
    [Description(
        "Get all available scales with their binary scale IDs. " +
        "A binary scale ID is a 12-bit integer where bit n is set when pitch class n is present " +
        "(C=0, C#=1, D=2 … B=11). Example: major scale → 2741.")]
    public static IEnumerable<string> GetAvailableScales()
    {
        return ScalesConfig.GetAllScales()
            .OrderBy(s => s.Name)
            .Select(s => $"{s.Name} (id:{s.BinaryScaleId})");
    }

    [McpServerTool]
    [Description(
        "Look up a scale by its binary scale ID (12-bit pitch-class bitmask). " +
        "Returns the scale name, notes, category, alternate names, and Forte number if available. " +
        "Common IDs: Major=2741 (its relative Natural Minor has the same ID), Whole Tone=1365, " +
        "Half-Whole Diminished=1755, Whole-Half Diminished=2925.")]
    public static string GaScaleById(
        [Description("Binary scale ID, e.g. 2741 for the major scale")] int id)
    {
        var scale = ScalesConfig.TryGetScaleByBinaryId(id);
        if (scale == null)
            return $"No scale found for binary scale ID {id}.";

        return FormatScale(scale.Value);
    }

    /// <summary>
    /// Scale card shared by <see cref="GaScaleById"/> and <see cref="GaScaleByName"/>.
    /// The optional fields are F# options: a C# <c>??</c> on them converts the fallback into
    /// <c>Some(fallback)</c> and prints "Some(n/a)", so unwrap them explicitly.
    /// </summary>
    private static string FormatScale(ScalesConfig.ScaleInfo s)
    {
        var alts = s.AlternateNames.Count > 0 ? string.Join(", ", s.AlternateNames) : "none";
        var forte = s.ForteNumber?.Value is { Length: > 0 } configured ? configured : ForteNumberOf(s.BinaryScaleId);
        var category = s.Category?.Value ?? "unknown";
        var usage = s.Usage?.Value ?? "";
        return $"""
                Name: {s.Name}
                Binary Scale ID: {s.BinaryScaleId}
                Notes: {s.Notes}
                Category: {category}
                Alternate Names: {alts}
                Forte Number: {forte}
                Common: {s.Common}
                Usage: {usage}
                """;
    }

    /// <summary>Forte number of the pitch-class set encoded by a binary scale ID, or "n/a".</summary>
    private static string ForteNumberOf(int binaryScaleId)
    {
        var set = new PitchClassSet(Enumerable.Range(0, 12)
            .Where(pc => (binaryScaleId & 1 << pc) != 0)
            .Select(PitchClass.FromValue));
        return set.PrimeForm is { } prime && ForteCatalog.GetForteNumber(prime) is { } forte
            ? forte.ToString()
            : "n/a";
    }

    // Semitones above the root of each degree, per mode (the seven modes of the major scale).
    private static readonly Dictionary<string, int[]> ModeOffsets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["major"] = [0, 2, 4, 5, 7, 9, 11],
        ["ionian"] = [0, 2, 4, 5, 7, 9, 11],
        ["dorian"] = [0, 2, 3, 5, 7, 9, 10],
        ["phrygian"] = [0, 1, 3, 5, 7, 8, 10],
        ["lydian"] = [0, 2, 4, 6, 7, 9, 11],
        ["mixolydian"] = [0, 2, 4, 5, 7, 9, 10],
        ["minor"] = [0, 2, 3, 5, 7, 8, 10],
        ["natural minor"] = [0, 2, 3, 5, 7, 8, 10],
        ["aeolian"] = [0, 2, 3, 5, 7, 8, 10],
        ["locrian"] = [0, 1, 3, 5, 6, 8, 10],
    };

    private const string Letters = "CDEFGAB";
    private static readonly int[] LetterPitchClasses = [0, 2, 4, 5, 7, 9, 11];

    [McpServerTool]
    [Description(
        "Get the 7 scale notes for a key string such as 'G major', 'Bb major' or 'A minor'. " +
        "Returns a JSON array of {degree, note, pitchClass} objects suitable for fretboard overlays or theory analysis. " +
        "Notes are spelled with one letter per degree (F major has Bb, not A#); pitchClass is 0-11 (C=0, C#=1 … B=11). " +
        "Supports major, natural minor and the modes ionian, dorian, phrygian, lydian, mixolydian, aeolian, locrian.")]
    public static string GetScaleNotes(
        [Description("Key string in 'Root mode' format, e.g. 'G major', 'A minor', 'Bb major', 'D dorian'")] string key)
    {
        var parts = (key ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new McpException($"Invalid key format '{key}'. Expected 'Root mode', e.g. 'G major'.");

        var root = parts[0];
        var mode = string.Join(' ', parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var letter = root.Length > 0 ? Letters.IndexOf(char.ToUpperInvariant(root[0])) : -1;
        var accidental = root.Length > 1 ? root[1..] : "";
        var shift = accidental switch { "" => 0, "#" => 1, "##" or "x" => 2, "b" => -1, "bb" => -2, _ => (int?)null };
        if (letter < 0 || shift is null)
            throw new McpException($"Unknown root note '{root}'. Use a letter A-G with an optional #, ## (or x), b or bb, e.g. C, F#, Bb.");

        if (!ModeOffsets.TryGetValue(mode, out var offsets))
            throw new McpException(
                $"Unsupported mode '{mode}'. Use major, minor, natural minor, ionian, dorian, phrygian, lydian, mixolydian, aeolian or locrian.");

        var rootPc = (LetterPitchClasses[letter] + shift.Value + 12) % 12;
        var notes = offsets.Select((offset, degree) =>
        {
            // One letter per degree; the accidental is whatever closes the gap to the pitch class.
            var degreeLetter = (letter + degree) % 7;
            var pc = (rootPc + offset) % 12;
            var alteration = (pc - LetterPitchClasses[degreeLetter] + 18) % 12 - 6;
            var name = Letters[degreeLetter] + new string(alteration > 0 ? '#' : 'b', Math.Abs(alteration));
            return $"{{\"degree\":{degree + 1},\"note\":\"{name}\",\"pitchClass\":{pc}}}";
        });
        return $"[{string.Join(",", notes)}]";
    }

    [McpServerTool]
    [Description(
        "Look up a scale by name or alternate name (case-insensitive). " +
        "Returns the scale's binary scale ID, notes, category, and other metadata. " +
        "Modes (Dorian, Lydian, Phrygian dominant…) are looked up in the modes catalog, spelled from C. " +
        "Example: 'Ionian' resolves to the Major scale (id:2741).")]
    public static string GaScaleByName(
        [Description("Scale or mode name, or alternate name, e.g. 'Major', 'Ionian', 'Blues', 'Dorian'")] string name)
    {
        var scale = ScalesConfig.TryGetScaleByName(name);
        if (scale != null)
            return FormatScale(scale.Value);

        // Scales.yaml lists one entry per pitch-class set (Major, not its modes);
        // the modes live in Modes.yaml.
        var mode = ModesConfig.TryGetModeByName(name);
        if (mode != null)
            return FormatMode(mode.Value);

        return $"No scale found for name '{name}'.";
    }

    private static string FormatMode(ModesConfig.ModeInfo m)
    {
        var id = ScalesConfig.computeBinaryScaleId(m.Notes);
        var alts = m.AlternateNames?.Value is { Count: > 0 } names ? string.Join(", ", names) : "none";
        var family = m.FamilyName?.Value is { } familyName ? $"Mode of the {familyName}" : "Mode";
        return $"""
                Name: {m.Name}
                Binary Scale ID: {id}
                Notes: {m.Notes}
                Category: {family}
                Alternate Names: {alts}
                Forte Number: {ForteNumberOf(id)}
                Description: {m.Description?.Value ?? ""}
                """;
    }

    [McpServerTool]
    [Description(
        "Get detailed mathematical and structural properties for a scale by binary scale ID. " +
        "Includes Myhill's Property, Rothenberg Propriety, Maximal Evenness Discrepancy, " +
        "Well-Formedness, Imperfection Count, Zeitler Legitimacy, and Interval Contradictions/Ambiguities.")]
    public static string GaScaleProperties(
        [Description("Binary scale ID, e.g. 1709 for Dorian or 2741 for Major")] int id)
    {
        var pcsId = GA.Domain.Core.Theory.Atonal.PitchClassSetId.FromValue(id);
        var pcs = pcsId.ToPitchClassSet();
        var (contradictions, ambiguities) = GA.Domain.Core.Theory.Atonal.AdvancedScaleGeometry.GetIntervalMatrixDiagnostics(pcs);
        var isWellFormed = GA.Domain.Core.Theory.Atonal.ScaleStructuralProperties.IsWellFormed(pcs, out var generator);

        return $"""
                Binary Scale ID: {id}
                Pitch Classes: {pcs}
                Cardinality: {pcs.Cardinality.Value}
                Myhill's Property: {GA.Domain.Core.Theory.Atonal.ScaleStructuralProperties.HasMyhillProperty(pcs)}
                Rothenberg Propriety: {GA.Domain.Core.Theory.Atonal.ScaleStructuralProperties.GetRothenbergPropriety(pcs)}
                Maximal Evenness Discrepancy: {GA.Domain.Core.Theory.Atonal.ScaleStructuralProperties.GetMaximalEvennessDiscrepancy(pcs):F4}
                Well-Formed: {isWellFormed} (Generator: {(isWellFormed ? generator : "none")})
                Imperfection Count: {GA.Domain.Core.Theory.Atonal.ScaleStructuralProperties.GetImperfectionCount(pcs)}
                Zeitler Legitimacy: {GA.Domain.Core.Theory.Atonal.ScaleStructuralProperties.GetZeitlerLegitimacy(pcs)}
                Interval Contradictions: {contradictions}
                Interval Ambiguities: {ambiguities}
                """;
    }

    [McpServerTool]
    [Description(
        "Get all common tertian triads (Major, Minor, Diminished, Augmented) in a scale and their " +
        "parsimonious voice-leading connections (2 common tones, 1 voice moving by 1-2 semitones).")]
    public static string GaParsimoniousTriads(
        [Description("Binary scale ID, e.g. 1709 for Dorian")] int id)
    {
        var pcsId = GA.Domain.Core.Theory.Atonal.PitchClassSetId.FromValue(id);
        var pcs = pcsId.ToPitchClassSet();
        var triads = GA.Domain.Core.Theory.Atonal.AdvancedScaleGeometry.GetTertianTriads(pcs);
        var connections = GA.Domain.Core.Theory.Atonal.AdvancedScaleGeometry.GetParsimoniousTriadConnections(pcs);

        var triadList = string.Join("\n", triads.Select(t => $"  - {t.Root} {t.TriadQuality} ({t.Root}, {t.Third}, {t.Fifth})"));
        var connList = string.Join("\n", connections.Select(c =>
            $"  - {c.FromTriad.Root} {c.FromTriad.TriadQuality} <-> {c.ToTriad.Root} {c.ToTriad.TriadQuality} [Voice {c.MovingVoiceFrom} -> {c.MovingVoiceTo} ({c.SemitoneShift} semitone)]"));

        return $"""
                Triads ({triads.Count}):
                {triadList}

                Parsimonious Voice-Leading Connections ({connections.Count}):
                {connList}
                """;
    }
}

