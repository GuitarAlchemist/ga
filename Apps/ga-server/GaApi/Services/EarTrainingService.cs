namespace GaApi.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Generates ear training exercises for interval recognition, chord quality identification,
/// and scale/mode discrimination. Provides both structured quizzes and random generation.
/// </summary>
public class EarTrainingService
{
    private readonly ILogger<EarTrainingService> _logger;
    private readonly IChatClient _chatClient;

    public EarTrainingService(
        ILogger<EarTrainingService> logger,
        IChatClient chatClient)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    /// <summary>
    /// Interval training difficulty levels.
    /// </summary>
    public enum IntervalDifficulty
    {
        Basic,        // Unison to octave
        Chromatic,    // All 12 semitones
        Extended      // Intervals beyond octave
    }

    /// <summary>
    /// Chord quality types for ear training.
    /// </summary>
    public enum ChordQuality
    {
        Major,
        Minor,
        Dominant7,
        MajorSeventh,
        MinorSeventh,
        HalfDiminished,
        Diminished,
        Augmented,
        Sus2,
        Sus4
    }

    /// <summary>
    /// Scale/mode recognition types.
    /// </summary>
    public enum ScaleType
    {
        Major,
        NaturalMinor,
        HarmonicMinor,
        MelodicMinor,
        Dorian,
        Phrygian,
        Lydian,
        Mixolydian,
        Blues,
        Harmonic_Major
    }

    public record IntervalQuestion(
        string IntervalName,
        int Semitones,
        string RootNote,
        string AudioDescription,
        List<string> MultipleChoiceOptions,
        string CorrectAnswer);

    public record ChordQuestion(
        string ChordName,
        ChordQuality Quality,
        string RootNote,
        string Voicing,
        string AudioDescription,
        List<string> MultipleChoiceOptions,
        string CorrectAnswer);

    public record ScaleQuestion(
        string ScaleName,
        ScaleType Type,
        string StartingNote,
        string AudioDescription,
        List<string> MultipleChoiceOptions,
        string CorrectAnswer);

    public record EarTrainingQuiz(
        string Title,
        string Description,
        int QuestionCount,
        List<object> Questions,
        TimeSpan EstimatedDuration);

    /// <summary>
    /// Generate an interval recognition quiz.
    /// </summary>
    public async Task<Result<EarTrainingQuiz>> GenerateIntervalQuizAsync(
        IntervalDifficulty difficulty,
        int questionCount = 10)
    {
        try
        {
            var questions = new List<object>();
            var intervals = GetIntervalsForDifficulty(difficulty);

            for (int i = 0; i < questionCount; i++)
            {
                var interval = intervals[Random.Shared.Next(intervals.Count)];
                var rootNote = GetRandomNote();

                var question = new IntervalQuestion(
                    IntervalName: interval.Name,
                    Semitones: interval.Semitones,
                    RootNote: rootNote,
                    AudioDescription: $"{interval.Name} from {rootNote}",
                    MultipleChoiceOptions: GenerateIntervalChoices(interval),
                    CorrectAnswer: interval.Name
                );

                questions.Add(question);
            }

            var quiz = new EarTrainingQuiz(
                Title: $"{difficulty} Interval Recognition Quiz",
                Description: "Identify intervals by ear and match them to their names.",
                QuestionCount: questionCount,
                Questions: questions,
                EstimatedDuration: TimeSpan.FromMinutes(questionCount * 0.5)
            );

            _logger.LogInformation("Generated {QuestionCount} interval questions at {Difficulty} level",
                questionCount, difficulty);

            return Result.Ok(quiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate interval quiz");
            return Result.Fail<EarTrainingQuiz>(ex.Message);
        }
    }

    /// <summary>
    /// Generate a chord quality recognition quiz.
    /// </summary>
    public async Task<Result<EarTrainingQuiz>> GenerateChordQuizAsync(
        int questionCount = 10,
        List<ChordQuality>? allowedQualities = null)
    {
        try
        {
            var questions = new List<object>();
            var qualities = allowedQualities ?? Enum.GetValues<ChordQuality>().ToList();

            for (int i = 0; i < questionCount; i++)
            {
                var quality = qualities[Random.Shared.Next(qualities.Count)];
                var rootNote = GetRandomNote();

                var question = new ChordQuestion(
                    ChordName: $"{rootNote} {quality}",
                    Quality: quality,
                    RootNote: rootNote,
                    Voicing: GetVoicingForQuality(quality, rootNote),
                    AudioDescription: $"{rootNote} {quality} chord",
                    MultipleChoiceOptions: GenerateChordChoices(rootNote),
                    CorrectAnswer: quality.ToString()
                );

                questions.Add(question);
            }

            var quiz = new EarTrainingQuiz(
                Title: "Chord Quality Recognition Quiz",
                Description: "Identify the quality of chords played in isolation.",
                QuestionCount: questionCount,
                Questions: questions,
                EstimatedDuration: TimeSpan.FromMinutes(questionCount * 0.75)
            );

            _logger.LogInformation("Generated {QuestionCount} chord quality questions", questionCount);

            return Result.Ok(quiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate chord quiz");
            return Result.Fail<EarTrainingQuiz>(ex.Message);
        }
    }

    /// <summary>
    /// Generate a scale/mode discrimination quiz.
    /// </summary>
    public async Task<Result<EarTrainingQuiz>> GenerateScaleQuizAsync(
        int questionCount = 10,
        List<ScaleType>? allowedScales = null)
    {
        try
        {
            var questions = new List<object>();
            var scaleTypes = allowedScales ?? Enum.GetValues<ScaleType>().ToList();

            for (int i = 0; i < questionCount; i++)
            {
                var scaleType = scaleTypes[Random.Shared.Next(scaleTypes.Count)];
                var startingNote = GetRandomNote();

                var question = new ScaleQuestion(
                    ScaleName: $"{startingNote} {scaleType}",
                    Type: scaleType,
                    StartingNote: startingNote,
                    AudioDescription: $"{startingNote} {scaleType} scale ascending",
                    MultipleChoiceOptions: GenerateScaleChoices(scaleType, startingNote),
                    CorrectAnswer: scaleType.ToString()
                );

                questions.Add(question);
            }

            var quiz = new EarTrainingQuiz(
                Title: "Scale/Mode Recognition Quiz",
                Description: "Identify scales and modes by their characteristic sounds.",
                QuestionCount: questionCount,
                Questions: questions,
                EstimatedDuration: TimeSpan.FromMinutes(questionCount * 1.0)
            );

            _logger.LogInformation("Generated {QuestionCount} scale recognition questions", questionCount);

            return Result.Ok(quiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate scale quiz");
            return Result.Fail<EarTrainingQuiz>(ex.Message);
        }
    }

    /// <summary>
    /// Generate a progressive ear training curriculum.
    /// </summary>
    public EarTrainingCurriculum GetProgressiveCurriculum()
    {
        return new EarTrainingCurriculum(
            Modules: new List<CurriculumModule>
            {
                new("Module 1: Foundational Intervals",
                    "Master the most common intervals.",
                    new List<EarTrainingQuiz>
                    {
                        BuildQuiz("Major 2nd & Perfect 5th", 5),
                        BuildQuiz("Perfect 4th & Octave", 5),
                        BuildQuiz("Minor 3rd & Major 3rd", 5)
                    }),

                new("Module 2: Extended Intervals",
                    "Recognize less common interval relationships.",
                    new List<EarTrainingQuiz>
                    {
                        BuildQuiz("Minor 6th & Major 6th", 5),
                        BuildQuiz("Diminished 5th & Augmented 4th", 5),
                        BuildQuiz("All Intervals Mixed", 10)
                    }),

                new("Module 3: Chord Qualities",
                    "Distinguish major, minor, and seventh chord varieties.",
                    new List<EarTrainingQuiz>
                    {
                        BuildQuiz("Major vs. Minor Triads", 5),
                        BuildQuiz("Seventh Chord Varieties", 5),
                        BuildQuiz("Suspended & Augmented Chords", 5)
                    }),

                new("Module 4: Scale Recognition",
                    "Identify characteristic scale sounds.",
                    new List<EarTrainingQuiz>
                    {
                        BuildQuiz("Major vs. Minor Scales", 5),
                        BuildQuiz("Modal Discrimination", 5),
                        BuildQuiz("Blues Scale vs. Others", 5)
                    })
            },
            TotalEstimatedHours: 8
        );
    }

    // ────── Helpers ──────────────────────────────────────────────

    private List<(string Name, int Semitones)> GetIntervalsForDifficulty(IntervalDifficulty difficulty)
    {
        return difficulty switch
        {
            IntervalDifficulty.Basic => new List<(string, int)>
            {
                ("Unison", 0),
                ("Minor 2nd", 1),
                ("Major 2nd", 2),
                ("Minor 3rd", 3),
                ("Major 3rd", 4),
                ("Perfect 4th", 5),
                ("Diminished 5th", 6),
                ("Perfect 5th", 7),
                ("Minor 6th", 8),
                ("Major 6th", 9),
                ("Minor 7th", 10),
                ("Major 7th", 11),
                ("Octave", 12)
            },
            IntervalDifficulty.Chromatic => new List<(string, int)>
            {
                ("Unison", 0), ("Minor 2nd", 1), ("Major 2nd", 2), ("Minor 3rd", 3),
                ("Major 3rd", 4), ("Perfect 4th", 5), ("Tritone", 6), ("Perfect 5th", 7),
                ("Minor 6th", 8), ("Major 6th", 9), ("Minor 7th", 10), ("Major 7th", 11),
                ("Octave", 12)
            },
            IntervalDifficulty.Extended => new List<(string, int)>
            {
                ("Minor 9th", 13), ("Major 9th", 14), ("Minor 10th", 15), ("Major 10th", 16),
                ("Perfect 11th", 17), ("Augmented 11th", 18), ("Perfect 12th", 19),
                ("Minor 13th", 20), ("Major 13th", 21), ("Double Octave", 24)
            },
            _ => new List<(string, int)>()
        };
    }

    private List<string> GenerateIntervalChoices(
        (string Name, int Semitones) correctInterval)
    {
        var allIntervals = GetIntervalsForDifficulty(IntervalDifficulty.Extended)
            .Select(i => i.Name)
            .ToList();

        var choices = new HashSet<string> { correctInterval.Name };

        while (choices.Count < 4)
        {
            var randomInterval = allIntervals[Random.Shared.Next(allIntervals.Count)];
            choices.Add(randomInterval);
        }

        return choices.OrderBy(_ => Random.Shared.Next()).ToList();
    }

    private List<string> GenerateChordChoices(string rootNote)
    {
        var qualities = Enum.GetValues<ChordQuality>()
            .Select(q => $"{rootNote} {q}")
            .ToList();

        return qualities
            .OrderBy(_ => Random.Shared.Next())
            .Take(4)
            .ToList();
    }

    private List<string> GenerateScaleChoices(ScaleType correctScale, string startingNote)
    {
        var scales = Enum.GetValues<ScaleType>()
            .Select(s => $"{startingNote} {s}")
            .ToList();

        return scales
            .OrderBy(_ => Random.Shared.Next())
            .Take(4)
            .ToList();
    }

    private string GetRandomNote()
    {
        var notes = new[] { "C", "D", "E", "F", "G", "A", "B" };
        return notes[Random.Shared.Next(notes.Length)];
    }

    private string GetVoicingForQuality(ChordQuality quality, string rootNote)
    {
        return quality switch
        {
            ChordQuality.Major => $"{rootNote}-E-G",
            ChordQuality.Minor => $"{rootNote}-Eb-G",
            ChordQuality.Dominant7 => $"{rootNote}-E-G-Bb",
            ChordQuality.MajorSeventh => $"{rootNote}-E-G-B",
            ChordQuality.MinorSeventh => $"{rootNote}-Eb-G-Bb",
            ChordQuality.HalfDiminished => $"{rootNote}-Eb-Gb-Bb",
            ChordQuality.Diminished => $"{rootNote}-Eb-Gb-Bbb",
            ChordQuality.Augmented => $"{rootNote}-E-G#",
            ChordQuality.Sus2 => $"{rootNote}-D-G",
            ChordQuality.Sus4 => $"{rootNote}-F-G",
            _ => rootNote
        };
    }

    private EarTrainingQuiz BuildQuiz(string name, int questionCount)
    {
        return new EarTrainingQuiz(
            Title: name,
            Description: $"Quiz on {name}",
            QuestionCount: questionCount,
            Questions: new List<object>(),
            EstimatedDuration: TimeSpan.FromMinutes(questionCount * 0.5)
        );
    }
}

/// <summary>
/// Ear training curriculum structure.
/// </summary>
public record EarTrainingCurriculum(
    List<CurriculumModule> Modules,
    double TotalEstimatedHours);

/// <summary>
/// Single curriculum module.
/// </summary>
public record CurriculumModule(
    string Name,
    string Description,
    List<EarTrainingService.EarTrainingQuiz> Quizzes);
