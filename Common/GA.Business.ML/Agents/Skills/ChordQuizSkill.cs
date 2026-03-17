namespace GA.Business.ML.Agents.Skills;

using System.Text.Json;
using System.Text.RegularExpressions;
using GA.Business.Core.Context;
using GA.Business.Core.Session;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Generates chord identification quizzes — presents notes and asks the user to name the chord.
/// Shares quiz state pattern with <see cref="QuizAnswerSkill"/>.
/// </summary>
public sealed class ChordQuizSkill(
    MemoryStore memoryStore,
    ISessionContextProvider sessionContextProvider,
    ILogger<ChordQuizSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ChordQuiz";
    public string Description => "Generates chord identification exercises";

    private static readonly Regex QuizPattern = new(
        @"\b(?:chord\s+quiz|identify\s+(?:this\s+)?chord|name\s+(?:the\s+)?chord|chord\s+(?:exercise|drill|test))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] NoteNames =
        ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // (quality name, semitone intervals from root, display suffix)
    private static readonly (string name, int[] intervals, string suffix)[] ChordTypes =
    [
        ("major",      [0, 4, 7],     ""),
        ("minor",      [0, 3, 7],     "m"),
        ("diminished", [0, 3, 6],     "dim"),
        ("augmented",  [0, 4, 8],     "aug"),
        ("major 7th",  [0, 4, 7, 11], "maj7"),
        ("minor 7th",  [0, 3, 7, 10], "m7"),
        ("dominant 7th",[0, 4, 7, 10],"7"),
        ("sus2",       [0, 2, 7],     "sus2"),
        ("sus4",       [0, 5, 7],     "sus4"),
    ];

    public bool CanHandle(string message) => QuizPattern.IsMatch(message);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var session = sessionContextProvider.GetContext();
        var level = session.SkillLevel ?? SkillLevel.Beginner;

        var rng = Random.Shared;
        var availableChords = GetChordsForLevel(level);
        var chord = availableChords[rng.Next(availableChords.Length)];
        var rootIdx = rng.Next(NoteNames.Length);
        var root = NoteNames[rootIdx];

        var noteNames = chord.intervals
            .Select(i => NoteNames[(rootIdx + i) % NoteNames.Length])
            .ToArray();

        var correctAnswer = $"{root}{chord.suffix}";

        // Store quiz state
        var quizState = JsonSerializer.Serialize(new
        {
            type = "chord",
            correctAnswer,
            root,
            quality = chord.name,
            notes = string.Join(", ", noteNames),
            timestamp = DateTimeOffset.UtcNow
        });
        memoryStore.Write("active_quiz", "quiz", quizState, ["quiz", "chord"]);

        logger.LogDebug("ChordQuizSkill: generated quiz — {Notes} = {Chord}", string.Join(", ", noteNames), correctAnswer);

        var question = $"**Chord Quiz!**\n\n" +
                       $"What chord do these notes form: **{string.Join(" – ", noteNames)}**?\n\n" +
                       $"*(Type the chord name, e.g. \"Cmaj7\" or \"Am\")*";

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = question,
            Confidence = 1.0f,
            Evidence   = [$"Notes: {string.Join(", ", noteNames)}"],
            Assumptions = []
        });
    }

    private static (string name, int[] intervals, string suffix)[] GetChordsForLevel(SkillLevel level) => level switch
    {
        SkillLevel.Beginner     => ChordTypes[..3],  // major, minor, diminished
        SkillLevel.Intermediate => ChordTypes[..7],  // + augmented, maj7, m7, dom7
        _                       => ChordTypes        // all including sus
    };
}
