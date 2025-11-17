namespace GA.Business.Core.Atonal;

/// <summary>
///     Minimal canonical Forte catalog for mapping prime-form pitch-class sets to Forte numbers.
///     This is intentionally small and can be extended incrementally. When a set is not found
///     in this catalog, callers may fall back to a stable, internal label.
/// </summary>
public static class ForteCatalog
{
    // Store keys as normalized string of ascending pitch-class values, e.g., "[0,1,5]"
    private static readonly IReadOnlyDictionary<string, ForteNumber> _map = new Dictionary<string, ForteNumber>
    {
        // Trichords
        ["[0,1,5]"] = ForteNumber.Parse("3-11"), // Major/minor triad set class representative

        // Tetrachords
        ["[0,1,4,6]"] = ForteNumber.Parse("4-19"),
        ["[0,2,5,7]"] = ForteNumber.Parse("4-23"),

        // Heptachords (diatonic)
        ["[0,2,4,5,7,9,11]"] = ForteNumber.Parse("7-35"), // Diatonic (Ionian/Aeolian family prime form)

        // Hexachords
        ["[0,2,4,6,8,10]"] = ForteNumber.Parse("6-35") // Whole-tone
    };

    // Optional fallback map by Interval Class Vector string, for cases where the prime-form representative differs
    // from the specific PCS literal we listed above.
    private static readonly IReadOnlyDictionary<string, ForteNumber> _icvMap = new Dictionary<string, ForteNumber>
    {
        // Diatonic heptachord family (Ionian/Aeolian, etc.)
        ["<2 5 4 3 6 1>"] = ForteNumber.Parse("7-35"),
        // Whole-tone hexachord
        ["<0 6 0 0 6 0>"] = ForteNumber.Parse("6-35")
    };

    public static bool TryGetForteNumber(PitchClassSet primeForm, out ForteNumber forte)
    {
        var key = ToKey(primeForm);
        if (_map.TryGetValue(key, out forte))
        {
            return true;
        }

        // Fallback: try by Interval Class Vector signature
        var icv = primeForm.IntervalClassVector.ToString();
        if (_icvMap.TryGetValue(icv, out forte))
        {
            return true;
        }
        forte = default;
        return false;
    }

    private static string ToKey(PitchClassSet set)
    {
        var values = set.Select(pc => pc.Value).OrderBy(v => v);
        return "[" + string.Join(",", values) + "]";
    }
}
