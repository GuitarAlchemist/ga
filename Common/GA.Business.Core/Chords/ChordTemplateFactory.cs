namespace GA.Business.Core.Chords;

using Atonal;
using Intervals;
using Intervals.Primitives;
using Notes;
using Tonal.Modes;
using Tonal.Modes.Diatonic;
using Tonal.Modes.Pentatonic;
using Tonal.Modes.Symmetric;
using Tonal.Primitives.Diatonic;
using Tonal.Primitives.Pentatonic;
using Tonal.Primitives.Symmetric;

/// <summary>
///     Factory for creating chord templates with comprehensive programmatic chord generation capabilities.
///     All chords are generated algorithmically by systematically iterating through all known scales and modes,
///     preserving complete intervallic and harmonic context from their parent scales.
///     NO CANNED INTERVAL SETS - everything is computed from scale relationships.
/// </summary>
public static class ChordTemplateFactory
{
    /// <summary>
    ///     BACKWARD COMPATIBILITY: Provides access to systematically generated chords.
    ///     This replaces the old hard-coded StandardChords property.
    ///     Use GenerateAllPossibleChords() or CreateTraditionalChordLibrary() instead.
    /// </summary>
    [Obsolete("Use GenerateAllPossibleChords() or CreateTraditionalChordLibrary() instead of StandardChords")]
    public static IEnumerable<ChordTemplate> StandardChords => CreateTraditionalChordLibrary();

    /// <summary>
    ///     Generates ALL possible chord templates by systematically iterating through:
    ///     1. All modal families (major, harmonic minor, melodic minor, symmetrical, etc.)
    ///     2. All modes within each family
    ///     3. All scale degrees within each mode
    ///     4. All stacking types (tertian, quartal, quintal, secundal)
    ///     5. All extensions (triad through 13th)
    ///     This is the master method that replaces any hard-coded chord libraries.
    /// </summary>
    public static IEnumerable<ChordTemplate> GenerateAllPossibleChords()
    {
        // 1. Generate from all modal families (systematic approach)
        foreach (var chord in GenerateFromAllModalFamilies())
        {
            yield return chord;
        }

        // 2. Generate from traditional scale modes (diatonic, harmonic minor, etc.)
        foreach (var chord in GenerateFromTraditionalScales())
        {
            yield return chord;
        }

        // 3. Generate from symmetrical scales
        foreach (var chord in GenerateFromSymmetricalScales())
        {
            yield return chord;
        }

        // 4. Generate from pentatonic scales
        foreach (var chord in GenerateFromPentatonicScales())
        {
            yield return chord;
        }
    }

    /// <summary>
    ///     Generates chords from all modal families systematically.
    ///     This covers ALL possible scale/mode combinations in the system.
    /// </summary>
    public static IEnumerable<ChordTemplate> GenerateFromAllModalFamilies()
    {
        // Until a generalized ModalFamily -> Modes factory exists, we aggregate from
        // all currently implemented modal families to ensure comprehensive coverage.

        // 1) Traditional diatonic families (major, natural minor, harmonic minor, melodic minor)
        foreach (var chord in GenerateFromTraditionalScales())
        {
            yield return chord;
        }

        // 2) Symmetrical scale families (whole tone, diminished, augmented)
        foreach (var chord in GenerateFromSymmetricalScales())
        {
            yield return chord;
        }

        // 3) Pentatonic families (major pentatonic, Hirajoshi, In Sen)
        foreach (var chord in GenerateFromPentatonicScales())
        {
            yield return chord;
        }
    }

    /// <summary>
    ///     Generates chords from traditional scale modes (major, natural minor, harmonic minor, melodic minor).
    ///     These are the most commonly used scales in Western music.
    /// </summary>
    private static IEnumerable<ChordTemplate> GenerateFromTraditionalScales()
    {
        // Major scale modes
        foreach (var degree in MajorScaleDegree.Items)
        {
            var mode = MajorScaleMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // Harmonic minor modes
        foreach (var degree in HarmonicMinorScaleDegree.Items)
        {
            var mode = HarmonicMinorMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // Melodic minor modes
        foreach (var degree in MelodicMinorScaleDegree.Items)
        {
            var mode = MelodicMinorMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // Natural minor modes
        foreach (var degree in NaturalMinorScaleDegree.Items)
        {
            var mode = NaturalMinorMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Generates chords from symmetrical scales (whole tone, diminished, augmented).
    ///     These scales have unique symmetrical properties that create interesting chord structures.
    /// </summary>
    private static IEnumerable<ChordTemplate> GenerateFromSymmetricalScales()
    {
        // Whole tone scales
        foreach (var degree in WholeToneScaleDegree.Items)
        {
            var mode = WholeToneScaleMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // Diminished scales
        foreach (var degree in DiminishedScaleDegree.Items)
        {
            var mode = DiminishedScaleMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // Augmented scales
        foreach (var degree in AugmentedScaleDegree.Items)
        {
            var mode = AugmentedScaleMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Generates chords from pentatonic scales (major pentatonic, minor pentatonic, and exotic pentatonics).
    /// </summary>
    private static IEnumerable<ChordTemplate> GenerateFromPentatonicScales()
    {
        // Major pentatonic modes
        foreach (var degree in MajorPentatonicScaleDegree.Items)
        {
            var mode = MajorPentatonicMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // Hirajoshi pentatonic modes
        foreach (var degree in HirajoshiScaleDegree.Items)
        {
            var mode = HirajoshiScaleMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }

        // In Sen pentatonic modes
        foreach (var degree in InSenScaleDegree.Items)
        {
            var mode = InSenScaleMode.Get(degree);
            foreach (var chord in GenerateFromScaleMode(mode))
            {
                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Generates ALL possible chords from a single scale mode by systematically exploring:
    ///     - All scale degrees
    ///     - All stacking types (tertian, quartal, quintal, secundal)
    ///     - All extensions (triad through 13th)
    ///     This is the core method that replaces hard-coded chord generation.
    /// </summary>
    public static IEnumerable<ChordTemplate> GenerateFromScaleMode(ScaleMode scaleMode)
    {
        var scaleLength = scaleMode.Notes.Count;

        // All possible stacking types
        var stackingTypes = new[]
        {
            ChordStackingType.Tertian,
            ChordStackingType.Quartal,
            ChordStackingType.Quintal,
            ChordStackingType.Secundal
        };

        // All possible extensions
        var extensions = new[]
        {
            ChordExtension.Triad,
            ChordExtension.Seventh,
            ChordExtension.Ninth,
            ChordExtension.Eleventh,
            ChordExtension.Thirteenth,
            ChordExtension.Add9,
            ChordExtension.Add11,
            ChordExtension.Sixth,
            ChordExtension.Sus2,
            ChordExtension.Sus4
        };

        // Generate chords for every combination
        for (var degree = 1; degree <= scaleLength; degree++)
        {
            foreach (var stackingType in stackingTypes)
            {
                foreach (var extension in extensions)
                {
                    // Skip combinations that don't make sense
                    if (!IsValidStackingExtensionCombination(stackingType, extension, scaleLength))
                    {
                        continue;
                    }

                    var formula = CreateModalChordFormula(scaleMode, degree, extension, stackingType);
                    if (formula.Intervals.Any()) // Only yield if we have intervals
                    {
                        yield return new ChordTemplate.TonalModal(formula, scaleMode, degree);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Determines if a stacking type and extension combination is valid for a given scale length.
    /// </summary>
    private static bool IsValidStackingExtensionCombination(ChordStackingType stackingType, ChordExtension extension,
        int scaleLength)
    {
        // Sus chords only make sense with tertian stacking
        if (extension is ChordExtension.Sus2 or ChordExtension.Sus4 && stackingType != ChordStackingType.Tertian)
        {
            return false;
        }

        // Don't generate extended chords beyond what the scale can support
        var maxIntervals = extension switch
        {
            ChordExtension.Triad => 2,
            ChordExtension.Seventh => 3,
            ChordExtension.Ninth => 4,
            ChordExtension.Eleventh => 5,
            ChordExtension.Thirteenth => 6,
            ChordExtension.Add9 => 3,
            ChordExtension.Add11 => 3,
            ChordExtension.Sixth => 3,
            ChordExtension.Sus2 => 2,
            ChordExtension.Sus4 => 2,
            _ => 2
        };

        return maxIntervals <= scaleLength - 1; // -1 because we don't count the root
    }

    /// <summary>
    ///     Creates chord templates for all degrees of a scale mode with specific extension and stacking type.
    ///     Preserves complete intervallic relationships and harmonic context from the parent scale.
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateModalChords(ScaleMode parentMode,
        ChordExtension extension = ChordExtension.Triad, ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        var scaleLength = parentMode.Notes.Count;

        for (var degree = 1; degree <= scaleLength; degree++)
        {
            var formula = CreateModalChordFormula(parentMode, degree, extension, stackingType);
            yield return new ChordTemplate.TonalModal(formula, parentMode, degree);
        }
    }

    /// <summary>
    ///     Creates chord templates for a specific scale mode (diatonic triads).
    ///     This method is kept for backward compatibility with existing tests.
    /// </summary>
    public static IReadOnlyList<ChordTemplate> CreateDiatonicChords(ScaleMode parentMode)
    {
        return CreateModalChords(parentMode).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Creates diatonic seventh chord templates for a scale mode.
    ///     This method is kept for backward compatibility with existing tests.
    /// </summary>
    public static IReadOnlyList<ChordTemplate> CreateDiatonicSevenths(ScaleMode parentMode)
    {
        return CreateModalChords(parentMode, ChordExtension.Seventh).ToList().AsReadOnly();
    }

    private static ChordFormula CreateModalChordFormula(ScaleMode parentMode, int degree, ChordExtension extension,
        ChordStackingType stackingType)
    {
        var scaleNotes = parentMode.Notes.ToList();
        var rootIndex = degree - 1;
        var intervals = new List<ChordFormulaInterval>();

        // Handle special chord types first
        if (extension == ChordExtension.Sus2)
        {
            intervals.AddRange(CreateSusChordIntervals(scaleNotes, rootIndex, 2)); // 2nd instead of 3rd
        }
        else if (extension == ChordExtension.Sus4)
        {
            intervals.AddRange(CreateSusChordIntervals(scaleNotes, rootIndex, 4)); // 4th instead of 3rd
        }
        else
        {
            // Regular stacked chord generation
            var stepSize = stackingType switch
            {
                ChordStackingType.Tertian => 2, // Skip one note (thirds)
                ChordStackingType.Quartal => 3, // Skip two notes (fourths)
                ChordStackingType.Quintal => 4, // Skip three notes (fifths)
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
                ChordExtension.Add9 => 3, // Triad + 9th (skip 7th)
                ChordExtension.Add11 => 3, // Triad + 11th (skip 7th, 9th)
                ChordExtension.Sixth => 3, // Triad + 6th
                _ => 2
            };

            // Generate base intervals
            for (var i = 1; i <= maxIntervals; i++)
            {
                var noteIndex = (rootIndex + i * stepSize) % scaleNotes.Count;
                var targetNote = scaleNotes[noteIndex];
                var rootNote = scaleNotes[rootIndex];

                var semitones = (targetNote.PitchClass.Value - rootNote.PitchClass.Value + 12) % 12;

                // Handle special extensions
                if (extension == ChordExtension.Add9 && i == 3)
                {
                    // For add9, skip the 7th and add the 9th
                    noteIndex = (rootIndex + 4 * stepSize) % scaleNotes.Count;
                    targetNote = scaleNotes[noteIndex];
                    semitones = (targetNote.PitchClass.Value - rootNote.PitchClass.Value + 12) % 12;
                }
                else if (extension == ChordExtension.Add11 && i == 3)
                {
                    // For add11, skip the 7th and 9th, add the 11th
                    noteIndex = (rootIndex + 5 * stepSize) % scaleNotes.Count;
                    targetNote = scaleNotes[noteIndex];
                    semitones = (targetNote.PitchClass.Value - rootNote.PitchClass.Value + 12) % 12;
                }
                else if (extension == ChordExtension.Sixth && i == 3)
                {
                    // For sixth chord, add the 6th degree instead of 7th
                    var sixthDegreeIndex = (rootIndex + 5) % scaleNotes.Count; // 6th degree
                    targetNote = scaleNotes[sixthDegreeIndex];
                    semitones = (targetNote.PitchClass.Value - rootNote.PitchClass.Value + 12) % 12;
                }

                var interval = new Interval.Chromatic(Semitones.FromValue(semitones));
                var function = DetermineChordFunction(semitones);

                intervals.Add(new ChordFormulaInterval(interval, function));
            }
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

    /// <summary>
    ///     Creates intervals for suspended chords (sus2, sus4)
    /// </summary>
    private static IEnumerable<ChordFormulaInterval> CreateSusChordIntervals(List<Note> scaleNotes, int rootIndex,
        int susInterval)
    {
        var rootNote = scaleNotes[rootIndex];
        var intervals = new List<ChordFormulaInterval>();

        // Add the suspended interval (2nd or 4th)
        var susNoteIndex = (rootIndex + susInterval - 1) % scaleNotes.Count;
        var susNote = scaleNotes[susNoteIndex];
        var susSemitones = (susNote.PitchClass.Value - rootNote.PitchClass.Value + 12) % 12;
        var susIntervalObj = new Interval.Chromatic(Semitones.FromValue(susSemitones));
        var susFunction = susInterval == 2 ? ChordFunction.Ninth : ChordFunction.Eleventh;
        intervals.Add(new ChordFormulaInterval(susIntervalObj, susFunction));

        // Add the fifth
        var fifthNoteIndex = (rootIndex + 4) % scaleNotes.Count;
        var fifthNote = scaleNotes[fifthNoteIndex];
        var fifthSemitones = (fifthNote.PitchClass.Value - rootNote.PitchClass.Value + 12) % 12;
        var fifthInterval = new Interval.Chromatic(Semitones.FromValue(fifthSemitones));
        intervals.Add(new ChordFormulaInterval(fifthInterval, ChordFunction.Fifth));

        return intervals;
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

    /// <summary>
    ///     Gets chord templates by structural characteristics (quality, extension, stacking type).
    ///     This is the proper way to find chords - by their musical properties, not arbitrary names.
    /// </summary>
    public static IEnumerable<ChordTemplate> GetChordsByCharacteristics(
        ChordQuality? quality = null,
        ChordExtension? extension = null,
        ChordStackingType? stackingType = null,
        int? noteCount = null)
    {
        var allChords = GenerateAllPossibleChords();

        return allChords.Where(chord =>
            (quality == null || chord.Quality == quality) &&
            (extension == null || chord.Extension == extension) &&
            (stackingType == null || chord.StackingType == stackingType) &&
            (noteCount == null || chord.NoteCount == noteCount));
    }

    /// <summary>
    ///     Gets chord templates by interval pattern (semitone distances from root).
    ///     This allows finding chords by their actual intervallic structure.
    /// </summary>
    public static IEnumerable<ChordTemplate> GetChordsByIntervalPattern(params int[] semitones)
    {
        var targetPattern = semitones.OrderBy(s => s).ToArray();
        var allChords = GenerateAllPossibleChords();

        return allChords.Where(chord =>
        {
            var chordPattern = chord.Intervals
                .Select(i => i.Interval.Semitones.Value)
                .OrderBy(s => s)
                .ToArray();

            return chordPattern.SequenceEqual(targetPattern);
        });
    }

    /// <summary>
    ///     Creates comprehensive chord libraries using systematic generation.
    ///     This replaces any hard-coded chord libraries with computed results.
    /// </summary>
    public static IEnumerable<ChordTemplate> CreateTraditionalChordLibrary()
    {
        // Generate from traditional scales only (most commonly used)
        return GenerateFromTraditionalScales();
    }


    /// <summary>
    ///     Creates a chord template from a pitch class set with theoretical analysis
    /// </summary>
    public static ChordTemplate FromPitchClassSet(PitchClassSet pitchClassSet, string name)
    {
        return ChordTemplate.Analytical.FromPitchClassSet(pitchClassSet, name);
    }
}
