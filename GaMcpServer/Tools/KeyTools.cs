namespace GaMcpServer.Tools;

using GA.Business.Core.Intervals.Primitives;
using GA.Business.Core.Tonal;
using JetBrains.Annotations;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class KeyTool
{
    [McpServerTool]
    [Description("Get all available keys")]
    public static IEnumerable<string> GetAllKeys()
    {
        return Key.Items.Select(k => k.ToString());
    }

    [McpServerTool]
    [Description("Get all major keys")]
    public static IEnumerable<string> GetMajorKeys()
    {
        return Key.GetItems(KeyMode.Major).Select(k => k.ToString());
    }

    [McpServerTool]
    [Description("Get all minor keys")]
    public static IEnumerable<string> GetMinorKeys()
    {
        return Key.GetItems(KeyMode.Minor).Select(k => k.ToString());
    }

    [McpServerTool]
    [Description("Get key signature information")]
    public static KeyInfo GetKeySignatureInfo(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        return new KeyInfo(
            key.ToString(),
            key.KeyMode.ToString(),
            key.Root.ToString(),
            key.AccidentalKind.ToString(),
            key.KeySignature.ToString(),
            key.KeySignature.AccidentedNotes.ToString(),
            key.Notes.Select(n => n.ToString())
        );
    }

    [McpServerTool]
    [Description("Get all notes in a key")]
    public static IEnumerable<string> GetKeyNotes(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        return key.Notes.Select(n => n.ToString());
    }

    [McpServerTool]
    [Description("Get all accidentals in a key signature")]
    public static string GetKeyAccidentals(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        return key.KeySignature.AccidentedNotes.ToString();
    }

    [McpServerTool]
    [Description("Get all key signatures")]
    public static IEnumerable<KeySignatureInfo> GetKeySignatures()
    {
        var keysBySignature = Key.Items.ToLookup(key => key.KeySignature);

        return KeySignature.Items.Select(keySignature => new KeySignatureInfo(
            $"{keySignature.AccidentalCount} {keySignature.AccidentalKind}(s)",
            keySignature.AccidentedNotes.ToString(),
            keysBySignature[keySignature].Select(k => k.ToString())
        ));
    }

    [McpServerTool]
    [Description("Get keys by accidental count")]
    public static IEnumerable<string> GetKeysByAccidentalCount(
        [Description("Number of accidentals (0-7)")]
        int count)
    {
        return Key.Items
            .Where(k => k.KeySignature.AccidentalCount == count)
            .Select(k => k.ToString());
    }

    [McpServerTool]
    [Description("Get keys by accidental type")]
    public static IEnumerable<string> GetKeysByAccidentalKind(
        [Description("Type of accidental (Sharp/Flat)")]
        string accidentalKind)
    {
        if (!Enum.TryParse<AccidentalKind>(accidentalKind, true, out var kind))
        {
            throw new InvalidOperationException($"Invalid accidental kind: {accidentalKind}. Use 'Sharp' or 'Flat'.");
        }

        return Key.Items
            .Where(k => k.AccidentalKind == kind)
            .Select(k => k.ToString());
    }

    [McpServerTool]
    [Description("Get pitch classes for a key")]
    public static string GetKeyPitchClasses(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        return key.PitchClassSet.ToString();
    }

    [McpServerTool]
    [Description("Check if a note is in a key")]
    public static bool IsNoteInKey(
        [Description("Name of the key")] string keyName,
        [Description("Note to check")] string noteName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        return key.Notes.Any(n => n.ToString() == noteName);
    }

    [McpServerTool]
    [Description("Get relative key")]
    public static string GetRelativeKey(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        // Get the relative key - parallel minor for major keys, parallel major for minor keys
        Key relativeKey = key.KeyMode switch
        {
            KeyMode.Major => new Key.Minor(key.KeySignature),
            KeyMode.Minor => new Key.Major(key.KeySignature),
            _ => throw new InvalidOperationException($"Unsupported key mode: {key.KeyMode}")
        };

        return relativeKey.ToString();
    }

    [McpServerTool]
    [Description("Get parallel key")]
    public static string GetParallelKey(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        // Create the parallel key - major for minor keys, minor for major keys
        Key parallelKey = key.KeyMode switch
        {
            KeyMode.Major => new Key.Minor(key.KeySignature),
            KeyMode.Minor => new Key.Major(key.KeySignature),
            _ => throw new InvalidOperationException($"Unsupported key mode: {key.KeyMode}")
        };

        return parallelKey.ToString();
    }

    [McpServerTool]
    [Description("Get scale degrees for a key")]
    public static IEnumerable<string> GetScaleDegrees(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        return key.Notes.Select(d => d.ToString());
    }

    [McpServerTool]
    [Description("Get key circle of fifths position")]
    public static int GetCircleOfFifthsPosition(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        var position = key.KeySignature.AccidentalKind switch
        {
            AccidentalKind.Sharp => key.KeySignature.AccidentalCount,
            AccidentalKind.Flat => -key.KeySignature.AccidentalCount,
            _ => 0
        };
        return position;
    }

    [McpServerTool]
    [Description("Get neighboring keys in circle of fifths")]
    public static NeighboringKeys GetNeighboringKeys(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName)
                  ?? throw new InvalidOperationException($"Key not found: {keyName}");

        var position = GetCircleOfFifthsPosition(key.KeySignature.ToString());
        var prevKey = Key.Items.FirstOrDefault(k =>
            GetCircleOfFifthsPosition(k.KeySignature.ToString()) == position - 1);
        var nextKey = Key.Items.FirstOrDefault(k =>
            GetCircleOfFifthsPosition(k.KeySignature.ToString()) == position + 1);

        return new NeighboringKeys(
            prevKey?.ToString() ?? "None",
            key.ToString(),
            nextKey?.ToString() ?? "None"
        );
    }

    [McpServerTool]
    [Description("Compare two keys")]
    public static string CompareKeys(
        [Description("First key name")] string keyName1,
        [Description("Second key name")] string keyName2)
    {
        var key1 = Key.Items.FirstOrDefault(k => k.ToString() == keyName1)
                   ?? throw new InvalidOperationException($"Key not found: {keyName1}");

        var key2 = Key.Items.FirstOrDefault(k => k.ToString() == keyName2)
                   ?? throw new InvalidOperationException($"Key not found: {keyName2}");

        var commonNotes = key1.Notes.Intersect(key2.Notes);

        return $"""
                Key 1: {key1}
                Key 2: {key2}
                Common Notes: {string.Join(", ", commonNotes)}
                Circle of Fifths Distance: {Math.Abs(GetCircleOfFifthsPosition(key1.ToString()) - GetCircleOfFifthsPosition(key2.ToString()))}
                Same Mode: {key1.KeyMode == key2.KeyMode}
                Same Root: {key1.Root == key2.Root}
                """;
    }

    [McpServerTool]
    [Description("Get key by root note and mode")]
    public static string GetKeyByRootAndMode(
        [Description("Root note name")] string rootNote,
        [Description("Mode (Major/Minor)")] string mode)
    {
        if (!Enum.TryParse<KeyMode>(mode, true, out var keyMode))
        {
            throw new InvalidOperationException($"Invalid mode: {mode}. Use 'Major' or 'Minor'.");
        }

        var key = Key.Items.FirstOrDefault(k =>
            k.Root.ToString() == rootNote &&
            k.KeyMode == keyMode);

        return key?.ToString()
               ?? throw new InvalidOperationException($"No key found with root {rootNote} and mode {mode}");
    }

    [PublicAPI]
    public record KeyInfo(
        string Name,
        string Mode,
        string Root,
        string AccidentalKind,
        string KeySignature,
        string Accidentals,
        IEnumerable<string> Notes
    );

    [PublicAPI]
    public record KeySignatureInfo(
        string Description,
        string AccidentedNotes,
        IEnumerable<string> RelatedKeys
    );

    [PublicAPI]
    public record NeighboringKeys(
        string Previous,
        string Current,
        string Next
    );
}
