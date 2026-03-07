namespace GaMcpServer.Tools;

using GA.Business.Config;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class ScaleTool
{
    [McpServerTool]
    [Description(
        "Get all available scales with their Ian Ring IDs. " +
        "Ian Ring IDs are 12-bit integers where each bit n represents pitch class n (C=0…B=11). " +
        "Example: major scale → 2741 (bits 0,2,4,5,7,9,11 set).")]
    public static IEnumerable<string> GetAvailableScales()
    {
        return ScalesConfig.GetAllScales()
            .OrderBy(s => s.Name)
            .Select(s => $"{s.Name} (id:{s.IanRingId})");
    }

    [McpServerTool]
    [Description(
        "Look up a scale by its Ian Ring ID (12-bit pitch-class set integer). " +
        "Returns the scale name, notes, category, alternate names, and Forte number if available. " +
        "Common IDs: Major=2741, Natural Minor=1453, Whole Tone=1365, Diminished=1755.")]
    public static string GaScaleById(
        [Description("Ian Ring scale ID, e.g. 2741 for the major scale")] int id)
    {
        var scale = ScalesConfig.TryGetScaleByIanRingId(id);
        if (scale == null)
            return $"No scale found for Ian Ring ID {id}.";

        var s = scale.Value;
        var alts = s.AlternateNames.Count > 0 ? string.Join(", ", s.AlternateNames) : "none";
        var forte = s.ForteNumber ?? "n/a";
        var category = s.Category ?? "unknown";
        var usage = s.Usage ?? "";
        return $"""
                Name: {s.Name}
                Ian Ring ID: {s.IanRingId}
                Notes: {s.Notes}
                Category: {category}
                Alternate Names: {alts}
                Forte Number: {forte}
                Common: {s.Common}
                Usage: {usage}
                """;
    }

    [McpServerTool]
    [Description(
        "Look up a scale by name or alternate name (case-insensitive). " +
        "Returns the scale's Ian Ring ID, notes, category, and other metadata. " +
        "Example: 'Ionian' resolves to the Major scale (id:2741).")]
    public static string GaScaleByName(
        [Description("Scale name or alternate name, e.g. 'Major', 'Ionian', 'Blues'")] string name)
    {
        var scale = ScalesConfig.TryGetScaleByName(name);
        if (scale == null)
            return $"No scale found for name '{name}'.";

        var s = scale.Value;
        var alts = s.AlternateNames.Count > 0 ? string.Join(", ", s.AlternateNames) : "none";
        var forte = s.ForteNumber ?? "n/a";
        var category = s.Category ?? "unknown";
        var usage = s.Usage ?? "";
        return $"""
                Name: {s.Name}
                Ian Ring ID: {s.IanRingId}
                Notes: {s.Notes}
                Category: {category}
                Alternate Names: {alts}
                Forte Number: {forte}
                Common: {s.Common}
                Usage: {usage}
                """;
    }
}
