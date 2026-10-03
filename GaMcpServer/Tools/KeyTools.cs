namespace GaMcpServer.Tools;

using GA.Domain.Core.Primitives;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Theory.Tonal;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using ModelContextProtocol;
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
    public static KeyInfo GetKeySignatureInfo([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

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
    public static IEnumerable<string> GetKeyNotes([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

        return key.Notes.Select(n => n.ToString());
    }

    [McpServerTool]
    [Description("Get all accidentals in a key signature")]
    public static string GetKeyAccidentals([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

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
            throw new McpException($"Invalid accidental kind: {accidentalKind}. Use 'Sharp' or 'Flat'.");
        }

        return Key.Items
            .Where(k => k.AccidentalKind == kind)
            .Select(k => k.ToString());
    }

    [McpServerTool]
    [Description("Get pitch classes for a key")]
    public static string GetKeyPitchClasses([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

        return key.PitchClassSet.ToString();
    }

    [McpServerTool]
    [Description("Check if a note is in a key")]
    public static bool IsNoteInKey(
        [Description(KeyNameFormat)] string keyName,
        [Description("Note to check")] string noteName)
    {
        var key = FindKey(keyName);

        return key.Notes.Any(n => n.ToString() == noteName);
    }

    [McpServerTool]
    [Description("Get relative key")]
    public static string GetRelativeKey([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

        // Relative keys share a key signature and use opposite modes.
        Key relativeKey = key.KeyMode switch
        {
            KeyMode.Major => new Key.Minor(key.KeySignature),
            KeyMode.Minor => new Key.Major(key.KeySignature),
            _ => throw new McpException($"Unsupported key mode: {key.KeyMode}")
        };

        return relativeKey.ToString();
    }

    [McpServerTool]
    [Description("Get parallel key")]
    public static string GetParallelKey([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

        var targetMode = key.KeyMode == KeyMode.Major ? KeyMode.Minor : KeyMode.Major;
        var parallelKey = Key.Items.FirstOrDefault(candidate =>
            candidate.KeyMode == targetMode &&
            candidate.Root.NaturalNote == key.Root.NaturalNote &&
            candidate.Root.Accidental == key.Root.Accidental)
            ?? Key.Items.FirstOrDefault(candidate =>
                candidate.KeyMode == targetMode && candidate.Root.PitchClass == key.Root.PitchClass)
            ?? throw new McpException($"Parallel key not found for: {keyName}");

        return parallelKey.ToString();
    }

    [McpServerTool]
    [Description("Get scale degrees for a key")]
    public static IEnumerable<string> GetScaleDegrees([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

        return key.Notes.Select(d => d.ToString());
    }

    [McpServerTool]
    [Description("Get key circle of fifths position")]
    public static int GetCircleOfFifthsPosition([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

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
    public static NeighboringKeys GetNeighboringKeys([Description(KeyNameFormat)] string keyName)
    {
        var key = FindKey(keyName);

        var position = GetCircleOfFifthsPosition(keyName);
        var prevKey = Key.Items.FirstOrDefault(candidate =>
            candidate.KeyMode == key.KeyMode &&
            (candidate.KeySignature.AccidentalKind switch
            {
                AccidentalKind.Sharp => candidate.KeySignature.AccidentalCount,
                AccidentalKind.Flat => -candidate.KeySignature.AccidentalCount,
                _ => 0
            }) == position - 1);
        var nextKey = Key.Items.FirstOrDefault(candidate =>
            candidate.KeyMode == key.KeyMode &&
            (candidate.KeySignature.AccidentalKind switch
            {
                AccidentalKind.Sharp => candidate.KeySignature.AccidentalCount,
                AccidentalKind.Flat => -candidate.KeySignature.AccidentalCount,
                _ => 0
            }) == position + 1);

        return new NeighboringKeys(
            prevKey?.ToString() ?? "None",
            key.ToString(),
            nextKey?.ToString() ?? "None"
        );
    }

    [McpServerTool]
    [Description("Compare two keys")]
    public static string CompareKeys(
        [Description("First key. " + KeyNameFormat)] string keyName1,
        [Description("Second key. " + KeyNameFormat)] string keyName2)
    {
        var key1 = FindKey(keyName1);

        var key2 = FindKey(keyName2);

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
            throw new McpException($"Invalid mode: {mode}. Use 'Major' or 'Minor'.");
        }

        var key = Key.Items.FirstOrDefault(k =>
            k.Root.ToString() == rootNote &&
            k.KeyMode == keyMode);

        return key?.ToString()
               ?? throw new McpException($"No key found with root {rootNote} and mode {mode}");
    }

    /// <summary>The key name format every key tool expects: the strings <see cref="GetAllKeys"/> returns.</summary>
    private const string KeyNameFormat =
        "Key name in the format get_all_keys returns: 'Key of C' for C major, 'Key of Am' for A minor " +
        "(e.g. 'Key of Bb', 'Key of F#m')";

    private static readonly Regex KeyLikeName = new(
        @"^\s*(?:key\s+of\s+)?(?<letter>[A-Ga-g])(?<accidental>#|b)?\s*(?<mode>major|maj|minor|min|m)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Finds a key by its <see cref="Key.ToString"/> name. A miss throws <see cref="McpException"/>, whose
    /// message reaches the client (any other exception surfaces only as the SDK's generic "An error occurred
    /// invoking …"), and names the expected key when the input reads like "A major" or "F# minor".
    /// </summary>
    private static Key FindKey(string keyName)
    {
        var key = Key.Items.FirstOrDefault(k => k.ToString() == keyName);
        if (key is not null)
            return key;

        var hint = SuggestKeyName(keyName) is { } suggestion ? $" Did you mean '{suggestion}'?" : "";
        throw new McpException(
            $"Key not found: '{keyName}'. Use the get_all_keys format: 'Key of C' for C major, 'Key of Am' for A minor.{hint}");
    }

    private static string? SuggestKeyName(string? keyName)
    {
        var match = KeyLikeName.Match(keyName ?? "");
        if (!match.Success)
            return null;

        var root = char.ToUpperInvariant(match.Groups["letter"].Value[0]) + match.Groups["accidental"].Value.ToLowerInvariant();
        var isMinor = match.Groups["mode"].Value.ToLowerInvariant() is "m" or "min" or "minor";
        var candidate = $"Key of {root}{(isMinor ? "m" : "")}";
        return Key.Items.Any(k => k.ToString() == candidate) ? candidate : null;
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
