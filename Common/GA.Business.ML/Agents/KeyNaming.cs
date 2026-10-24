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
    ///     The relative key's name (e.g. <c>"A minor"</c> for C major): the opposite mode on the same
    ///     key signature. Matching pitch-class sets instead also accepted the enharmonic key, so F#
    ///     major (6 sharps) came out as Eb minor (6 flats) rather than D# minor.
    /// </summary>
    public static string RelativeKeyName(Key key)
    {
        Key relative = key.KeyMode == KeyMode.Major ? new Key.Minor(key.KeySignature) : new Key.Major(key.KeySignature);
        return $"{relative.Root} {(relative.KeyMode == KeyMode.Major ? "major" : "minor")}";
    }
}
