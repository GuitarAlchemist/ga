namespace GA.Business.Core.Chords;

using Intervals;
using Tonal.Modes;
using System.Collections.Concurrent;

/// <summary>
/// Factory for creating chord templates with comprehensive chord generation capabilities
/// </summary>
public static class ChordTemplateFactory
{
    private static readonly Lazy<ConcurrentDictionary<string, ChordTemplate>> _standardChords = 
        new(BuildStandardChords);

    /// <summary>
    /// Gets all standard chord templates
    /// </summary>
    public static IReadOnlyDictionary<string, ChordTemplate> StandardChords => _standardChords.Value;

    /// <summary>
    /// Creates a chord template from interval specifications
    /// </summary>
    public static ChordTemplate Create(string name, params (int semitones, ChordFunction function)[] intervals)
    {
        var chordIntervals = intervals.Select(spec => 
            new ChordFormulaInterval(Interval.FromSemitones(spec.semitones), spec.function));
        var formula = new ChordFormula(name, chordIntervals);
        return new ChordTemplate(formula);
    }

    /// <summary>
    /// Creates a chord template from semitone intervals (for convenience)
    /// </summary>
    public static ChordTemplate FromSemitones(string name, params int[] semitones)
    {
        var intervals = semitones.Select((s, i) => 
        {
            var interval = Interval.FromSemitones(s);
            var function = DetermineChordFunction(s);
            return new ChordFormulaInterval(interval, function);
        });
        
        var formula = new ChordFormula(name, intervals);
        return new ChordTemplate(formula);
    }

    /// <summary>
    /// Gets a standard chord template by name
    /// </summary>
    public static ChordTemplate? GetStandardChord(string name)
    {
        return _standardChords.Value.TryGetValue(name, out var chord) ? chord : null;
    }

    /// <summary>
    /// Gets count of available standard chords
    /// </summary>
    public static int StandardChordCount => StandardChords.Count;

    /// <summary>
    /// Creates chord templates for all degrees of a scale mode with specific extension and stacking type
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateModalChords(ScaleMode parentMode, ChordExtension extension = ChordExtension.Triad, ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        var scaleLength = parentMode.Notes.Count;

        for (var degree = 1; degree <= scaleLength; degree++)
        {
            var formula = CreateModalChordFormula(parentMode, degree, extension, stackingType);
            yield return new ChordTemplate(formula);
        }
    }

    /// <summary>
    /// Creates ALL possible chord templates for a scale mode - comprehensive generation
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateAllModalChords(ScaleMode parentMode)
    {
        var scaleLength = parentMode.Notes.Count;
        var allExtensions = Enum.GetValues<ChordExtension>();
        var allStackingTypes = Enum.GetValues<ChordStackingType>();

        for (var degree = 1; degree <= scaleLength; degree++)
        {
            foreach (var extension in allExtensions)
            {
                foreach (var stackingType in allStackingTypes)
                {
                    var formula = CreateModalChordFormula(parentMode, degree, extension, stackingType);
                    yield return new ChordTemplate(formula);
                }
            }
        }
    }

    /// <summary>
    /// Creates comprehensive chord templates for a scale mode with all extensions for a specific stacking type
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateModalChordsAllExtensions(ScaleMode parentMode, ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        var scaleLength = parentMode.Notes.Count;
        var allExtensions = Enum.GetValues<ChordExtension>();

        for (var degree = 1; degree <= scaleLength; degree++)
        {
            foreach (var extension in allExtensions)
            {
                var formula = CreateModalChordFormula(parentMode, degree, extension, stackingType);
                yield return new ChordTemplate(formula);
            }
        }
    }

    /// <summary>
    /// Creates quartal chord templates for a scale mode
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateQuartalChords(ScaleMode parentMode, ChordExtension extension = ChordExtension.Triad)
    {
        return CreateModalChords(parentMode, extension, ChordStackingType.Quartal);
    }

    /// <summary>
    /// Creates quintal chord templates for a scale mode
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateQuintalChords(ScaleMode parentMode, ChordExtension extension = ChordExtension.Triad)
    {
        return CreateModalChords(parentMode, extension, ChordStackingType.Quintal);
    }

    /// <summary>
    /// Creates secundal chord templates for a scale mode
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateSecundalChords(ScaleMode parentMode, ChordExtension extension = ChordExtension.Triad)
    {
        return CreateModalChords(parentMode, extension, ChordStackingType.Secundal);
    }

    /// <summary>
    /// Creates chord templates for a specific scale mode (diatonic triads)
    /// </summary>
    public static IReadOnlyList<ChordTemplate> CreateDiatonicChords(ScaleMode parentMode)
    {
        return CreateModalChords(parentMode, ChordExtension.Triad, ChordStackingType.Tertian).ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates diatonic seventh chord templates for a scale mode
    /// </summary>
    public static IReadOnlyList<ChordTemplate> CreateDiatonicSevenths(ScaleMode parentMode)
    {
        return CreateModalChords(parentMode, ChordExtension.Seventh, ChordStackingType.Tertian).ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates extended chord templates (9th, 11th, 13th) for a specific scale mode
    /// </summary>
    public static IReadOnlyList<ChordTemplate> CreateDiatonicExtendedChords(ScaleMode parentMode, ChordExtension extension)
    {
        return CreateModalChords(parentMode, extension, ChordStackingType.Tertian).ToList().AsReadOnly();
    }

    private static ChordFormula CreateModalChordFormula(ScaleMode parentMode, int degree, ChordExtension extension, ChordStackingType stackingType)
    {
        var scaleNotes = parentMode.Notes.ToList();
        var rootIndex = degree - 1;
        var intervals = new List<ChordFormulaInterval>();

        var stepSize = stackingType switch
        {
            ChordStackingType.Tertian => 2,  // Skip one note (thirds)
            ChordStackingType.Quartal => 3,  // Skip two notes (fourths)
            ChordStackingType.Quintal => 4,  // Skip three notes (fifths)
            ChordStackingType.Secundal => 1, // Adjacent notes (seconds)
            _ => 2 // Default to tertian
        };

        var maxIntervals = extension switch
        {
            ChordExtension.Triad => 2,
            ChordExtension.Seventh => 3,
            ChordExtension.Ninth => 4,
            ChordExtension.Eleventh => 5,
            ChordExtension.Thirteenth => 6,
            _ => 2
        };

        for (int i = 1; i <= maxIntervals; i++)
        {
            var noteIndex = (rootIndex + i * stepSize) % scaleNotes.Count;
            var targetNote = scaleNotes[noteIndex];
            var rootNote = scaleNotes[rootIndex];
            
            var semitones = (targetNote.MidiNote - rootNote.MidiNote) % 12;
            var interval = Interval.FromSemitones(semitones);
            var function = DetermineChordFunction(semitones);
            
            intervals.Add(new ChordFormulaInterval(interval, function));
        }

        var stackingSuffix = stackingType switch
        {
            ChordStackingType.Quartal => " (4ths)",
            ChordStackingType.Quintal => " (5ths)",
            ChordStackingType.Secundal => " (2nds)",
            _ => ""
        };

        var name = $"{parentMode.Name} Degree{degree} {extension}{stackingSuffix}";
        return new ChordFormula(name, intervals, stackingType);
    }

    private static ChordFunction DetermineChordFunction(int semitones)
    {
        return semitones switch
        {
            2 or 14 => ChordFunction.Ninth,
            3 or 4 => ChordFunction.Third,
            5 or 17 => ChordFunction.Eleventh,
            7 => ChordFunction.Fifth,
            9 or 21 => ChordFunction.Thirteenth,
            10 or 11 => ChordFunction.Seventh,
            _ => ChordFunction.Root
        };
    }

    private static ConcurrentDictionary<string, ChordTemplate> BuildStandardChords()
    {
        var chords = new ConcurrentDictionary<string, ChordTemplate>();

        // Basic triads
        TryAddChord("Major", 4, 7);
        TryAddChord("Minor", 3, 7);
        TryAddChord("Diminished", 3, 6);
        TryAddChord("Augmented", 4, 8);
        TryAddChord("Sus2", 2, 7);
        TryAddChord("Sus4", 5, 7);

        // Seventh chords
        TryAddChord("Major7", 4, 7, 11);
        TryAddChord("Minor7", 3, 7, 10);
        TryAddChord("Dominant7", 4, 7, 10);
        TryAddChord("Diminished7", 3, 6, 9);
        TryAddChord("HalfDiminished7", 3, 6, 10);
        TryAddChord("Augmented7", 4, 8, 10);
        TryAddChord("AugmentedMajor7", 4, 8, 11);

        // Extended chords
        TryAddChord("Major9", 4, 7, 11, 14);
        TryAddChord("Minor9", 3, 7, 10, 14);
        TryAddChord("Dominant9", 4, 7, 10, 14);
        TryAddChord("Major11", 4, 7, 11, 14, 17);
        TryAddChord("Minor11", 3, 7, 10, 14, 17);
        TryAddChord("Dominant11", 4, 7, 10, 14, 17);
        TryAddChord("Major13", 4, 7, 11, 14, 21);
        TryAddChord("Minor13", 3, 7, 10, 14, 21);
        TryAddChord("Dominant13", 4, 7, 10, 14, 21);

        // Add chords
        TryAddChord("Add9", 4, 7, 14);
        TryAddChord("MinorAdd9", 3, 7, 14);
        TryAddChord("Add11", 4, 7, 17);
        TryAddChord("MinorAdd11", 3, 7, 17);

        // Sixth chords
        TryAddChord("Sixth", 4, 7, 9);
        TryAddChord("MinorSixth", 3, 7, 9);
        TryAddChord("SixNine", 4, 7, 9, 14);
        TryAddChord("MinorSixNine", 3, 7, 9, 14);

        // Altered dominants
        TryAddChord("Dominant7b5", 4, 6, 10);
        TryAddChord("Dominant7#5", 4, 8, 10);
        TryAddChord("Dominant7b9", 4, 7, 10, 13);
        TryAddChord("Dominant7#9", 4, 7, 10, 15);
        TryAddChord("Dominant7#11", 4, 7, 10, 18);
        TryAddChord("Dominant7b13", 4, 7, 10, 20);
        TryAddChord("AlteredDominant", 4, 6, 10, 13, 15, 20);

        return chords;

        void TryAddChord(string name, params int[] semitones)
        {
            if (!chords.ContainsKey(name))
            {
                var chord = FromSemitones(name, semitones);
                chords[name] = chord;
            }
        }
    }
}
