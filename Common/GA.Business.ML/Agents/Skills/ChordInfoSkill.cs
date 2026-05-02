namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;

/// <summary>
/// Answers basic chord-note questions from deterministic interval formulas.
/// </summary>
public sealed partial class ChordInfoSkill(ILogger<ChordInfoSkill> logger) : IOrchestratorSkill
{
    private static readonly IReadOnlyDictionary<string, int> PitchClasses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0,
        ["C#"] = 1,
        ["Db"] = 1,
        ["D"] = 2,
        ["D#"] = 3,
        ["Eb"] = 3,
        ["E"] = 4,
        ["F"] = 5,
        ["F#"] = 6,
        ["Gb"] = 6,
        ["G"] = 7,
        ["G#"] = 8,
        ["Ab"] = 8,
        ["A"] = 9,
        ["A#"] = 10,
        ["Bb"] = 10,
        ["B"] = 11,
        ["B#"] = 0,
        ["Cb"] = 11,
        ["E#"] = 5,
        ["Fb"] = 4,
    };

    private static readonly char[] NaturalLetters = ['C', 'D', 'E', 'F', 'G', 'A', 'B'];
    private static readonly IReadOnlyDictionary<char, int> NaturalPitchClasses = new Dictionary<char, int>
    {
        ['C'] = 0,
        ['D'] = 2,
        ['E'] = 4,
        ['F'] = 5,
        ['G'] = 7,
        ['A'] = 9,
        ['B'] = 11,
    };

    public string Name => "ChordInfo";

    public string Description => "Returns notes for common chord qualities from deterministic interval formulas";

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return ChordQuestionRegex().IsMatch(message) ||
               (HasChordIntent(message) && CompactChordRegex().IsMatch(message)) ||
               NoteSetQuestionRegex().IsMatch(message);
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var parsed = TryParse(message) ?? TryIdentifyNoteSet(message);
        if (parsed is null)
        {
            return Task.FromResult(AgentResponse.CannotHelp(AgentIds.Theory, "Could not parse a chord name from your question."));
        }

        var (root, quality) = parsed.Value;
        if (!PitchClasses.TryGetValue(root, out var rootPc))
        {
            return Task.FromResult(AgentResponse.CannotHelp(AgentIds.Theory, $"I do not recognise \"{root}\" as a chord root."));
        }

        var formula = GetFormula(quality);
        var noteNames = formula.Intervals
            .Zip(formula.LetterSteps, (interval, letterSteps) => Spell(root, rootPc + interval, letterSteps))
            .ToList();

        logger.LogDebug(
            "ChordInfoSkill: resolved {Root} {Quality} -> [{Notes}]",
            root,
            formula.DisplayName,
            string.Join(", ", noteNames));

        return Task.FromResult(new AgentResponse
        {
            AgentId = AgentIds.Theory,
            Result = $"{root} {formula.DisplayName} contains {JoinNotes(noteNames)}.",
            Confidence = 1.0f,
            Evidence =
            [
                $"Root: {root}",
                $"Quality: {formula.DisplayName}",
                $"Intervals: {string.Join(", ", formula.IntervalNames)}",
                $"Notes: {string.Join(", ", noteNames)}"
            ],
            Assumptions = []
        });
    }

    private static (string Root, string Quality)? TryParse(string message)
    {
        var chordQuestion = ChordQuestionRegex().Match(message);
        if (chordQuestion.Success)
        {
            return (NormalizeRoot(chordQuestion.Groups["root"].Value), NormalizeQuality(chordQuestion.Groups["quality"].Value));
        }

        var compact = CompactChordRegex().Match(message);
        if (compact.Success)
        {
            return (NormalizeRoot(compact.Groups["root"].Value), NormalizeQuality(compact.Groups["quality"].Value));
        }

        return null;
    }

    private static (string Root, string Quality)? TryIdentifyNoteSet(string message)
    {
        if (!NoteSetQuestionRegex().IsMatch(message))
        {
            return null;
        }

        var notes = NoteTokenRegex()
            .Matches(message)
            .Select(match => NormalizeRoot(match.Groups["note"].Value))
            .Where(PitchClasses.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (notes.Count is < 3 or > 4)
        {
            return null;
        }

        var pitchClasses = notes
            .Select(note => PitchClasses[note])
            .Order()
            .ToArray();

        foreach (var root in notes)
        {
            var rootPc = PitchClasses[root];
            foreach (var (quality, formula) in CandidateFormulas())
            {
                if (formula.Intervals.Count != pitchClasses.Length)
                {
                    continue;
                }

                var candidate = formula.Intervals
                    .Select(interval => ((rootPc + interval) % 12 + 12) % 12)
                    .Order()
                    .ToArray();

                if (candidate.SequenceEqual(pitchClasses))
                {
                    return (root, quality);
                }
            }
        }

        return null;
    }

    private static string NormalizeRoot(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string NormalizeQuality(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => "major",
            "maj" or "major" => "major",
            "m" or "min" or "minor" => "minor",
            "dim" or "diminished" => "diminished",
            "aug" or "augmented" => "augmented",
            "7" or "dominant" or "dominant 7" => "dominant 7",
            "maj7" or "major 7" => "major 7",
            "m7" or "min7" or "minor 7" => "minor 7",
            _ => normalized
        };
    }

    private static ChordFormula GetFormula(string quality) =>
        quality switch
        {
            "minor" => new ChordFormula("minor chord", [0, 3, 7], [0, 2, 4], ["root", "minor third", "perfect fifth"]),
            "diminished" => new ChordFormula("diminished chord", [0, 3, 6], [0, 2, 4], ["root", "minor third", "diminished fifth"]),
            "augmented" => new ChordFormula("augmented chord", [0, 4, 8], [0, 2, 4], ["root", "major third", "augmented fifth"]),
            "dominant 7" => new ChordFormula("dominant 7 chord", [0, 4, 7, 10], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "minor seventh"]),
            "major 7" => new ChordFormula("major 7 chord", [0, 4, 7, 11], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "major seventh"]),
            "minor 7" => new ChordFormula("minor 7 chord", [0, 3, 7, 10], [0, 2, 4, 6], ["root", "minor third", "perfect fifth", "minor seventh"]),
            _ => new ChordFormula("major chord", [0, 4, 7], [0, 2, 4], ["root", "major third", "perfect fifth"])
        };

    private static IEnumerable<(string Quality, ChordFormula Formula)> CandidateFormulas()
    {
        yield return ("major", GetFormula("major"));
        yield return ("minor", GetFormula("minor"));
        yield return ("diminished", GetFormula("diminished"));
        yield return ("augmented", GetFormula("augmented"));
        yield return ("dominant 7", GetFormula("dominant 7"));
        yield return ("major 7", GetFormula("major 7"));
        yield return ("minor 7", GetFormula("minor 7"));
    }

    private static bool HasChordIntent(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("chord", StringComparison.Ordinal) ||
               normalized.Contains("triad", StringComparison.Ordinal) ||
               normalized.Contains("note", StringComparison.Ordinal);
    }

    private static string Spell(string root, int pitchClass, int letterSteps)
    {
        var rootLetter = char.ToUpperInvariant(root[0]);
        var rootIndex = Array.IndexOf(NaturalLetters, rootLetter);
        var targetLetter = NaturalLetters[(rootIndex + letterSteps) % NaturalLetters.Length];
        var targetNatural = NaturalPitchClasses[targetLetter];
        var normalized = ((pitchClass % 12) + 12) % 12;
        var accidental = ((normalized - targetNatural) % 12 + 12) % 12;

        return accidental switch
        {
            0 => targetLetter.ToString(),
            1 => $"{targetLetter}#",
            2 => $"{targetLetter}##",
            10 => $"{targetLetter}bb",
            11 => $"{targetLetter}b",
            _ => $"{targetLetter}{(accidental < 6 ? new string('#', accidental) : new string('b', 12 - accidental))}"
        };
    }

    private static string JoinNotes(IReadOnlyList<string> notes) =>
        notes.Count switch
        {
            0 => string.Empty,
            1 => notes[0],
            2 => $"{notes[0]} and {notes[1]}",
            _ => $"{string.Join(", ", notes.Take(notes.Count - 1))}, and {notes[^1]}"
        };

    [GeneratedRegex(@"\b(?<root>[A-Ga-g][#b]?)\s*(?<quality>major 7|minor 7|dominant 7|major|minor|maj|min|diminished|dim|augmented|aug|7)?\s*(?:chord|triad)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ChordQuestionRegex();

    [GeneratedRegex(@"\b(?<root>[A-Ga-g][#b]?)(?<quality>maj7|min7|m7|maj|min|m|dim|aug|7)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompactChordRegex();

    [GeneratedRegex(@"\b(?:which|what)\s+chord\s+(?:contains|has|uses)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoteSetQuestionRegex();

    [GeneratedRegex(@"(?<![A-Za-z])(?<note>[A-Ga-g][#b]?)(?![A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex NoteTokenRegex();

    private sealed record ChordFormula(
        string DisplayName,
        IReadOnlyList<int> Intervals,
        IReadOnlyList<int> LetterSteps,
        IReadOnlyList<string> IntervalNames);
}
