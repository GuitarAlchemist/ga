namespace GaApi.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

/// <summary>
/// Generates practice routines for specific techniques, difficulty levels, and time constraints.
/// Integrates with the orchestrator pipeline for LLM-powered exercise generation.
/// </summary>
public class PracticeRoutineService
{
    private readonly ILogger<PracticeRoutineService> _logger;
    private readonly IChatClient _chatClient;

    public PracticeRoutineService(
        ILogger<PracticeRoutineService> logger,
        IChatClient chatClient)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    /// <summary>
    /// Difficulty levels for practice routines.
    /// </summary>
    public enum DifficultyLevel
    {
        Beginner,
        Intermediate,
        Advanced,
        Expert
    }

    /// <summary>
    /// Practice focus areas.
    /// </summary>
    public enum FocusArea
    {
        Scales,
        Chords,
        Arpeggios,
        Finger_Dexterity,
        Rhythm,
        Picking,
        Bending,
        Vibrato,
        Blues,
        Jazz,
        Technique_Agnostic
    }

    public record PracticeRoutine(
        string Title,
        string Description,
        DifficultyLevel Difficulty,
        FocusArea Focus,
        int DurationMinutes,
        List<Exercise> Exercises,
        string TipsAndTricks);

    public record Exercise(
        int Order,
        string Name,
        string Instructions,
        int DurationSeconds,
        string? FretboardContext,
        string? ScaleOrChord);

    /// <summary>
    /// Generate a practice routine for a specific technique, time, and difficulty.
    /// </summary>
    public async Task<Result<PracticeRoutine>> GenerateRoutineAsync(
        FocusArea focusArea,
        DifficultyLevel difficulty,
        int durationMinutes,
        string? userBackground = null)
    {
        try
        {
            var systemPrompt = BuildPracticePrompt(focusArea, difficulty, durationMinutes, userBackground);

            var messages = new List<AIChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, $"Generate a {durationMinutes}-minute {difficulty} practice routine for {focusArea}.")
            };

            var options = new ChatOptions
            {
                Temperature = 0.8f,
                MaxOutputTokens = 1024
            };

            var response = await _chatClient.GetResponseAsync(messages, options);
            var content = response.Text ?? string.Empty;

            // Parse the LLM response into a structured routine
            var routine = ParseRoutineResponse(content, focusArea, difficulty, durationMinutes);

            _logger.LogInformation(
                "Generated {Difficulty} practice routine for {FocusArea} ({DurationMinutes} min)",
                difficulty, focusArea, durationMinutes);

            return Result.Ok(routine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate practice routine");
            return Result.Fail<PracticeRoutine>(ex.Message);
        }
    }

    /// <summary>
    /// Get pre-built routine templates for common focus areas.
    /// </summary>
    public PracticeRoutine GetTemplateRoutine(FocusArea focusArea, DifficultyLevel difficulty)
    {
        return focusArea switch
        {
            FocusArea.Scales => BuildScaleRoutine(difficulty),
            FocusArea.Chords => BuildChordRoutine(difficulty),
            FocusArea.Arpeggios => BuildArpeggioRoutine(difficulty),
            FocusArea.Blues => BuildBluesRoutine(difficulty),
            FocusArea.Finger_Dexterity => BuildFingerDexterityRoutine(difficulty),
            _ => BuildGenericRoutine(difficulty)
        };
    }

    // ────── Template Builders ──────────────────────────────────────

    private PracticeRoutine BuildScaleRoutine(DifficultyLevel difficulty)
    {
        var exercises = difficulty switch
        {
            DifficultyLevel.Beginner => new List<Exercise>
            {
                new(1, "Pentatonic Minor - Slow Ascent",
                    "Play the A minor pentatonic scale up and down 3 times, slowly and deliberately.",
                    180, "A minor pentatonic in position 1", "A minor pentatonic"),
                new(2, "Pentatonic Minor - Moderate Tempo",
                    "Play the same scale at a comfortable playing speed (100 BPM).",
                    180, "A minor pentatonic in position 1", "A minor pentatonic"),
                new(3, "Position Shift Practice",
                    "Move the pentatonic scale to position 5 and repeat the above exercises.",
                    120, "A minor pentatonic in position 5", "A minor pentatonic")
            },
            DifficultyLevel.Intermediate => new List<Exercise>
            {
                new(1, "Three-Note-Per-String Major Scale",
                    "Play the C major scale using three notes per string technique.",
                    240, "C major scale (3NPS)", "C major"),
                new(2, "Scale Mode Recognition",
                    "Play C major scale and identify Dorian, Phrygian, Lydian, Mixolydian modes.",
                    300, "C major modes", "C major modes"),
                new(3, "Speed Drill",
                    "Increase tempo progressively (100→120→140 BPM) with the 3NPS scale.",
                    240, "C major (3NPS)", "C major")
            },
            DifficultyLevel.Advanced => new List<Exercise>
            {
                new(1, "Harmonic Minor Exotic Runs",
                    "Practice fluid runs through the A harmonic minor scale at 160 BPM.",
                    300, "A harmonic minor (fluid)", "A harmonic minor"),
                new(2, "Legato Technique",
                    "Execute hammer-on and pull-off sequences through the scale.",
                    300, "A harmonic minor (legato)", "A harmonic minor"),
                new(3, "Polytonal Scale Exploration",
                    "Combine major and harmonic minor scales in call-and-response patterns.",
                    240, "Multiple modes", "A minor / A harmonic minor")
            },
            _ => new List<Exercise>()
        };

        return new PracticeRoutine(
            Title: $"{difficulty} Scale Mastery",
            Description: "Focused scale practice with position shifts and modal awareness.",
            Difficulty: difficulty,
            Focus: FocusArea.Scales,
            DurationMinutes: 15,
            Exercises: exercises,
            TipsAndTricks: "Focus on clean finger placement and consistent tempo. Use a metronome."
        );
    }

    private PracticeRoutine BuildChordRoutine(DifficultyLevel difficulty)
    {
        var exercises = difficulty switch
        {
            DifficultyLevel.Beginner => new List<Exercise>
            {
                new(1, "Four Essential Chords",
                    "Practice E major, A major, D major, and G major. 10 strums each, 2 seconds apart.",
                    240, "EADG chords", "E maj, A maj, D maj, G maj"),
                new(2, "Chord Transitions",
                    "Transition smoothly from E to A, then A to D, then D to G.",
                    180, "EADG transitions", "E maj → A maj → D maj → G maj"),
                new(3, "Strumming Pattern",
                    "Practice downstrokes only on each chord (8 beats per chord).",
                    120, "EADG chords", "E maj, A maj, D maj, G maj")
            },
            DifficultyLevel.Intermediate => new List<Exercise>
            {
                new(1, "Jazz Seventh Chords",
                    "Practice Cmaj7, C7, Cm7, and Cm7b5. Focus on finger placement efficiency.",
                    300, "Jazz seventh chords", "Cmaj7, C7, Cm7, Cm7b5"),
                new(2, "Voice Leading",
                    "Practice smooth chord transitions using minimal finger movement.",
                    240, "Voice leading patterns", "Jazz chord progressions"),
                new(3, "Chord Arpeggiation",
                    "Arpeggiate each chord vertically across the fretboard.",
                    240, "Arpeggiated voicings", "Cmaj7, C7, Cm7, Cm7b5")
            },
            DifficultyLevel.Advanced => new List<Exercise>
            {
                new(1, "Extended Voicings",
                    "Practice 9th, 11th, and 13th extensions on Cmaj chord.",
                    300, "Extended voicings", "Cmaj, Cmaj9, Cmaj11, Cmaj13"),
                new(2, "Polychord Exploration",
                    "Practice slash chords and upper structure triads.",
                    300, "Polychords", "C/G, Emaj/C, etc."),
                new(3, "Harmonic Rhythm",
                    "Play complex jazz progressions with varied rhythmic articulations.",
                    240, "Complex progressions", "Jazz standards")
            },
            _ => new List<Exercise>()
        };

        return new PracticeRoutine(
            Title: $"{difficulty} Chord Dexterity",
            Description: "Build finger strength and chord transition fluency.",
            Difficulty: difficulty,
            Focus: FocusArea.Chords,
            DurationMinutes: 15,
            Exercises: exercises,
            TipsAndTricks: "Record yourself and listen back. Small, deliberate movements are faster than large ones."
        );
    }

    private PracticeRoutine BuildArpeggioRoutine(DifficultyLevel difficulty)
    {
        var exercises = difficulty switch
        {
            DifficultyLevel.Beginner => new List<Exercise>
            {
                new(1, "Basic Triad Arpeggios",
                    "Play E major triad arpeggio in first position, ascending and descending.",
                    180, "E major arpeggio (position 1)", "E major"),
                new(2, "Position Shifts",
                    "Move the same arpeggio to positions 5, 7, and 9.",
                    240, "E major arpeggio (multiple positions)", "E major"),
                new(3, "Finger Picking",
                    "Use alternating fingers (index, middle, ring) to pick the arpeggio cleanly.",
                    180, "E major arpeggio (finger pick)", "E major")
            },
            DifficultyLevel.Intermediate => new List<Exercise>
            {
                new(1, "Seventh Chord Arpeggios",
                    "Practice Cmaj7, Cm7, C7, and Cdim7 arpeggios across the neck.",
                    300, "Seventh arpeggio shapes", "Cmaj7, Cm7, C7, Cdim7"),
                new(2, "Sweep Picking Introduction",
                    "Learn basic 3-string and 4-string sweep patterns.",
                    240, "Sweep picking basics", "Various chords"),
                new(3, "Speed Drill",
                    "Execute arpeggios cleanly at 120 BPM.",
                    240, "Arpeggio speed", "Various arpeggios")
            },
            DifficultyLevel.Advanced => new List<Exercise>
            {
                new(1, "Eight-String Sweep Patterns",
                    "Master complex sweep picking across all six strings.",
                    300, "Advanced sweep picking", "Extended voicings"),
                new(2, "Legato Arpeggios",
                    "Use hammer-ons and pull-offs within arpeggio patterns.",
                    300, "Legato technique", "Various arpeggios"),
                new(3, "Rhythmic Variation",
                    "Play arpeggios with syncopated rhythms and dynamic accents.",
                    240, "Rhythmic arpeggios", "Jazz standards")
            },
            _ => new List<Exercise>()
        };

        return new PracticeRoutine(
            Title: $"{difficulty} Arpeggio Mastery",
            Description: "Develop fluidity and dexterity with broken chord patterns.",
            Difficulty: difficulty,
            Focus: FocusArea.Arpeggios,
            DurationMinutes: 15,
            Exercises: exercises,
            TipsAndTricks: "Let the pick gravity do the work in sweep picking. Lighten your grip for faster passages."
        );
    }

    private PracticeRoutine BuildBluesRoutine(DifficultyLevel difficulty)
    {
        var exercises = difficulty switch
        {
            DifficultyLevel.Beginner => new List<Exercise>
            {
                new(1, "12-Bar Blues Progression",
                    "Learn the 12-bar blues progression: I-I-I-I-IV-IV-I-I-V-IV-I-V",
                    240, "12-bar blues", "A minor pentatonic"),
                new(2, "Basic Lick Library",
                    "Learn 3 fundamental blues licks over the progression.",
                    240, "Blues licks", "A minor pentatonic"),
                new(3, "Call and Response",
                    "Alternate between backing track and your improvised responses.",
                    180, "12-bar blues", "A minor pentatonic")
            },
            DifficultyLevel.Intermediate => new List<Exercise>
            {
                new(1, "Blues Modes",
                    "Explore blues scale variations and harmonic minor in blues context.",
                    300, "Blues modes", "Blues scale, harmonic minor"),
                new(2, "Bending Techniques",
                    "Master quarter-tone, half-step, and full-step bends.",
                    240, "Bending techniques", "Various blues licks"),
                new(3, "Shuffle Rhythm",
                    "Play over swing shuffle backing tracks.",
                    240, "Shuffle rhythm", "Blues progressions")
            },
            DifficultyLevel.Advanced => new List<Exercise>
            {
                new(1, "Modern Blues Harmony",
                    "Explore tritone substitutions and modal interchange in blues.",
                    300, "Advanced harmony", "Complex blues progressions"),
                new(2, "Expression Techniques",
                    "Combine bending, vibrato, and dynamic picking for emotional depth.",
                    300, "Expression", "Blues standards"),
                new(3, "Jam Session Simulation",
                    "Full performance of 3-5 minute blues improvisation.",
                    240, "Full performance", "Blues progression")
            },
            _ => new List<Exercise>()
        };

        return new PracticeRoutine(
            Title: $"{difficulty} Blues Mastery",
            Description: "Develop blues vocabulary and improvisational skills.",
            Difficulty: difficulty,
            Focus: FocusArea.Blues,
            DurationMinutes: 15,
            Exercises: exercises,
            TipsAndTricks: "Listen to classic blues artists. Feel the groove, not just the notes."
        );
    }

    private PracticeRoutine BuildFingerDexterityRoutine(DifficultyLevel difficulty)
    {
        var exercises = difficulty switch
        {
            DifficultyLevel.Beginner => new List<Exercise>
            {
                new(1, "Finger Independence",
                    "Play individual chromatic notes with each finger in sequence.",
                    180, "Chromatic positions", "Chromatic scale"),
                new(2, "Fretting Hand Strength",
                    "Hold each note for 2 seconds, then release. Repeat 10 times per note.",
                    240, "Chord shapes", "E major, A major, D major"),
                new(3, "Finger Stretches",
                    "Practice stretches across 4 and 5 frets to build flexibility.",
                    180, "Stretch positions", "Extended chord shapes")
            },
            DifficultyLevel.Intermediate => new List<Exercise>
            {
                new(1, "Trill Exercises",
                    "Perform fast trills between adjacent notes at 120 BPM.",
                    240, "Trill patterns", "Various pitch pairs"),
                new(2, "Finger Tapping",
                    "Practice single-note tapping sequences to build precision.",
                    240, "Tapping sequences", "Various scales"),
                new(3, "Chromatic Runs",
                    "Execute fast chromatic passages across the entire neck.",
                    240, "Chromatic runs", "Chromatic scale")
            },
            DifficultyLevel.Advanced => new List<Exercise>
            {
                new(1, "Hybrid Picking",
                    "Develop fluency with pick + finger combinations.",
                    300, "Hybrid picking patterns", "Various techniques"),
                new(2, "Rapid Position Shifts",
                    "Transition quickly between distant fretboard positions.",
                    300, "Position shifts", "Scale passages"),
                new(3, "Endurance Challenge",
                    "Sustain complex finger patterns at high speed for extended duration.",
                    240, "Speed endurance", "Challenging sequences")
            },
            _ => new List<Exercise>()
        };

        return new PracticeRoutine(
            Title: $"{difficulty} Finger Dexterity Builder",
            Description: "Strengthen fingers and improve coordination and speed.",
            Difficulty: difficulty,
            Focus: FocusArea.Finger_Dexterity,
            DurationMinutes: 15,
            Exercises: exercises,
            TipsAndTricks: "Consistency beats intensity. Short, focused practice sessions build faster than long unfocused ones."
        );
    }

    private PracticeRoutine BuildGenericRoutine(DifficultyLevel difficulty)
    {
        return new PracticeRoutine(
            Title: "Balanced Practice Session",
            Description: "Well-rounded routine covering scales, chords, and technique.",
            Difficulty: difficulty,
            Focus: FocusArea.Technique_Agnostic,
            DurationMinutes: 30,
            Exercises: new List<Exercise>
            {
                new(1, "Warm-up", "Chromatic scales and finger stretches", 300, null, null),
                new(2, "Scales", "Major and minor scales in multiple positions", 600, null, null),
                new(3, "Chords", "Chord transitions and voicing practice", 600, null, null),
                new(4, "Technique", "Pick control and finger dexterity drills", 600, null, null),
                new(5, "Cool-down", "Slow, melodic playing and reflection", 300, null, null)
            },
            TipsAndTricks: "Mix technical work with musical playing. Make practice enjoyable."
        );
    }

    private string BuildPracticePrompt(
        FocusArea focusArea,
        DifficultyLevel difficulty,
        int durationMinutes,
        string? userBackground)
    {
        var background = userBackground ?? "general guitarist";

        return $"""
        You are a professional guitar coach creating personalized practice routines.

        Generate a {difficulty} practice routine for a {background} focusing on {focusArea}.
        Duration: {durationMinutes} minutes total.

        Structure the routine as:
        1. Warm-up exercise (if applicable)
        2. Main focus area exercises (3-4 exercises)
        3. Cool-down or integration exercise

        For each exercise, provide:
        - Name
        - Clear instructions
        - Estimated duration
        - Fretboard context (if applicable)
        - Specific scale/chord if relevant

        Make it practical, achievable, and progressive. Include tips for improvement.
        Format as structured JSON with exercises array.
        """;
    }

    private PracticeRoutine ParseRoutineResponse(
        string content,
        FocusArea focusArea,
        DifficultyLevel difficulty,
        int durationMinutes)
    {
        // Fallback to template if parsing fails
        try
        {
            // Simple extraction logic — in production, use JSON parsing
            var exercises = ExtractExercises(content);
            return new PracticeRoutine(
                Title: $"{difficulty} {focusArea} Practice",
                Description: content.Split('\n').FirstOrDefault(l => l.Length > 20) ?? "Focused practice routine",
                Difficulty: difficulty,
                Focus: focusArea,
                DurationMinutes: durationMinutes,
                Exercises: exercises,
                TipsAndTricks: "Stay consistent and play with purpose."
            );
        }
        catch
        {
            return GetTemplateRoutine(focusArea, difficulty);
        }
    }

    private List<Exercise> ExtractExercises(string content)
    {
        var exercises = new List<Exercise>();
        var lines = content.Split('\n');

        int order = 1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("-") || line.StartsWith("•"))
            {
                exercises.Add(new Exercise(
                    Order: order++,
                    Name: line.Trim('•', '-', ' '),
                    Instructions: i + 1 < lines.Length ? lines[i + 1].Trim() : "Practice this exercise",
                    DurationSeconds: 300,
                    FretboardContext: null,
                    ScaleOrChord: null
                ));
            }
        }

        return exercises.Any() ? exercises : [new Exercise(1, "Practice Exercise", content, 600, null, null)];
    }
}

/// <summary>
/// Result type for operation outcomes.
/// </summary>
public abstract record Result<T>
{
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Fail(string Message) : Result<T>;
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Result<T>.Ok(value);
    public static Result<T> Fail<T>(string message) => new Result<T>.Fail(message);
}
