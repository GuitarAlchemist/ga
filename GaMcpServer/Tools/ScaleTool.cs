namespace GaMcpServer.Tools;

using GA.Business.Config;
using GA.Domain.Core.Theory.Atonal;
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

    [McpServerTool]
    [Description(
        "Get the 7 scale notes for a key string such as 'G major' or 'A minor'. " +
        "Returns a JSON array of {degree, note, pitchClass} objects suitable for fretboard overlays or theory analysis. " +
        "pitchClass is 0-11 (C=0, C#=1 … B=11). Supports major and natural minor only.")]
    public static string GetScaleNotes(
        [Description("Key string in 'Root mode' format, e.g. 'G major', 'A minor', 'Bb major'")] string key)
    {
        var parts = key.Trim().Split(' ', 2);
        if (parts.Length < 2)
            return $"Invalid key format '{key}'. Expected 'Root mode', e.g. 'G major'.";

        var root = parts[0];
        var mode = parts[1].ToLowerInvariant();

        string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        int[] major = [0, 2, 4, 5, 7, 9, 11];
        int[] minor = [0, 2, 3, 5, 7, 8, 10];

        var rootIndex = Array.IndexOf(noteNames, root);
        if (rootIndex < 0)
            return $"Unknown root note '{root}'. Use sharps (e.g. C#, F#) not flats for black keys.";

        var offsets = mode.StartsWith("minor") ? minor : major;
        var notes = offsets.Select((offset, i) =>
        {
            var pc = (rootIndex + offset) % 12;
            return $"{{\"degree\":{i + 1},\"note\":\"{noteNames[pc]}\",\"pitchClass\":{pc}}}";
        });
        return $"[{string.Join(",", notes)}]";
    }

    [McpServerTool]
    [Description(
        "Look up a scale by name or alternate name (case-insensitive). " +
        "Returns the scale's binary scale ID, notes, category, and other metadata. " +
        "Example: 'Ionian' resolves to the Major scale (id:2741).")]
    public static string GaScaleByName(
        [Description("Scale name or alternate name, e.g. 'Major', 'Ionian', 'Blues'")] string name)
    {
        var scale = ScalesConfig.TryGetScaleByName(name);
        if (scale == null)
            return $"No scale found for name '{name}'.";

        return FormatScale(scale.Value);
    }
}
