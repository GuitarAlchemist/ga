namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Shared token-matching helper for the chord-intent skills (ChordVoicingsSkill,
/// ImprovisationSkill). Centralises whole-word keyword matching so the two
/// skills can't drift apart, and so an intent keyword only fires when the user
/// actually typed it as a word — not when it happens to be a substring of an
/// unrelated word ("shell" inside "PowerShell", "shape" inside "landscape").
/// See ga#261.
/// </summary>
internal static class ChordIntentMatching
{
    /// <summary>
    /// True when <paramref name="keyword"/> occurs in <paramref name="haystack"/>
    /// as a whole word — i.e. not embedded inside a larger alphabetic word.
    /// "shell" matches "shell voicing" but NOT "PowerShell"; "solo" matches
    /// "solo over G7" but NOT "Solomon". Both arguments are expected lowercase.
    /// Word edges are ASCII letters only, so surrounding digits, punctuation,
    /// hyphens, and spaces (e.g. "drop2 ", "open-shape", "chord-scale") still
    /// count as boundaries.
    /// </summary>
    public static bool ContainsWord(string haystack, string keyword)
    {
        var start = 0;
        while (true)
        {
            var i = haystack.IndexOf(keyword, start, StringComparison.Ordinal);
            if (i < 0) return false;
            var beforeOk = i == 0 || !char.IsAsciiLetter(haystack[i - 1]);
            var afterIdx = i + keyword.Length;
            var afterOk = afterIdx >= haystack.Length || !char.IsAsciiLetter(haystack[afterIdx]);
            if (beforeOk && afterOk) return true;
            start = i + 1;
        }
    }
}
