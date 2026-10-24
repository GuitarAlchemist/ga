namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Key-signature answers shared by <see cref="RelativeKeySkill"/> and <see cref="CircleOfFifthsSkill"/>:
/// "how many sharps are in the key of A major?" (key → signature) and "which major key has 4 flats?"
/// (signature → key). Counts and accidental names come from the domain <see cref="Key"/>, so the
/// question gets the same direct answer whichever of the two skills the router picks.
/// </summary>
/// <remarks>
/// Built 2026-10-03: "How many sharps are in the key of A major?" routed to the circle-of-fifths
/// catalog skill, which returned its whole page, and RelativeKeySkill's pattern did not accept the
/// words between "sharps" and the key ("are in the key of").
/// </remarks>
internal static class KeySignatureAnswers
{
    // "how many sharps are in the key of A major", "how many flats does E flat major have",
    // "what's the key signature of F# minor", "key signature for Bbm". Filler words may sit between
    // the question words and the key; the key letter must end the word so "each" is not E.
    private static readonly Regex KeyToSignaturePattern =
        new(@"\b(?:how\s+many\s+(?:sharps|flats|accidentals)|key\s+signature)\b(?:\s+(?:are|does|do|is|has|have|there|in|on|of|for|the|key))*\s+(?<key>[A-Ga-g])(?:(?<acc>b|#|♭|♯)|[\s-]+(?<accword>flat|sharp)(?![a-z]))?(?<m>m)?(?![A-Za-z#♭♯])(?:\s*(?<quality>maj(?:or)?|min(?:or)?)\b)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "which major key has 4 flats", "what key has two sharps", "what minor key has no sharps".
    private static readonly Regex SignatureToKeyPattern =
        new(@"\b(?:(?:which|what)\s+)?(?:(?<quality>major|minor)\s+)?keys?\s+(?:has|have|with|uses?|contains?)\s+(?<n>\d+|no|zero|one|two|three|four|five|six|seven)\s+(?<acc>sharps?|flats?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["no"] = 0, ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3,
        ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7,
    };

    /// <summary>True when <see cref="TryAnswer"/> recognises the question.</summary>
    public static bool IsKeySignatureQuestion(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && (SignatureToKeyPattern.IsMatch(message) || MatchKeyToSignature(message) is not null);

    // "how many sharps in d?", "key signature for f#": right after in/of/for and ending the clause.
    private static readonly Regex BareKeyLeadIn = new(@"\b(?:in|of|for)\s+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClauseEnd = new(@"\G\s*(?:[?.!,;:]|$)", RegexOptions.Compiled);

    // A lowercase letter is a key only with a quality ("e minor", "bbm") or as the bare object of
    // in/of/for that ends the clause, so the article in "how many sharps does a key signature have"
    // or "how many sharps are in a key signature" is not read as A major.
    private static Match? MatchKeyToSignature(string message) =>
        KeyToSignaturePattern.Match(message) is { Success: true } match
        && (char.IsUpper(match.Groups["key"].Value[0]) || match.Groups["quality"].Success || match.Groups["m"].Success
            || (BareKeyLeadIn.IsMatch(message[..match.Groups["key"].Index]) && ClauseEnd.IsMatch(message, match.Index + match.Length)))
            ? match
            : null;

    /// <summary>
    /// Answers a key-signature question in either direction, or returns false when the message is
    /// not one.
    /// </summary>
    public static bool TryAnswer(string? message, out string answer, out string evidence)
    {
        answer = string.Empty;
        evidence = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (SignatureToKeyPattern.Match(message) is { Success: true } reverse)
        {
            (answer, evidence) = AnswerKeyForSignature(reverse);
            return true;
        }

        if (MatchKeyToSignature(message) is { } forward)
        {
            var accidental = forward.Groups["accword"].Success
                ? (forward.Groups["accword"].Value.StartsWith("f", StringComparison.OrdinalIgnoreCase) ? "b" : "#")
                : forward.Groups["acc"].Value;
            var root = NormalizeRoot(forward.Groups["key"].Value + accidental);
            var isMinor = forward.Groups["m"].Success
                          || forward.Groups["quality"].Value.StartsWith("min", StringComparison.OrdinalIgnoreCase);
            (answer, evidence) = AnswerSignatureForKey(root, isMinor);
            return true;
        }

        return false;
    }

    private static (string Answer, string Evidence) AnswerSignatureForKey(string root, bool isMinor)
    {
        var mode = isMinor ? "minor" : "major";
        if (KeyNaming.ResolveKey(root, isMinor) is { } key)
        {
            var text = $"**{key.Root} {mode}** has {KeyNaming.DescribeKeySignature(key)}.{NameAccidentals(key)} " +
                       $"Its relative {(isMinor ? "major" : "minor")}, {KeyNaming.RelativeKeyName(key)}, shares this key signature.";
            return (text, $"key-signature({key.Root} {mode}={Signed(key)})");
        }

        // A theoretical key such as G# major: the standard key on the same pitch is its enharmonic,
        // and the theoretical signature needs 12 minus that key's accidentals (with double sharps or flats).
        if (RootPitchClass(root) is { } pc
            && Key.Items.FirstOrDefault(k => k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major)
                                             && k.Root.PitchClass.Value == pc) is { } enharmonic)
        {
            var theoretical = 12 - enharmonic.KeySignature.AccidentalCount;
            var kind = root.EndsWith('#') ? "sharps" : "flats";
            var text = $"**{root} {mode}** is a theoretical key: its signature would need {theoretical} {kind}, " +
                       $"some of them double {(kind == "sharps" ? "sharps" : "flats")}. Music in that key is written in its " +
                       $"enharmonic equivalent, **{enharmonic.Root} {mode}**, which has {KeyNaming.DescribeKeySignature(enharmonic)}.{NameAccidentals(enharmonic)}";
            return (text, $"key-signature({root} {mode} theoretical → {enharmonic.Root} {mode})");
        }

        return ($"I couldn't identify '{root} {mode}' as a key. Try a pitch letter optionally followed by # or b (e.g. A major, F# minor, Bb major).",
                $"key-signature(unparseable '{root}')");
    }

    private static (string Answer, string Evidence) AnswerKeyForSignature(Match match)
    {
        var raw = match.Groups["n"].Value;
        var count = NumberWords.TryGetValue(raw, out var word) ? word : int.Parse(raw);
        var flats = match.Groups["acc"].Value.StartsWith("flat", StringComparison.OrdinalIgnoreCase);
        var quality = match.Groups["quality"].Value.ToLowerInvariant();
        var kindWord = flats ? "flats" : "sharps";

        if (count > 7)
            return ($"No standard key signature has {count} {kindWord}: the most is 7, " +
                    (flats ? "**Cb major** and its relative minor **Ab minor**." : "**C# major** and its relative minor **A# minor**."),
                    $"key-for-signature({count} {kindWord}: none)");

        Key? Find(KeyMode mode) => Key.Items.FirstOrDefault(k =>
            k.KeyMode == mode
            && k.KeySignature.AccidentalCount == count
            && (count == 0 || k.KeySignature.AccidentalKind == (flats ? AccidentalKind.Flat : AccidentalKind.Sharp)));

        var major = Find(KeyMode.Major)!;
        var minor = Find(KeyMode.Minor)!;
        var signature = count == 0 ? "No sharps or flats" : $"{KeyNaming.DescribeKeySignature(major)} ({string.Join(", ", major.KeySignature.AccidentedNotes)})";

        var text = quality switch
        {
            "major" => $"**{major.Root} major** has {Describe(major)}. Its relative minor, **{minor.Root} minor**, shares this key signature.",
            "minor" => $"**{minor.Root} minor** has {Describe(minor)}. Its relative major, **{major.Root} major**, shares this key signature.",
            _ => $"{signature} is the key signature of **{major.Root} major** and its relative minor, **{minor.Root} minor**.",
        };
        return (text, $"key-for-signature({count} {kindWord} → {major.Root} major / {minor.Root} minor)");

        static string Describe(Key key) =>
            key.KeySignature.AccidentalCount == 0
                ? KeyNaming.DescribeKeySignature(key)
                : $"{KeyNaming.DescribeKeySignature(key)}: {string.Join(", ", key.KeySignature.AccidentedNotes)}";
    }

    private static string NameAccidentals(Key key)
    {
        var notes = key.KeySignature.AccidentedNotes;
        return key.KeySignature.AccidentalCount switch
        {
            0 => string.Empty,
            1 => $" The {(key.KeySignature.AccidentalKind == AccidentalKind.Sharp ? "sharp" : "flat")} is {notes.First()}.",
            _ => $" The {(key.KeySignature.AccidentalKind == AccidentalKind.Sharp ? "sharps" : "flats")} are {string.Join(", ", notes)}.",
        };
    }

    private static string Signed(Key key) =>
        key.KeySignature.AccidentalCount == 0
            ? "0"
            : $"{(key.KeySignature.AccidentalKind == AccidentalKind.Sharp ? "+" : "-")}{key.KeySignature.AccidentalCount}";

    private static string NormalizeRoot(string raw)
    {
        var ascii = raw.Replace("♯", "#").Replace("♭", "b");
        return ascii.Length == 1
            ? ascii.ToUpperInvariant()
            : char.ToUpperInvariant(ascii[0]) + ascii[1..].ToLowerInvariant();
    }

    private static int? RootPitchClass(string root)
    {
        int? natural = char.ToUpperInvariant(root[0]) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => null,
        };
        if (natural is null) return null;
        var shift = root.Length > 1 ? (root[1] == '#' ? 1 : -1) : 0;
        return ((natural.Value + shift) % 12 + 12) % 12;
    }
}
