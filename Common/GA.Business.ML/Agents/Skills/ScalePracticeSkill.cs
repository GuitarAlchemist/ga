namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Context;
using GA.Business.Core.Session;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Generates targeted scale practice exercises with note sequences and BPM suggestions.
/// Pure domain logic — no LLM calls.
/// </summary>
public sealed class ScalePracticeSkill(
    ISessionContextProvider sessionContextProvider,
    ILogger<ScalePracticeSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ScalePractice";
    public string Description => "Generates targeted scale practice exercises";

    private static readonly Regex ScalePattern = new(
        @"\b(?:practice|learn|exercises?\s+for|drill|work\s+on)\s+(?:the\s+)?([A-G][#b]?\s*(?:major|minor|pentatonic|blues|dorian|mixolydian|lydian|phrygian|locrian)?(?:\s+(?:scale|pentatonic))?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message) => ScalePattern.IsMatch(message);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var match = ScalePattern.Match(message);
        if (!match.Success)
            return Task.FromResult(CannotHelp("Could not parse a scale name from your message."));

        var scaleStr = match.Groups[1].Value.Trim();
        var session = sessionContextProvider.GetContext();
        var level = session.SkillLevel ?? SkillLevel.Beginner;

        // Try to resolve key from domain
        var keyMatch = Regex.Match(scaleStr, @"([A-G][#b]?)\s*(major|minor)", RegexOptions.IgnoreCase);
        Key? key = null;
        if (keyMatch.Success)
        {
            var rootStr = keyMatch.Groups[1].Value;
            var isMinor = keyMatch.Groups[2].Value.ToLowerInvariant() is "minor";
            key = Key.Items.FirstOrDefault(k =>
                k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major) &&
                string.Equals(k.Root.ToString(), rootStr, StringComparison.OrdinalIgnoreCase));
        }

        var exercises = BuildExercises(scaleStr, key, level);
        logger.LogDebug("ScalePracticeSkill: generated exercises for {Scale}, level={Level}", scaleStr, level);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Technique,
            Result     = exercises,
            Confidence = 1.0f,
            Evidence   = [$"Scale: {scaleStr}", $"Skill level: {level}"],
            Assumptions = key is null ? [$"Could not resolve '{scaleStr}' from domain — using generic exercises"] : []
        });
    }

    private static string BuildExercises(string scaleName, Key? key, SkillLevel level)
    {
        var sb = new StringBuilder();
        var bpm = level switch
        {
            SkillLevel.Beginner     => 60,
            SkillLevel.Intermediate => 90,
            SkillLevel.Advanced     => 120,
            SkillLevel.Expert       => 140,
            _                       => 80
        };

        sb.AppendLine($"## {scaleName} Practice Exercises");
        sb.AppendLine();

        // Show notes if we resolved the key
        if (key is not null)
        {
            var notes = key.Notes.Select(n => n.ToString());
            sb.AppendLine($"**Notes:** {string.Join(" – ", notes)}");
            sb.AppendLine();
        }

        // Exercise 1: Ascending/Descending
        sb.AppendLine($"### 1. Ascending & Descending ({bpm} BPM)");
        sb.AppendLine($"- Play the {scaleName} ascending from the lowest root to the highest reachable note");
        sb.AppendLine($"- Then descend back down");
        sb.AppendLine("- Use alternate picking (down-up-down-up)");
        sb.AppendLine("- Repeat 4 times");
        sb.AppendLine();

        // Exercise 2: Sequences
        sb.AppendLine($"### 2. Diatonic Sequences ({bpm + 10} BPM)");
        sb.AppendLine("- Play in groups of 3: 1-2-3, 2-3-4, 3-4-5, etc.");
        sb.AppendLine("- Then in groups of 4: 1-2-3-4, 2-3-4-5, etc.");
        if (level >= SkillLevel.Intermediate)
            sb.AppendLine("- Try descending sequences too: 8-7-6, 7-6-5, etc.");
        sb.AppendLine();

        // Exercise 3: Position exercises
        sb.AppendLine($"### 3. Position Playing ({bpm} BPM)");
        if (level == SkillLevel.Beginner)
        {
            sb.AppendLine("- Play the scale in open position (frets 0-3)");
            sb.AppendLine("- Focus on one octave");
        }
        else
        {
            sb.AppendLine("- Play in 5 different positions across the neck");
            sb.AppendLine("- Connect positions by sliding between them");
        }
        sb.AppendLine();

        // Exercise 4: Interval skips (intermediate+)
        if (level >= SkillLevel.Intermediate)
        {
            sb.AppendLine($"### 4. Interval Skips ({bpm - 10} BPM)");
            sb.AppendLine("- Play in thirds: 1-3, 2-4, 3-5, 4-6, etc.");
            sb.AppendLine("- Play in sixths: 1-6, 2-7, 3-8, etc.");
            if (level >= SkillLevel.Advanced)
                sb.AppendLine("- Play in octaves: 1-8, 2-9, etc.");
            sb.AppendLine();
        }

        // Exercise 5: Improvisation (advanced+)
        if (level >= SkillLevel.Advanced)
        {
            sb.AppendLine($"### 5. Creative Application ({bpm + 20} BPM)");
            sb.AppendLine("- Improvise a melody using only this scale over a backing track");
            sb.AppendLine("- Try creating phrases that resolve to the root");
            sb.AppendLine("- Experiment with rhythmic variation: mix quarter notes, eighths, and triplets");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"*Start at **{bpm} BPM** with a metronome. Increase by 5 BPM when you can play cleanly at the current tempo.*");

        return sb.ToString();
    }

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Technique,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Could not parse scale from message"]
    };
}
