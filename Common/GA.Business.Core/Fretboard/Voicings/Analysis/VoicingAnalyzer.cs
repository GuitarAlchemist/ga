namespace GA.Business.Core.Fretboard.Voicings.Analysis;

using Core;
using Atonal;
using Chords;
using Primitives;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Notes.Primitives;
using Tonal;
using Generation;

/// <summary>
/// Provides comprehensive musical analysis for guitar voicings
/// </summary>
public static class VoicingAnalyzer
{
    private static readonly IReadOnlyDictionary<string, ModeFamilyMetadata> ModeMetadataMap = BuildModeMetadata();

    /// <summary>
    /// Analyzes a voicing and returns comprehensive musical information
    /// </summary>
    public static MusicalVoicingAnalysis Analyze(Voicing voicing)
    {
        return AnalyzeEnhanced(new DecomposedVoicing(voicing, null!, null, null));
    }

    /// <summary>
    /// Analyzes a decomposed voicing with equivalence group information
    /// </summary>
    public static MusicalVoicingAnalysis AnalyzeEnhanced(DecomposedVoicing decomposedVoicing)
    {
        var voicing = decomposedVoicing.Voicing;

        // Get MIDI notes (already in the voicing)
        var midiNotes = voicing.Notes;

        // Convert to pitch classes
        var pitchClasses = midiNotes.Select(n => n.PitchClass).Distinct().ToList();
        var pitchClassSet = new PitchClassSet(pitchClasses);

        // Get bass note (lowest MIDI note)
        var bassNote = midiNotes.Length > 0 ? midiNotes[0].PitchClass : (PitchClass?)null;

        // Identify chord
        var chordIdentification = IdentifyChord(pitchClassSet, pitchClasses, bassNote);

        // Detect voicing characteristics
        var voicingCharacteristics = AnalyzeVoicingCharacteristics(voicing, chordIdentification);

        // Detect mode
        var modeInfo = DetectMode(pitchClassSet);

        // Detect chromatic notes (if key context available)
        var chromaticNotes = chordIdentification.ClosestKey != null
            ? IdentifyChromaticNotes(pitchClassSet, chordIdentification.ClosestKey)
            : null;

        // Detect symmetrical scales
        var symmetricalInfo = DetectSymmetricalScales(pitchClassSet);

        // Analyze intervallic content
        var intervallicInfo = AnalyzeIntervallic(pitchClassSet);

        // Extract equivalence information
        var equivalenceInfo = ExtractEquivalenceInfo(decomposedVoicing);

        // Extract physical layout
        var physicalLayout = ExtractPhysicalLayout(voicing);

        // Calculate playability
        var playabilityInfo = CalculatePlayability(physicalLayout, voicing);

        // Generate semantic tags
        var semanticTags = GenerateSemanticTags(
            chordIdentification,
            voicingCharacteristics,
            modeInfo,
            physicalLayout,
            playabilityInfo);

        return new MusicalVoicingAnalysis(
            midiNotes,
            pitchClassSet,
            chordIdentification,
            voicingCharacteristics,
            modeInfo,
            chromaticNotes,
            symmetricalInfo,
            intervallicInfo,
            equivalenceInfo,
            physicalLayout,
            playabilityInfo,
            semanticTags
        );
    }

    private static ChordIdentification IdentifyChord(PitchClassSet pitchClassSet, List<PitchClass> pitchClasses, PitchClass? bassNote)
    {
        if (pitchClasses.Count == 0)
        {
            return new ChordIdentification(null, null, null, null, null, null, false, null);
        }

        // Try to find matching chord templates
        var root = pitchClasses[0]; // Assume lowest pitch class is root

        try
        {
            // Create a chord template from the pitch class set
            var template = ChordTemplateFactory.FromPitchClassSet(pitchClassSet, "Analysis");

            // Generate comprehensive names using the unified naming service (with bass note for slash chord detection)
            var bestName = ChordTemplateNamingService.GetBestChordName(template, root, bassNote);
            var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(template, root, bassNote);

            // Try to find the closest key
            var closestKey = pitchClassSet.ClosestDiatonicKey;

            // Get scale degree in the closest key
            var scaleDegree = KeyAwareChordNamingService.GetScaleDegree(root, closestKey);
            var romanNumeral = GenerateRomanNumeral(scaleDegree, template.Quality, template.Extension);

            // Check if naturally occurring in key
            var isNaturallyOccurring = KeyAwareChordNamingService.IsNaturallyOccurringInKey(template, root, closestKey);

            // Get intervals (theoretical and actual)
            var intervals = GetChordIntervals(template, pitchClasses);

            // Detect slash chord
            var slashChordInfo = bassNote.HasValue && bassNote.Value != root
                ? DetectSlashChord(template, root, bassNote.Value)
                : null;

            return new ChordIdentification(
                bestName,
                comprehensive.Primary,
                closestKey,
                romanNumeral,
                $"{romanNumeral} in {closestKey}",
                slashChordInfo,
                isNaturallyOccurring,
                intervals
        );
        }
        catch
        {
            // If chord identification fails, return atonal analysis
            return new ChordIdentification(
                $"Set {pitchClassSet.Id.Value}",
                null,
                null,
                null,
                "Atonal/Ambiguous",
                null,
                false,
                null
        );
        }
    }

    private static string GenerateRomanNumeral(int scaleDegree, ChordQuality quality, ChordExtension extension)
    {
        var numeral = scaleDegree switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            _ => scaleDegree.ToString()
        };

        // Use lowercase for minor/diminished
        if (quality == ChordQuality.Minor || quality == ChordQuality.Diminished)
        {
            numeral = numeral.ToLower();
        }

        // Add quality suffix
        var suffix = quality switch
        {
            ChordQuality.Diminished => "°",
            ChordQuality.Augmented => "+",
            _ => ""
        };

        // Add extension suffix
        var extSuffix = extension switch
        {
            ChordExtension.Seventh => "7",
            ChordExtension.Ninth => "9",
            ChordExtension.Eleventh => "11",
            ChordExtension.Thirteenth => "13",
            ChordExtension.Add9 => "add9",
            ChordExtension.Add11 => "add11",
            ChordExtension.Sixth => "6",
            ChordExtension.SixNine => "6/9",
            ChordExtension.Sus2 => "sus2",
            ChordExtension.Sus4 => "sus4",
            _ => ""
        };

        return numeral + suffix + extSuffix;
    }

    private static SlashChordInfo DetectSlashChord(ChordTemplate template, PitchClass root, PitchClass bassNote)
    {
        var analysis = SlashChordNamingService.AnalyzeSlashChord(template, root, bassNote);
        return new SlashChordInfo(
            analysis.SlashNotation,
            analysis.Type.ToString(),
            analysis.IsCommonInversion
        );
    }

    private static VoicingCharacteristics AnalyzeVoicingCharacteristics(Voicing voicing, ChordIdentification chordId)
    {
        var midiNotes = voicing.Notes;
        var pitchClasses = midiNotes.Select(n => n.PitchClass).Distinct().ToList();

        // Detect open vs closed voicing (span > octave)
        var lowestNote = midiNotes[0].Value;
        var highestNote = midiNotes[^1].Value;
        var span = highestNote - lowestNote;
        var isOpenVoicing = span > 12;

        // Detect rootless voicing
        var root = pitchClasses[0];
        var bassNote = midiNotes[0].PitchClass;
        var isRootless = bassNote != root;

        // Detect drop voicings (simplified - would need more sophisticated analysis)
        var dropVoicing = DetectDropVoicing(midiNotes);

        // Detect quartal/quintal harmony
        var isQuartal = chordId.Intervals != null &&
                       chordId.Intervals.Actual.Any(i => i.Contains("5 semitones") || i.Contains("7 semitones"));

        // Detect suspended chords
        var isSuspended = chordId.ChordName?.Contains("sus") ?? false;

        // Detect added tone chords
        var hasAddedTones = chordId.ChordName?.Contains("add") ?? false;

        var features = new List<string>();
        if (isOpenVoicing) features.Add("Open voicing");
        else features.Add("Closed voicing");

        if (isRootless) features.Add("Rootless");
        if (dropVoicing != null) features.Add(dropVoicing);
        if (isQuartal) features.Add("Quartal/Quintal harmony");
        if (isSuspended) features.Add("Suspended");
        if (hasAddedTones) features.Add("Added tones");

        return new VoicingCharacteristics(
            isOpenVoicing,
            isRootless,
            dropVoicing,
            span,
            features
        );
    }

    private static string? DetectDropVoicing(MidiNote[] midiNotes)
    {
        if (midiNotes.Length < 4) return null;

        // Simplified drop voicing detection
        // Drop-2: second voice from top is dropped an octave
        // Drop-3: third voice from top is dropped an octave
        // This is a basic heuristic - real detection would be more sophisticated

        var intervals = new List<int>();
        for (int i = 1; i < midiNotes.Length; i++)
        {
            intervals.Add(midiNotes[i].Value - midiNotes[i - 1].Value);
        }

        // Look for characteristic interval patterns
        // Drop-2 typically has a large interval (>5 semitones) between bottom two notes
        if (intervals.Count >= 3 && intervals[0] > 5 && intervals[1] <= 5 && intervals[2] <= 5)
        {
            return "Drop-2";
        }

        // Drop-3 typically has a large interval between 1st and 2nd notes from bottom
        if (intervals.Count >= 3 && intervals[0] <= 5 && intervals[1] > 5 && intervals[2] <= 5)
        {
            return "Drop-3";
        }

        return null;
    }

    private static ModeInfo? DetectMode(PitchClassSet pitchClassSet)
    {
        var icv = pitchClassSet.IntervalClassVector.ToString();

        if (ModeMetadataMap.TryGetValue(icv, out var metadata))
        {
            var modeIndexFromMetadata = metadata.ModeIds.FindIndex(id => id.Equals(pitchClassSet.Id));
            if (modeIndexFromMetadata >= 0)
            {
                return new ModeInfo(
                    metadata.ModeNames[modeIndexFromMetadata],
                    metadata.NoteCount,
                    modeIndexFromMetadata + 1,
                    metadata.FamilyName);
            }
        }

        var modalFamily = pitchClassSet.ModalFamily;
        if (modalFamily == null)
        {
            return null;
        }

        var modes = modalFamily.Modes.ToList();
        var modeIndex = modes.IndexOf(pitchClassSet);
        if (modeIndex < 0)
        {
            return null;
        }

        var (familyName, modeName) = ResolveModeNameFromIntervalVector(icv, modeIndex, null);

        return modeName != null
            ? new ModeInfo(modeName, modalFamily.NoteCount, modeIndex + 1, familyName)
            : null;
    }

    private static (string? FamilyName, string? ModeName) ResolveModeNameFromIntervalVector(
        string intervalClassVector,
        int modeIndex,
        string? preferredFamilyName)
    {
        var degree = modeIndex + 1;
        string? familyName = preferredFamilyName;
        string? modeName = null;

        if (intervalClassVector == "<2 5 4 3 6 1>")
        {
            familyName ??= "Major Scale Family";
            modeName = degree switch
            {
                1 => "Ionian (Major)",
                2 => "Dorian",
                3 => "Phrygian",
                4 => "Lydian",
                5 => "Mixolydian",
                6 => "Aeolian (Natural Minor)",
                7 => "Locrian",
                _ => null
            };
        }
        else if (intervalClassVector == "<2 4 3 5 4 3>")
        {
            familyName ??= "Harmonic Minor Family";
            modeName = degree switch
            {
                1 => "Harmonic Minor",
                2 => "Locrian ♮6",
                3 => "Ionian ♯5 (Ionian Augmented)",
                4 => "Dorian ♯4 (Romanian)",
                5 => "Phrygian Dominant",
                6 => "Lydian ♯2",
                7 => "Ultralocrian (Superlocrian ♭♭7)",
                _ => null
            };
        }
        else if (intervalClassVector == "<2 4 5 3 6 2>")
        {
            familyName ??= "Melodic Minor Family";
            modeName = degree switch
            {
                1 => "Melodic Minor (Jazz Minor)",
                2 => "Dorian ♭2 (Phrygian ♮6)",
                3 => "Lydian Augmented (Lydian ♯5)",
                4 => "Lydian Dominant (Overtone)",
                5 => "Mixolydian ♭6 (Aeolian Dominant)",
                6 => "Locrian ♮2 (Half Diminished)",
                7 => "Altered (Super Locrian)",
                _ => null
            };
        }
        else if (intervalClassVector == "<2 4 4 3 5 3>")
        {
            familyName ??= "Harmonic Major Family";
            modeName = degree switch
            {
                1 => "Harmonic Major",
                2 => "Dorian ♭5",
                3 => "Phrygian ♭4",
                4 => "Lydian ♭3",
                5 => "Mixolydian ♭2",
                6 => "Lydian Augmented ♯2",
                7 => "Locrian ♭♭7",
                _ => null
            };
        }
        else if (intervalClassVector == "<1 5 2 5 2 6>")
        {
            familyName ??= "Double Harmonic Family";
            modeName = degree switch
            {
                1 => "Double Harmonic (Byzantine)",
                2 => "Lydian ♯2 ♯6",
                3 => "Ultraphrygian",
                4 => "Hungarian Minor",
                5 => "Oriental",
                6 => "Ionian Augmented ♯2",
                7 => "Locrian ♭♭3 ♭♭7",
                _ => null
            };
        }

        return (familyName, modeName);
    }

    private static List<string>? IdentifyChromaticNotes(PitchClassSet pitchClassSet, Key key)
    {
        var keyPitchClasses = key.PitchClassSet.ToHashSet();
        var chromaticPitchClasses = pitchClassSet
            .Where(pc => !keyPitchClasses.Contains(pc))
            .ToList();

        if (chromaticPitchClasses.Count == 0)
        {
            return null;
        }

        return [.. chromaticPitchClasses.Select(pc => pc.ToSharpNote().ToString())];
    }

    private static ChordIntervals? GetChordIntervals(ChordTemplate template, List<PitchClass> pitchClasses)
    {
        if (template.Formula.Intervals.Count == 0)
        {
            return null;
        }

        // Get theoretical intervals from the template
        var theoretical = template.Formula.Intervals
            .Select(i => $"{i.Function}: {i.Interval}")
            .ToList();

        // Get actual intervals from the pitch classes
        var root = pitchClasses[0];
        var actual = pitchClasses
            .Skip(1)
            .Select(pc =>
            {
                var semitones = (pc.Value - root.Value + 12) % 12;
                return $"{semitones} semitones";
            })
            .ToList();

        return new ChordIntervals(theoretical, actual);
    }

    private static SymmetricalScaleInfo? DetectSymmetricalScales(PitchClassSet pitchClassSet)
    {
        // Check for diminished (octatonic) - 8 notes, alternating half/whole steps
        if (pitchClassSet.Cardinality.Value == 8)
        {
            var icv = pitchClassSet.IntervalClassVector;
            // Diminished scale has specific ICV pattern
            if (icv.ToString() == "[448444]") // Octatonic ICV
            {
                return new SymmetricalScaleInfo(
                    "Diminished (Octatonic)",
                    GetDiminishedRoots(pitchClassSet),
                    "Symmetrical scale with 3 possible roots"
                );
            }
        }

        // Check for whole tone - 6 notes, all whole steps
        if (pitchClassSet.Cardinality.Value == 6)
        {
            var icv = pitchClassSet.IntervalClassVector;
            if (icv.ToString() == "[060603]") // Whole tone ICV
            {
                return new SymmetricalScaleInfo(
                    "Whole Tone",
                    GetWholeToneRoots(pitchClassSet),
                    "Symmetrical scale with 6 possible roots"
                );
            }
        }

        // Check for augmented triad - 3 notes, all major thirds
        if (pitchClassSet.Cardinality.Value == 3)
        {
            var icv = pitchClassSet.IntervalClassVector;
            if (icv.ToString() == "[003000]") // Augmented triad ICV
            {
                return new SymmetricalScaleInfo(
                    "Augmented Triad",
                    [.. pitchClassSet.ToList().Select(pc => pc.ToString())],
                    "Symmetrical chord with 3 enharmonic roots"
                );
            }
        }

        // Check for diminished 7th - 4 notes, all minor thirds
        if (pitchClassSet.Cardinality.Value == 4)
        {
            var icv = pitchClassSet.IntervalClassVector;
            if (icv.ToString() == "[004002]") // Diminished 7th ICV
            {
                return new SymmetricalScaleInfo(
                    "Diminished 7th",
                    [.. pitchClassSet.ToList().Select(pc => pc.ToString())],
                    "Symmetrical chord with 4 enharmonic roots"
                );
            }
        }

        return null;
    }

    private static List<string> GetDiminishedRoots(PitchClassSet pitchClassSet)
    {
        // Diminished scale has 3 possible roots (every minor third)
        var roots = new List<string>();
        var pitchClasses = pitchClassSet.ToList();
        for (var i = 0; i < Math.Min(3, pitchClasses.Count); i += 3)
        {
            roots.Add(pitchClasses[i].ToString());
        }
        return roots;
    }

    private static List<string> GetWholeToneRoots(PitchClassSet pitchClassSet)
    {
        // Whole tone scale - all notes are potential roots
        return [.. pitchClassSet.ToList().Select(pc => pc.ToString())];
    }

    private static IntervallicInfo AnalyzeIntervallic(PitchClassSet pitchClassSet)
    {
        var icv = pitchClassSet.IntervalClassVector;
        var features = new List<string>();
        var semitoneCount = icv[IntervalClass.Hemitone];
        var wholeToneCount = icv[IntervalClass.Tone];
        var quartalCount = icv[IntervalClass.FromValue(5)];
        var tritoneCount = icv[IntervalClass.Tritone];

        // Check for tritone (interval class 6)
        if (tritoneCount > 0)
        {
            features.Add($"Contains {tritoneCount} tritone(s)");
        }

        // Check for quartal harmony (perfect fourths - interval class 5)
        if (quartalCount >= 2)
        {
            features.Add("Quartal harmony");
        }

        // Check for quintal harmony (perfect fifths - same interval class)
        if (quartalCount >= 2)
        {
            features.Add("Quintal harmony");
        }

        // Check for cluster (many semitones)
        if (semitoneCount >= 2)
        {
            features.Add($"Cluster ({semitoneCount} semitones)");
        }

        // Check for whole tone content
        if (wholeToneCount >= 3)
        {
            features.Add("Whole tone content");
        }

        return new IntervallicInfo(
            icv.ToString(),
            features
        );
    }

    private static EquivalenceInfo? ExtractEquivalenceInfo(DecomposedVoicing decomposedVoicing)
    {
        if (decomposedVoicing.Vector == null)
        {
            return null;
        }

        var isPrimeForm = decomposedVoicing.PrimeForm != null;
        var translationOffset = decomposedVoicing.Translation?.Increment ?? 0;

        // Get prime form ID
        var primeFormId = isPrimeForm
            ? decomposedVoicing.PrimeForm!.ToString()
            : decomposedVoicing.Vector.ToString();

        // Get equivalence class size from prime form if available
        var equivalenceClassSize = isPrimeForm
            ? decomposedVoicing.PrimeForm!.Translations.Count + 1 // +1 for the prime form itself
            : 1; // If we don't have the prime form, we can't know the class size

        return new EquivalenceInfo(
            primeFormId,
            isPrimeForm,
            translationOffset,
            equivalenceClassSize
        );
    }

    private static PhysicalLayout ExtractPhysicalLayout(Voicing voicing)
    {
        var positions = voicing.Positions;
        var fretPositions = new int[positions.Length];
        var stringsUsed = new List<int>();
        var mutedStrings = new List<int>();
        var openStrings = new List<int>();
        var minFret = int.MaxValue;
        var maxFret = 0;

        for (var i = 0; i < positions.Length; i++)
        {
            var stringNum = i + 1; // 1-based string numbering

            switch (positions[i])
            {
                case Position.Played played:
                    var fret = played.Location.Fret.Value;
                    fretPositions[i] = fret;
                    stringsUsed.Add(stringNum);

                    if (fret == 0)
                    {
                        openStrings.Add(stringNum);
                    }
                    else
                    {
                        if (fret < minFret) minFret = fret;
                        if (fret > maxFret) maxFret = fret;
                    }
                    break;

                case Position.Muted:
                    fretPositions[i] = -1;
                    mutedStrings.Add(stringNum);
                    break;
            }
        }

        // Determine hand position
        var handPosition = maxFret switch
        {
            <= 4 => "Open Position",
            <= 7 => "Low Position",
            <= 12 => "Middle Position",
            _ => "Upper Position"
        };

        // Handle case where all strings are open or muted
        if (minFret == int.MaxValue) minFret = 0;

        return new PhysicalLayout(
            fretPositions,
            [.. stringsUsed],
            [.. mutedStrings],
            [.. openStrings],
            minFret,
            maxFret,
            handPosition
        );
    }

    private static PlayabilityInfo CalculatePlayability(PhysicalLayout layout, Voicing voicing)
    {
        // Calculate hand stretch (fret span)
        var handStretch = layout.MaxFret - layout.MinFret;

        // Detect barre requirement (same fret on multiple adjacent strings)
        var barreRequired = DetectBarreRequirement(layout.FretPositions);

        // Estimate minimum fingers needed
        var uniqueFrets = layout.FretPositions.Where(f => f > 0).Distinct().Count();
        var minimumFingers = Math.Min(uniqueFrets + (barreRequired ? 0 : 0), 4);

        // Determine difficulty
        var difficulty = CalculateDifficulty(handStretch, barreRequired, layout.OpenStrings.Length);

        // Detect CAGED shape (simplified - would need more sophisticated analysis)
        var cagedShape = DetectCagedShape(layout, voicing);

        return new PlayabilityInfo(
            difficulty,
            handStretch,
            barreRequired,
            minimumFingers,
            cagedShape
        );
    }

    private static bool DetectBarreRequirement(int[] fretPositions)
    {
        // Check for same fret on 3+ adjacent strings
        for (var i = 0; i < fretPositions.Length - 2; i++)
        {
            var fret = fretPositions[i];
            if (fret > 0 &&
                fretPositions[i + 1] == fret &&
                fretPositions[i + 2] == fret)
            {
                return true;
            }
        }
        return false;
    }

    private static string CalculateDifficulty(int handStretch, bool barreRequired, int openStringCount)
    {
        // Beginner: Small stretch, no barre, has open strings
        if (handStretch <= 3 && !barreRequired && openStringCount > 0)
        {
            return "Beginner";
        }

        // Advanced: Large stretch or complex barre
        if (handStretch >= 5 || (barreRequired && handStretch >= 4))
        {
            return "Advanced";
        }

        // Intermediate: Everything else
        return "Intermediate";
    }

    private static string? DetectCagedShape(PhysicalLayout layout, Voicing voicing)
    {
        // Simplified CAGED detection - would need more sophisticated pattern matching
        // This is a placeholder for now
        if (layout.OpenStrings.Length >= 2 && layout.MaxFret <= 3)
        {
            // Could be C, A, G, E, or D shape in open position
            // Would need to analyze the actual pattern
            return null; // TODO: Implement proper CAGED detection
        }
        return null;
    }

    private static List<string> GenerateSemanticTags(
        ChordIdentification chordId,
        VoicingCharacteristics voicingChars,
        ModeInfo? modeInfo,
        PhysicalLayout layout,
        PlayabilityInfo playability)
    {
        var tags = new List<string>();

        // Position tags
        tags.Add(layout.HandPosition.ToLower().Replace(" ", "-"));
        if (layout.OpenStrings.Length > 0) tags.Add("open-strings");

        // Difficulty tags
        tags.Add(playability.Difficulty.ToLower());
        if (playability.Difficulty == "Beginner") tags.Add("beginner-friendly");
        if (playability.HandStretch >= 5) tags.Add("wide-stretch");
        if (playability.BarreRequired) tags.Add("barre-chord");

        // Voicing type tags
        if (voicingChars.DropVoicing != null)
        {
            tags.Add(voicingChars.DropVoicing.ToLower().Replace(" ", "-"));
            tags.Add("jazz-voicing");
        }
        if (voicingChars.IsRootless)
        {
            tags.Add("rootless");
            tags.Add("jazz-comping");
        }
        if (voicingChars.IsOpenVoicing) tags.Add("open-voicing");
        else tags.Add("closed-voicing");

        // Chord type tags
        if (chordId.ChordName?.Contains("maj7", StringComparison.OrdinalIgnoreCase) == true)
        {
            tags.Add("major-seventh");
            tags.Add("jazz-chord");
        }
        if (chordId.ChordName?.Contains("m7", StringComparison.OrdinalIgnoreCase) == true)
        {
            tags.Add("minor-seventh");
        }
        if (chordId.ChordName?.Contains("dom", StringComparison.OrdinalIgnoreCase) == true ||
            chordId.ChordName?.EndsWith("7") == true)
        {
            tags.Add("dominant");
        }

        // Mode tags
        if (modeInfo != null)
        {
            var modeName = modeInfo.ModeName.ToLower().Replace(" ", "-");
            tags.Add($"mode-{modeName}");

            if (modeInfo.ModeName.Contains("Dorian")) tags.Add("modal-jazz");
            if (modeInfo.ModeName.Contains("Phrygian")) tags.Add("modal-jazz");
            if (modeInfo.ModeName.Contains("Lydian")) tags.Add("modal-jazz");
        }

        // Style tags based on characteristics
        if (voicingChars.Features.Any(f => f.Contains("Quartal")))
        {
            tags.Add("quartal-harmony");
            tags.Add("modern-jazz");
        }

        // Use case tags
        if (playability.Difficulty == "Beginner" && layout.OpenStrings.Length >= 2)
        {
            tags.Add("campfire-chord");
            tags.Add("folk-guitar");
        }

        return [.. tags.Distinct()];
    }

    private static IReadOnlyDictionary<string, ModeFamilyMetadata> BuildModeMetadata()
    {
        var metadata = new Dictionary<string, ModeFamilyMetadata>(StringComparer.Ordinal);

        AddFamilyMetadata(
            metadata,
            "Major Scale Family",
            new[]
            {
                ("Ionian (Major)", new[] { 0, 2, 4, 5, 7, 9, 11 }),
                ("Dorian", new[] { 0, 2, 3, 5, 7, 9, 10 }),
                ("Phrygian", new[] { 0, 1, 3, 5, 7, 8, 10 }),
                ("Lydian", new[] { 0, 2, 4, 6, 7, 9, 11 }),
                ("Mixolydian", new[] { 0, 2, 4, 5, 7, 9, 10 }),
                ("Aeolian (Natural Minor)", new[] { 0, 2, 3, 5, 7, 8, 10 }),
                ("Locrian", new[] { 0, 1, 3, 5, 6, 8, 10 })
            });

        AddFamilyMetadata(
            metadata,
            "Harmonic Minor Family",
            new[]
            {
                ("Harmonic Minor", new[] { 0, 2, 3, 5, 7, 8, 11 }),
                ("Locrian ♮6", new[] { 0, 1, 3, 5, 6, 9, 10 }),
                ("Ionian ♯5 (Ionian Augmented)", new[] { 0, 2, 4, 5, 8, 9, 11 }),
                ("Dorian ♯4 (Romanian)", new[] { 0, 2, 3, 6, 7, 9, 10 }),
                ("Phrygian Dominant", new[] { 0, 1, 4, 5, 7, 8, 10 }),
                ("Lydian ♯2", new[] { 0, 3, 4, 6, 7, 9, 11 }),
                ("Ultralocrian (Superlocrian ♭♭7)", new[] { 0, 1, 3, 4, 6, 7, 9 })
            });

        AddFamilyMetadata(
            metadata,
            "Melodic Minor Family",
            new[]
            {
                ("Melodic Minor (Jazz Minor)", new[] { 0, 2, 3, 5, 7, 9, 11 }),
                ("Dorian ♭2 (Phrygian ♮6)", new[] { 0, 1, 3, 5, 7, 9, 10 }),
                ("Lydian Augmented (Lydian ♯5)", new[] { 0, 2, 4, 6, 8, 9, 11 }),
                ("Lydian Dominant (Overtone)", new[] { 0, 2, 4, 6, 7, 9, 10 }),
                ("Mixolydian ♭6 (Aeolian Dominant)", new[] { 0, 2, 4, 5, 7, 8, 10 }),
                ("Locrian ♮2 (Half Diminished)", new[] { 0, 2, 3, 5, 6, 8, 10 }),
                ("Altered (Super Locrian)", new[] { 0, 1, 3, 4, 6, 8, 10 })
            });

        return metadata;
    }

    private static void AddFamilyMetadata(
        IDictionary<string, ModeFamilyMetadata> metadata,
        string familyName,
        (string Name, int[] PitchClasses)[] definitions)
    {
        var icvKey = GetIntervalClassVectorKey(definitions[0].PitchClasses);
        metadata[icvKey] = CreateMetadata(familyName, definitions);
    }

    private static string GetIntervalClassVectorKey(int[] pitchClasses)
    {
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(PitchClass.FromValue));
        return pitchClassSet.IntervalClassVector.ToString();
    }

    private static ModeFamilyMetadata CreateMetadata(
        string familyName,
        (string Name, int[] PitchClasses)[] definitions)
    {
        if (definitions.Length == 0)
        {
            throw new ArgumentException("Mode definitions cannot be empty.", nameof(definitions));
        }

        var noteCount = definitions[0].PitchClasses.Length;
        var modeNames = definitions.Select(d => d.Name).ToArray();
        var ids = definitions
            .Select(d => CreatePitchClassSetId(d.PitchClasses))
            .ToList();

        return new ModeFamilyMetadata(
            familyName,
            noteCount,
            modeNames,
            ids);
    }

    private static PitchClassSetId CreatePitchClassSetId(IEnumerable<int> pitchClasses)
    {
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(PitchClass.FromValue));
        return pitchClassSet.Id;
    }

    private sealed record ModeFamilyMetadata(
        string FamilyName,
        int NoteCount,
        string[] ModeNames,
        List<PitchClassSetId> ModeIds);
}

/// <summary>
/// Comprehensive musical analysis result for a voicing
/// </summary>
public record MusicalVoicingAnalysis(
    MidiNote[] MidiNotes,
    PitchClassSet PitchClassSet,
    ChordIdentification ChordId,
    VoicingCharacteristics VoicingCharacteristics,
    ModeInfo? ModeInfo,
    List<string>? ChromaticNotes,
    SymmetricalScaleInfo? SymmetricalInfo,
    IntervallicInfo IntervallicInfo,
    EquivalenceInfo? EquivalenceInfo,
    PhysicalLayout PhysicalLayout,
    PlayabilityInfo PlayabilityInfo,
    List<string> SemanticTags
);

/// <summary>
/// Chord identification information
/// </summary>
public record ChordIdentification(
    string? ChordName,
    string? AlternateName,
    Key? ClosestKey,
    string? RomanNumeral,
    string? FunctionalDescription,
    SlashChordInfo? SlashChordInfo,
    bool IsNaturallyOccurring,
    ChordIntervals? Intervals
);

/// <summary>
/// Slash chord information
/// </summary>
public record SlashChordInfo(
    string Notation,
    string Type,
    bool IsCommonInversion
);

/// <summary>
/// Voicing characteristics (open/closed, drop voicings, etc.)
/// </summary>
public record VoicingCharacteristics(
    bool IsOpenVoicing,
    bool IsRootless,
    string? DropVoicing,
    int Span,
    List<string> Features
);

/// <summary>
/// Mode information
/// </summary>
public record ModeInfo(
    string ModeName,
    int NoteCount,
    int DegreeInFamily,
    string? FamilyName = null
);

/// <summary>
/// Chord intervals (theoretical and actual)
/// </summary>
public record ChordIntervals(
    List<string> Theoretical,
    List<string> Actual
);

/// <summary>
/// Symmetrical scale information
/// </summary>
public record SymmetricalScaleInfo(
    string ScaleName,
    List<string> PossibleRoots,
    string Description
);

/// <summary>
/// Intervallic content information
/// </summary>
public record IntervallicInfo(
    string IntervalClassVector,
    List<string> Features
);

/// <summary>
/// Equivalence group information for pattern recognition
/// </summary>
public record EquivalenceInfo(
    string PrimeFormId,
    bool IsPrimeForm,
    int TranslationOffset,
    int EquivalenceClassSize
);

/// <summary>
/// Physical layout on the fretboard
/// </summary>
public record PhysicalLayout(
    int[] FretPositions,
    int[] StringsUsed,
    int[] MutedStrings,
    int[] OpenStrings,
    int MinFret,
    int MaxFret,
    string HandPosition
);

/// <summary>
/// Playability and difficulty information
/// </summary>
public record PlayabilityInfo(
    string Difficulty,
    int HandStretch,
    bool BarreRequired,
    int MinimumFingers,
    string? CagedShape
);
