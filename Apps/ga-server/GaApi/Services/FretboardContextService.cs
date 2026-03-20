namespace GaApi.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides fretboard-aware context for practice routines and scale exploration.
/// Maps scales, modes, and arpeggios to specific fretboard positions.
/// </summary>
public class FretboardContextService
{
    private readonly ILogger<FretboardContextService> _logger;

    public FretboardContextService(ILogger<FretboardContextService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fretboard positions (using standard CAGED system).
    /// </summary>
    public enum FretboardPosition
    {
        Position1,   // Open position
        Position2,
        Position5,   // A form (CAGED)
        Position7,   // D form (CAGED)
        Position9,   // G form (CAGED)
        Position12   // C form (CAGED)
    }

    public record ScaleShape(
        string Name,
        FretboardPosition Position,
        List<string> Pattern,
        string FretboardDiagram,
        string FingeringGuide);

    public record ModeExploration(
        string ScaleName,
        List<ModeShape> Modes,
        string ExplorationPath);

    public record ModeShape(
        string ModeName,
        string RelativeScale,
        List<string> IntervalPattern,
        string CharacteristicSound);

    public record ArpeggioShape(
        string ChordName,
        FretboardPosition Position,
        List<string> Pattern,
        string SweepPattern,
        string FretboardDiagram);

    /// <summary>
    /// Generate scale shapes for all CAGED positions.
    /// </summary>
    public List<ScaleShape> GetScaleShapes(string scaleName, string rootNote)
    {
        var shapes = new List<ScaleShape>();

        var positions = new[]
        {
            FretboardPosition.Position1,
            FretboardPosition.Position5,
            FretboardPosition.Position7,
            FretboardPosition.Position9,
            FretboardPosition.Position12
        };

        foreach (var position in positions)
        {
            var shape = GenerateScaleShape(scaleName, rootNote, position);
            if (shape != null)
                shapes.Add(shape);
        }

        return shapes;
    }

    /// <summary>
    /// Explore all modes of a scale with fretboard context.
    /// </summary>
    public ModeExploration GetModeExploration(string parentScaleName, string rootNote)
    {
        var modes = new List<ModeShape>();

        var modeMap = new Dictionary<string, (string RelativeScale, List<string> Intervals, string Sound)>
        {
            ["Ionian"] = ("Major", new List<string> { "W", "W", "H", "W", "W", "W", "H" }, "Bright, major sound"),
            ["Dorian"] = ("Minor with raised 6th", new List<string> { "W", "H", "W", "W", "W", "H", "W" }, "Minor with major 6th color"),
            ["Phrygian"] = ("Minor with lowered 2nd", new List<string> { "H", "W", "W", "W", "H", "W", "W" }, "Spanish, exotic minor"),
            ["Lydian"] = ("Major with raised 4th", new List<string> { "W", "W", "W", "H", "W", "W", "H" }, "Major with #4 brightness"),
            ["Mixolydian"] = ("Major with lowered 7th", new List<string> { "W", "W", "H", "W", "W", "H", "W" }, "Major with dominant sound"),
            ["Aeolian"] = ("Natural Minor", new List<string> { "W", "H", "W", "W", "H", "W", "W" }, "Natural minor"),
            ["Locrian"] = ("Diminished feeling", new List<string> { "H", "W", "W", "H", "W", "W", "W" }, "Diminished, spooky")
        };

        if (parentScaleName == "Major")
        {
            foreach (var (modeName, (relativeScale, intervals, sound)) in modeMap)
            {
                modes.Add(new ModeShape(
                    ModeName: modeName,
                    RelativeScale: relativeScale,
                    IntervalPattern: intervals,
                    CharacteristicSound: sound
                ));
            }
        }

        return new ModeExploration(
            ScaleName: parentScaleName,
            Modes: modes,
            ExplorationPath: "Start with Ionian (major), then explore variations using different starting notes"
        );
    }

    /// <summary>
    /// Get arpeggio shapes for a chord across different positions and voicings.
    /// </summary>
    public List<ArpeggioShape> GetArpeggioShapes(string chordName, string rootNote)
    {
        var shapes = new List<ArpeggioShape>();

        // Generate basic voicings
        var voicings = new[]
        {
            FretboardPosition.Position1,
            FretboardPosition.Position5,
            FretboardPosition.Position7,
            FretboardPosition.Position12
        };

        foreach (var position in voicings)
        {
            var shape = GenerateArpeggioShape(chordName, rootNote, position);
            if (shape != null)
                shapes.Add(shape);
        }

        return shapes;
    }

    /// <summary>
    /// Generate a 3-position scale exploration guide (beginner-friendly).
    /// </summary>
    public ScaleExplorationGuide GetBeginnerScaleGuide(string scaleName, string rootNote)
    {
        return new ScaleExplorationGuide(
            ScaleName: $"{rootNote} {scaleName}",
            Description: $"Learn {scaleName} in 3 strategic fretboard positions",
            Positions: new List<PositionGuide>
            {
                new(
                    Position: FretboardPosition.Position1,
                    FocusArea: "Open position and comfortable finger spacing",
                    Exercises: new List<string>
                    {
                        "Ascending and descending slowly",
                        "Position shifts to position 5",
                        "All notes on 3 strings"
                    }
                ),
                new(
                    Position: FretboardPosition.Position5,
                    FocusArea: "Middle neck comfort and speed",
                    Exercises: new List<string>
                    {
                        "Three notes per string technique",
                        "Speed drill at 100-140 BPM",
                        "Smooth transitions"
                    }
                ),
                new(
                    Position: FretboardPosition.Position12,
                    FocusArea: "Higher register and extended range",
                    Exercises: new List<string>
                    {
                        "Octave jumps from lower positions",
                        "Full-neck visualization",
                        "Connecting all positions"
                    }
                )
            },
            ProgressionPath: "Master each position independently, then connect them with smooth transitions"
        );
    }

    /// <summary>
    /// Generate a comprehensive fretboard map showing a scale across the entire neck.
    /// </summary>
    public FretboardMap GenerateFretboardMap(string scaleName, string rootNote)
    {
        var notes = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var intervals = GetScaleIntervals(scaleName);

        var fretboard = new List<List<FretboardNote>>();

        // Generate 6 strings (standard tuning: E-A-D-G-B-E)
        var strings = new[] { "E", "A", "D", "G", "B", "E" };

        foreach (var openString in strings)
        {
            var stringNotes = new List<FretboardNote>();
            var startIndex = Array.IndexOf(notes, openString);

            for (int fret = 0; fret <= 24; fret++)
            {
                var noteIndex = (startIndex + fret) % 12;
                var noteName = notes[noteIndex];
                var isInScale = IsNoteInScale(noteName, rootNote, intervals);
                var isRoot = noteName == rootNote;

                stringNotes.Add(new FretboardNote(
                    Name: noteName,
                    Fret: fret,
                    IsInScale: isInScale,
                    IsRoot: isRoot,
                    Interval: GetIntervalName(noteName, rootNote, intervals)
                ));
            }

            fretboard.Add(stringNotes);
        }

        return new FretboardMap(
            ScaleName: $"{rootNote} {scaleName}",
            Fretboard: fretboard,
            Legend: "Root = R, Scale Notes = •, Non-Scale Notes = -",
            IntervalInfo: intervals
        );
    }

    // ────── Helpers ──────────────────────────────────────────────

    private ScaleShape? GenerateScaleShape(string scaleName, string rootNote, FretboardPosition position)
    {
        var positionNum = position switch
        {
            FretboardPosition.Position1 => 0,
            FretboardPosition.Position5 => 5,
            FretboardPosition.Position7 => 7,
            FretboardPosition.Position9 => 9,
            FretboardPosition.Position12 => 12,
            _ => 0
        };

        var pattern = GeneratePattern(scaleName, 7); // 7 notes for scale

        return new ScaleShape(
            Name: $"{rootNote} {scaleName} - Position {positionNum}",
            Position: position,
            Pattern: pattern,
            FretboardDiagram: GenerateDiagramAscii(pattern, positionNum),
            FingeringGuide: GenerateFingeringGuide(pattern, positionNum)
        );
    }

    private ArpeggioShape? GenerateArpeggioShape(string chordName, string rootNote, FretboardPosition position)
    {
        var positionNum = position switch
        {
            FretboardPosition.Position1 => 0,
            FretboardPosition.Position5 => 5,
            FretboardPosition.Position7 => 7,
            FretboardPosition.Position12 => 12,
            _ => 0
        };

        var notes = ExtractChordNotes(chordName, rootNote);
        var pattern = new List<string> { rootNote };
        pattern.AddRange(notes.Where(n => n != rootNote));

        return new ArpeggioShape(
            ChordName: $"{rootNote} {chordName}",
            Position: position,
            Pattern: pattern,
            SweepPattern: $"Sweep {positionNum}-{positionNum + 6} covering {pattern.Count} strings",
            FretboardDiagram: GenerateDiagramAscii(pattern, positionNum)
        );
    }

    private List<string> GetScaleIntervals(string scaleName)
    {
        return scaleName switch
        {
            "Major" => new List<string> { "R", "2", "3", "4", "5", "6", "7" },
            "Minor" or "Natural Minor" or "Aeolian" => new List<string> { "R", "2", "b3", "4", "5", "b6", "b7" },
            "Harmonic Minor" => new List<string> { "R", "2", "b3", "4", "5", "b6", "7" },
            "Dorian" => new List<string> { "R", "2", "b3", "4", "5", "6", "b7" },
            "Phrygian" => new List<string> { "R", "b2", "b3", "4", "5", "b6", "b7" },
            "Lydian" => new List<string> { "R", "2", "3", "#4", "5", "6", "7" },
            "Mixolydian" => new List<string> { "R", "2", "3", "4", "5", "6", "b7" },
            "Blues" => new List<string> { "R", "b3", "4", "b5", "5", "b7" },
            _ => new List<string> { "R", "2", "3", "4", "5", "6", "7" }
        };
    }

    private bool IsNoteInScale(string noteName, string rootNote, List<string> intervals)
    {
        var notes = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var rootIndex = Array.IndexOf(notes, rootNote);

        var semitoneIntervals = intervals.Select(i => GetSemitones(i)).ToList();
        var noteOffset = (Array.IndexOf(notes, noteName) - rootIndex + 12) % 12;

        return semitoneIntervals.Contains(noteOffset);
    }

    private string GetIntervalName(string noteName, string rootNote, List<string> intervals)
    {
        var notes = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var rootIndex = Array.IndexOf(notes, rootNote);
        var noteOffset = (Array.IndexOf(notes, noteName) - rootIndex + 12) % 12;

        var semitoneIntervals = intervals.Select(i => GetSemitones(i)).ToList();
        var idx = semitoneIntervals.IndexOf(noteOffset);

        return idx >= 0 ? intervals[idx] : "?";
    }

    private int GetSemitones(string interval)
    {
        return interval switch
        {
            "R" => 0, "2" => 2, "b3" => 3, "3" => 4,
            "4" => 5, "#4" => 6, "b5" => 6, "5" => 7,
            "b6" => 8, "6" => 9, "b7" => 10, "7" => 11,
            _ => 0
        };
    }

    private List<string> GeneratePattern(string scaleName, int noteCount)
    {
        var scales = new Dictionary<string, List<string>>
        {
            ["Major"] = new() { "1", "2", "3", "4", "5", "6", "7" },
            ["Minor"] = new() { "1", "2", "b3", "4", "5", "b6", "b7" },
            ["Blues"] = new() { "1", "b3", "4", "b5", "5", "b7" }
        };

        return scales.TryGetValue(scaleName, out var pattern)
            ? pattern
            : Enumerable.Range(1, noteCount).Select(n => n.ToString()).ToList();
    }

    private string GenerateDiagramAscii(List<string> pattern, int startFret)
    {
        // Simplified ASCII diagram
        return $"|--{string.Join("-", pattern.Take(4))}--| (Frets {startFret}-{startFret + 4})";
    }

    private string GenerateFingeringGuide(List<string> pattern, int startFret)
    {
        return $"Use fingers 1-2-3-4 for efficient movement starting at fret {startFret}";
    }

    private List<string> ExtractChordNotes(string chordName, string rootNote)
    {
        // Simplified chord note extraction
        return chordName switch
        {
            "Major" => new List<string> { rootNote, "E", "G" },
            "Minor" => new List<string> { rootNote, "Eb", "G" },
            "Dominant7" => new List<string> { rootNote, "E", "G", "Bb" },
            _ => new List<string> { rootNote }
        };
    }
}

/// <summary>
/// Single note on fretboard (DTO — not to be confused with GA.Domain.Core.Primitives.Notes.Note).
/// </summary>
public record FretboardNote(
    string Name,
    int Fret,
    bool IsInScale,
    bool IsRoot,
    string Interval);

/// <summary>
/// Complete fretboard visualization.
/// </summary>
public record FretboardMap(
    string ScaleName,
    List<List<FretboardNote>> Fretboard,
    string Legend,
    List<string> IntervalInfo);

/// <summary>
/// Beginner-friendly scale exploration guide.
/// </summary>
public record ScaleExplorationGuide(
    string ScaleName,
    string Description,
    List<PositionGuide> Positions,
    string ProgressionPath);

/// <summary>
/// Single position in exploration guide.
/// </summary>
public record PositionGuide(
    FretboardContextService.FretboardPosition Position,
    string FocusArea,
    List<string> Exercises);
