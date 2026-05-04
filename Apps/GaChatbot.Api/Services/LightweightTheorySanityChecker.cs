namespace GaChatbot.Api.Services;

public sealed class LightweightTheorySanityChecker
{
    public string Apply(string userMessage, PromptProfile promptProfile, string answer)
    {
        if (promptProfile != PromptProfile.Theory)
        {
            return answer;
        }

        var query = userMessage.Trim().ToLowerInvariant();

        if (IsMajorScaleModesQuery(query))
        {
            return BuildMajorScaleModesAnswer();
        }

        return answer;
    }

    private static bool IsMajorScaleModesQuery(string query) =>
        (query.Contains("modes of the major scale", StringComparison.OrdinalIgnoreCase)
         || query.Contains("modes of a major scale", StringComparison.OrdinalIgnoreCase)
         || query.Contains("seven modes", StringComparison.OrdinalIgnoreCase)
         || query.Contains("7 modes", StringComparison.OrdinalIgnoreCase))
        && query.Contains("mode", StringComparison.OrdinalIgnoreCase);

    private static string BuildMajorScaleModesAnswer() =>
        """
        The 7 modes of the major scale are the same notes, but each starts on a different degree:

        1. Ionian: major scale
        2. Dorian: minor with a natural 6
        3. Phrygian: minor with a flat 2
        4. Lydian: major with a sharp 4
        5. Mixolydian: major with a flat 7
        6. Aeolian: natural minor
        7. Locrian: diminished sound, with a flat 2 and flat 5

        In C major, that gives:
        C Ionian, D Dorian, E Phrygian, F Lydian, G Mixolydian, A Aeolian, B Locrian.
        """;
}
