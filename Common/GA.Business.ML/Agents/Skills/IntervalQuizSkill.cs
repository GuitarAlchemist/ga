namespace GA.Business.ML.Agents.Skills;

using System.Text.Json;
using System.Text.RegularExpressions;
using GA.Business.Core.Context;
using GA.Business.Core.Session;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Generates interval identification quizzes using domain interval objects.
/// Stores quiz state in <see cref="MemoryStore"/> for answer validation.
/// </summary>
public sealed class IntervalQuizSkill(
    MemoryStore memoryStore,
    ISessionContextProvider sessionContextProvider,
    ILogger<IntervalQuizSkill> logger) : IOrchestratorSkill
{
    public string Name        => "IntervalQuiz";
    public string Description => "Generates interval identification exercises";

    private static readonly Regex QuizPattern = new(
        @"\b(?:interval\s+quiz|test\s+my\s+intervals|ear\s+training|interval\s+(?:exercise|practice|drill|test))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] NoteNames =
        ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly (string name, int semitones)[] Intervals =
    [
        ("minor second", 1), ("major second", 2), ("minor third", 3), ("major third", 4),
        ("perfect fourth", 5), ("tritone", 6), ("perfect fifth", 7),
        ("minor sixth", 8), ("major sixth", 9), ("minor seventh", 10), ("major seventh", 11),
        ("octave", 12)
    ];

    public bool CanHandle(string message) => QuizPattern.IsMatch(message);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var session = sessionContextProvider.GetContext();
        var level = session.SkillLevel ?? SkillLevel.Beginner;

        var rng = Random.Shared;
        var availableIntervals = GetIntervalsForLevel(level);
        var interval = availableIntervals[rng.Next(availableIntervals.Length)];
        var rootIdx = rng.Next(NoteNames.Length);
        var root = NoteNames[rootIdx];
        var targetIdx = (rootIdx + interval.semitones) % NoteNames.Length;
        var target = NoteNames[targetIdx];

        // Store quiz state
        var quizState = JsonSerializer.Serialize(new
        {
            type = "interval",
            correctAnswer = interval.name,
            root,
            target,
            semitones = interval.semitones,
            timestamp = DateTimeOffset.UtcNow
        });
        memoryStore.Write("active_quiz", "quiz", quizState, ["quiz", "interval"]);

        logger.LogDebug("IntervalQuizSkill: generated quiz — {Root} to {Target} = {Interval}",
            root, target, interval.name);

        var question = $"**Interval Quiz!**\n\n" +
                       $"What interval is between **{root}** and **{target}**?\n\n" +
                       $"*(Type your answer, e.g. \"minor third\" or \"perfect fifth\")*";

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = question,
            Confidence = 1.0f,
            Evidence   = [$"Root: {root}", $"Target: {target}"],
            Assumptions = []
        });
    }

    private static (string name, int semitones)[] GetIntervalsForLevel(SkillLevel level) => level switch
    {
        SkillLevel.Beginner => Intervals[..5], // up to perfect fourth
        SkillLevel.Intermediate => Intervals[..8], // up to minor sixth
        _ => Intervals // all intervals
    };
}
