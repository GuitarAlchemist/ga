namespace GA.Business.ML.Agents;

using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
///     Shared key/scale resolution and naming seam crossed by both
///     <see cref="Mcp.ScaleMcpTools"/> (MCP transport adapter) and
///     <see cref="Skills.ScaleInfoSkill"/> (orchestrator adapter).
/// </summary>
/// <remarks>
///     Both adapters previously carried byte-identical relative-key arithmetic and key-signature
///     description, plus the same <see cref="Key.Items"/> lookup. Consolidating here (candidate #3 of
///     the architecture review) keeps the two in lock-step — the relative-key mask arithmetic, the
///     key-signature wording, and the mode normalization now have one home.
/// </remarks>
public static class KeyNaming
{
    /// <summary>
    ///     Normalises a mode token to a major/minor flag. Accepts <c>major</c>/<c>maj</c> and
    ///     <c>minor</c>/<c>min</c> (case-insensitive); returns false for anything else so callers can
    ///     distinguish an unknown mode from an unknown key.
    /// </summary>
    public static bool TryNormalizeMode(string mode, out bool isMinor)
    {
        var n = mode.Trim().ToLowerInvariant();
        if (n is "minor" or "min") { isMinor = true;  return true; }
        if (n is "major" or "maj") { isMinor = false; return true; }
        isMinor = false;
        return false;
    }

    /// <summary>
    ///     Resolves a root + major/minor flag to the canonical domain <see cref="Key"/>, or null if no
    ///     standard key matches. Root comparison is case-insensitive and trimmed.
    /// </summary>
    public static Key? ResolveKey(string root, bool isMinor) =>
        Key.Items.FirstOrDefault(k =>
            k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major) &&
            string.Equals(k.Root.ToString(), root.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Human-readable key signature, e.g. <c>"2 sharps"</c> or <c>"no sharps or flats"</c>.</summary>
    public static string DescribeKeySignature(Key key)
    {
        var count = key.KeySignature.AccidentalCount;
        if (count == 0) return "no sharps or flats";
        var kind = key.KeySignature.AccidentalKind == AccidentalKind.Sharp ? "sharp" : "flat";
        return $"{count} {kind}{(count > 1 ? "s" : "")}";
    }

    /// <summary>
    ///     The relative key's name (e.g. <c>"A minor"</c> for C major): the key of the opposite mode
    ///     with the same key signature, or <c>"none"</c> if no standard key has it.
    /// </summary>
    /// <remarks>
    ///     Matching the pitch-class set instead is ambiguous for the enharmonic twins (B/Cb, F#/Gb
    ///     and C#/Db major, G#/Ab, D#/Eb and A#/Bb minor), and <see cref="Key.Items"/> lists the flat
    ///     twin first: B major came out as "Ab minor", not G# minor, contradicting
    ///     <see cref="Skills.RelativeKeySkill"/> (issue #769).
    /// </remarks>
    public static string RelativeKeyName(Key key)
    {
        var otherMode = key.KeyMode == KeyMode.Major ? KeyMode.Minor : KeyMode.Major;
        var sibling = Key.GetItems(otherMode)
            .FirstOrDefault(k => k.KeySignature.Value == key.KeySignature.Value);

        return sibling is null
            ? "none"
            : $"{sibling.Root} {(sibling.KeyMode == KeyMode.Major ? "major" : "minor")}";
    }
}
