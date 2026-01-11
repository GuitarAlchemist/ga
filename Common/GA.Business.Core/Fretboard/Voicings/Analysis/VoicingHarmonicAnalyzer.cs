namespace GA.Business.Core.Fretboard.Voicings.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using Atonal;
using Chords;
using Core;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Notes.Primitives;
using Generation;
using Tonal;

/// <summary>
/// specialized analyzer for harmonic and theoretical properties of voicings
/// </summary>
public static class VoicingHarmonicAnalyzer
{
    private static readonly IReadOnlyDictionary<string, ModeFamilyMetadata> _modeMetadataMap = ModeCatalog.Metadata;

    public static ChordIdentification IdentifyChord(PitchClassSet pitchClassSet, List<PitchClass> pitchClasses, PitchClass? bassNote)
    {
        if (pitchClasses.Count == 0)
        {
            return new(null, null, null, null, null, null, null, false, null, null, 0.0);
        }

        // FIXED: Use intelligent root detection instead of assuming lowest pitch class
        var (root, rootConfidence) = GuessRoot(pitchClasses, pitchClassSet, bassNote);

        try
        {
            // Create a chord template from the pitch class set using the detected root
            // This ensures intervals are calculated correctly relative to the actual musical root
            var template = ChordTemplateFactory.FromPitchClassSet(pitchClassSet, root, "Analysis");

            // Generate comprehensive names using the unified naming service (with bass note for slash chord detection)
            var bestName = ChordTemplateNamingService.GetBestChordName(template, root, bassNote);
            var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(template, root, bassNote);

            // === CRITICAL FIX: Enforce Strict Guide Tone Rules for Dominant 7ths ===
            // If the algorithm identified a "7" chord (dominant), ensure it actually has the 3rd and 7th.
            // If not, we must qualify the name to avoid misleading results.
            if (bestName.EndsWith("7") && !bestName.Contains("m") && !bestName.Contains("maj") && !bestName.Contains("sus"))
            {
               var has3Rd = HasThird(pitchClassSet, root);
               var has7Th = HasSeventh(pitchClassSet, root);

               if (!has7Th)
               {
                   // If no 7th, it's NOT a Dominant 7th. It's likely just Major or Add something.
                   bestName = bestName.Replace("7", "");
                   if (bestName.EndsWith("G") && bestName.Length == 1) bestName = "G"; // Clean up edge case
               }
               else if (!has3Rd)
               {
                   // Dominant 7th without 3rd is ambiguous. It's strictly "7(no3)".
                   // Unless it's a sus4, but checked above.
                   bestName += "(no3)";
               }
            }
            // ======================================================================

            // Final override: if we have an iconic name, it usually wins for guitar contexts
            if (!string.IsNullOrEmpty(comprehensive.IconicName))
            {
                bestName = comprehensive.IconicName;
            }

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

            // Determine harmonic function
            var harmonicFunction = scaleDegree switch
            {
                1 or 3 or 6 => "Tonic",
                2 or 4 => "Predominant",
                5 or 7 => "Dominant",
                _ => "Ambiguous"
            };

            return new(
                bestName,
                !string.IsNullOrEmpty(comprehensive.IconicName) ? comprehensive.IconicName : comprehensive.Primary,
                closestKey,
                romanNumeral,
                $"{romanNumeral} in {closestKey}",
                harmonicFunction,
                slashChordInfo,
                isNaturallyOccurring,
                intervals,
                root,
                rootConfidence
            );
        }
        catch
        {
            // If chord identification fails, return atonal analysis
            return new(
                $"Set {pitchClassSet.Id.Value}",
                null,
                null,
                null,
                "Atonal/Ambiguous",
                "Ambiguous",
                null,
                false,
                null,
                bassNote,
                0.3
            );
        }
    }

    public static VoicingCharacteristics AnalyzeVoicingCharacteristics(Voicing voicing, ChordIdentification chordId)
    {
        var midiNotes = voicing.Notes;
        var pitchClasses = midiNotes.Select(n => n.PitchClass).Distinct().ToList();

        // Detect open vs closed voicing (span > octave)
        var lowestNote = midiNotes[0].Value;
        var highestNote = midiNotes[^1].Value;
        var span = highestNote - lowestNote;
        var isOpenVoicing = span > 12;

        // FIXED: "Rootless" means the root pitch class is ABSENT from the voicing
        // NOT that the bass note differs from the root (that's an inversion!)
        // An inversion (e.g., C/E) has the root C present, just not in the bass.
        // A rootless voicing (e.g., jazz Dm7 without D) truly omits the root.
        var detectedRoot = chordId.RootPitchClass;
        var bassNote = midiNotes[0].PitchClass;

        bool isRootless;
        if (detectedRoot.HasValue)
        {
            // Rootless = root pitch class not in the pitch set
            isRootless = !pitchClasses.Any(pc => pc.Value == detectedRoot.Value.Value);
        }
        else
        {
            // Fallback: if we couldn't detect root, assume not rootless
            isRootless = false;
        }

        // Also track if this is an inversion (bass != root, but root IS present)
        var isInversion = detectedRoot.HasValue &&
                          bassNote.Value != detectedRoot.Value.Value &&
                          !isRootless;

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
        if (isInversion) features.Add("Inversion");
        if (dropVoicing != null) features.Add(dropVoicing);
        if (isQuartal) features.Add("Quartal/Quintal harmony");
        if (isSuspended) features.Add("Suspended");
        if (hasAddedTones) features.Add("Added tones");

        // Determine voicing type
        var voicingType = dropVoicing ?? (isOpenVoicing ? "Open" : "Closed");

        return new(
            isOpenVoicing,
            isRootless,
            dropVoicing,
            voicingType,
            span,
            features
        );
    }

    public static ModeInfo? DetectMode(PitchClassSet pitchClassSet)
    {
        var icv = pitchClassSet.IntervalClassVector.ToString();

        if (_modeMetadataMap.TryGetValue(icv, out var metadata))
        {
            var modeIndexFromMetadata = metadata.ModeIds.FindIndex(id => id.Equals(pitchClassSet.Id));
            if (modeIndexFromMetadata >= 0)
            {
                return new(
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

    public static List<string>? IdentifyChromaticNotes(PitchClassSet pitchClassSet, Key key)
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

    public static SymmetricalScaleInfo? DetectSymmetricalScales(PitchClassSet pitchClassSet)
    {
        // Try to get Forte number for robust identification
        if (!ForteCatalog.TryGetForteNumber(pitchClassSet.PrimeForm, out var forte))
        {
            return null;
        }

        var forteString = forte.ToString();

        // Octatonic (Diminished) Check: 8-28
        if (forteString == "8-28")
        {
            return new(
                "Diminished (Octatonic)",
                GetDiminishedRoots(pitchClassSet),
                "Symmetrical scale (Messiaen Mode 2)"
            );
        }

        // Whole Tone Check: 6-35
        if (forteString == "6-35")
        {
            return new(
                "Whole Tone",
                GetWholeToneRoots(pitchClassSet),
                "Symmetrical scale (Messiaen Mode 1)"
            );
        }

        // Augmented Triad: 3-12
        if (forteString == "3-12")
        {
             return new(
                "Augmented Triad",
                [.. pitchClassSet.ToList().Select(pc => pc.ToString())],
                "Symmetrical chord with 3 enharmonic roots"
            );
        }

        // Diminished 7th: 4-28
        if (forteString == "4-28")
        {
             return new(
                "Diminished 7th",
                [.. pitchClassSet.ToList().Select(pc => pc.ToString())],
                "Symmetrical chord with 4 enharmonic roots"
            );
        }

        // Hexatonic (Augmented Scale): 6-20
        if (forteString == "6-20")
        {
             return new(
                "Augmented Scale (Hexatonic)",
                [.. pitchClassSet.ToList().Select(pc => pc.ToString())],
                "Symmetrical scale (Messiaen Mode 3)"
            );
        }

        return null;
    }

    public static IntervallicInfo AnalyzeIntervallic(PitchClassSet pitchClassSet)
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

        return new(
            icv.ToString(),
            features
        );
    }

    public static EquivalenceInfo? ExtractEquivalenceInfo(DecomposedVoicing decomposedVoicing)
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

        // Resolve Forte code using programmatic catalog
        // Extract pitch classes from the voicing and get Forte number
        string? forteCode = null;
        var pitchClasses = decomposedVoicing.Voicing.Notes.Select(n => n.PitchClass).Distinct().ToList();
        if (pitchClasses.Count > 0)
        {
            var pcs = new PitchClassSet(pitchClasses);
            if (ForteCatalog.TryGetForteNumber(pcs, out var forteNum))
            {
                forteCode = forteNum.ToString();
            }
        }

        return new(
            primeFormId,
            forteCode,
            isPrimeForm,
            translationOffset,
            equivalenceClassSize
        );
    }

    public static ToneInventory AnalyzeToneInventory(MidiNote[] midiNotes, ChordIdentification chordId)
    {
        var tonesPresent = new List<string>();
        var doubledTones = new List<string>();
        var omittedTones = new List<string>();

        // Get pitch classes with their occurrences
        var pitchClassCounts = midiNotes
            .GroupBy(n => n.PitchClass.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // FIXED: Use the detected root from ChordIdentification (source of truth)
        // Not the bass note, which could be an inversion
        var trueBass = midiNotes.Length > 0 ? midiNotes.MinBy(n => n.Value).PitchClass.Value : 0;
        var rootPc = chordId.RootPitchClass?.Value ?? trueBass;
        var bassPc = trueBass;

        // FIXED: "hasRoot" means the root pitch class is present in the voicing
        // NOT that bass == root (inversions have the root, just not in bass)
        var hasRoot = pitchClassCounts.ContainsKey(rootPc);
        var hasThird = pitchClassCounts.ContainsKey((rootPc + 3) % 12) || pitchClassCounts.ContainsKey((rootPc + 4) % 12);
        var hasFifth = pitchClassCounts.ContainsKey((rootPc + 7) % 12);
        var hasSeventh = pitchClassCounts.ContainsKey((rootPc + 10) % 12) || pitchClassCounts.ContainsKey((rootPc + 11) % 12);

        if (hasRoot) tonesPresent.Add("Root");
        if (hasThird) tonesPresent.Add("3rd");
        if (hasFifth) tonesPresent.Add("5th");
        if (hasSeventh) tonesPresent.Add("7th");

        // Check for extended tones
        if (pitchClassCounts.ContainsKey((rootPc + 2) % 12))
            tonesPresent.Add("9th");
        if (pitchClassCounts.ContainsKey((rootPc + 5) % 12))
            tonesPresent.Add("11th");
        if (pitchClassCounts.ContainsKey((rootPc + 9) % 12))
            tonesPresent.Add("13th");

        // Check for doublings
        foreach (var kvp in pitchClassCounts.Where(k => k.Value > 1))
        {
            var interval = (kvp.Key - rootPc + 12) % 12;
            var toneName = interval switch
            {
                0 => "Root",
                3 or 4 => "3rd",
                7 => "5th",
                10 or 11 => "7th",
                _ => $"Tone {interval}"
            };
            doubledTones.Add(toneName);
        }

        // Check for omissions (from a "standard" 7th chord: Root, 3rd, 5th, 7th)
        // FIXED: Root can be omitted in truly rootless voicings (common in jazz)
        if (!hasRoot) omittedTones.Add("Root");
        if (!hasThird) omittedTones.Add("3rd");
        if (!hasFifth) omittedTones.Add("5th");

        // Guide tones = 3rd + 7th (critical for jazz)
        var hasGuideTones = hasThird && hasSeventh;

        return new(
            tonesPresent,
            doubledTones,
            omittedTones,
            hasGuideTones,
            hasRoot,
            hasThird,
            hasFifth,
            hasSeventh
        );
    }

    public static PerceptualQualities AnalyzePerceptualQualities(MidiNote[] midiNotes, PhysicalLayout layout)
    {
        if (midiNotes.Length == 0)
        {
            return new("Unknown", 0.5, 0.5, 0.0, "Close", false, null);
        }

        var midiValues = midiNotes.Select(n => n.Value).OrderBy(n => n).ToArray();

        // === Register classification based on average MIDI note ===
        var avgMidi = midiValues.Average();
        var register = avgMidi switch
        {
            < 48 => "Low",
            < 60 => "Mid-Low",
            < 72 => "Mid",
            < 84 => "Mid-High",
            _ => "High"
        };

        // === Brightness: spectral centroid proxy (normalized average pitch) ===
        // Guitar range roughly 40-88 MIDI
        // Adjusted v3: More sensitive curve. (avg-40)/40.0
        // Avg MIDI 60 (Middle C) -> 0.5
        // Avg MIDI 72 (High C) -> 0.8 (Bright)
        var brightness = Math.Clamp((avgMidi - 40) / 40.0, 0.0, 1.0);

        // === Low-interval mud penalty ===
        // Penalize small intervals (seconds, thirds) under ~E3 (MIDI 52)
        // These create psychoacoustic "beating" that sounds muddy on guitar
        double mudPenalty = 0;
        for (var i = 0; i < midiValues.Length; i++)
        {
            for (var j = i + 1; j < midiValues.Length; j++)
            {
                var loNote = midiValues[i];
                var hiNote = midiValues[j];
                var interval = (hiNote - loNote) % 12;

                // Penalize close intervals in bass (the root cause of muddiness)
                if (loNote <= 52 && interval is 1 or 2 or 3 or 4)
                {
                    mudPenalty += 1.0;
                    // Weight more heavily for very low notes
                    if (loNote <= 45) mudPenalty += 0.5;
                }
            }
        }

        // === Roughness proxy (psychoacoustic beating/dissonance) ===
        // Based on interval class dissonance weights
        double roughnessSum = 0;
        for (var i = 0; i < midiValues.Length - 1; i++)
        {
            for (var j = i + 1; j < midiValues.Length; j++)
            {
                var intervalClass = (midiValues[j] - midiValues[i]) % 12;
                // Interval-class dissonance weights (higher = more rough)
                roughnessSum += intervalClass switch
                {
                    0 => 0.0,   // Unison - perfect
                    1 => 1.0,   // Minor 2nd - very rough
                    2 => 0.8,   // Major 2nd - rough
                    3 => 0.2,   // Minor 3rd - consonant
                    4 => 0.2,   // Major 3rd - consonant
                    5 => 0.1,   // Perfect 4th - consonant
                    6 => 0.9,   // Tritone - dissonant
                    7 => 0.0,   // Perfect 5th - perfect
                    8 => 0.3,   // Minor 6th - imperfect consonance
                    9 => 0.3,   // Major 6th - imperfect consonance
                    10 => 0.7,  // Minor 7th - dissonant
                    11 => 0.6,  // Major 7th - dissonant
                    _ => 0.5
                };
            }
        }

        // Normalize roughness to 0-1
        var pairCount = midiValues.Length * (midiValues.Length - 1) / 2;
        var roughness = pairCount > 0 ? Math.Clamp(roughnessSum / pairCount, 0.0, 1.0) : 0.0;

        // === Consonance score (inverse of roughness, adjusted by mud) ===
        // consonance = 1 - (roughness + mudPenalty*k) with normalization
        var consonanceScore = Math.Clamp(1.0 - roughness - (mudPenalty * 0.1), 0.0, 1.0);

        // === Spacing classification ===
        // Based on average adjacent intervals
        var adjacentIntervals = new List<int>();
        for (var i = 1; i < midiValues.Length; i++)
        {
            adjacentIntervals.Add(midiValues[i] - midiValues[i - 1]);
        }

        var spacing = "Close";
        if (adjacentIntervals.Count > 0)
        {
            var avgInterval = adjacentIntervals.Average();
            spacing = avgInterval switch
            {
                <= 4 => "Close",
                <= 6 => "Mixed",
                _ => "Open"
            };
        }

        // === Boolean mud warning ===
        var mayBeMuddy = mudPenalty >= 1.0;

        // === Textural description (human-readable) ===
        var textural = (brightness, consonanceScore, mayBeMuddy, spacing) switch
        {
            (_, _, true, _) => "Potentially muddy - consider higher voicing",
            (> 0.7, > 0.7, _, _) => "Bright and clear",
            (> 0.7, < 0.4, _, _) => "Bright but tense",
            (< 0.3, > 0.7, _, _) => "Warm and rich",
            (< 0.3, < 0.4, _, _) => "Dark and complex",
            (_, _, _, "Open") => "Open and airy",
            (_, _, _, "Close") => "Dense and compact",
            _ => "Balanced"
        };

        return new(register, brightness, consonanceScore, roughness, spacing, mayBeMuddy, textural);
    }

    public static List<string>? DetectAlternateChordNames(PitchClassSet pitchClassSet, ChordIdentification primaryId)
    {
        var alternates = new List<string>();

        // Add the primary name
        if (primaryId.ChordName != null)
        {
            alternates.Add(primaryId.ChordName);
        }

        // Common enharmonic equivalents
        // C6 = Am7, Dm6 = Bm7b5, etc.
        var pcs = pitchClassSet.ToList();
        if (pcs.Count == 4)
        {
            // Check for 6th chord = m7 equivalence
            // A 6th chord has intervals: 0, 4, 7, 9 (major 6) or 0, 3, 7, 9 (minor 6)
            // An m7 chord has intervals: 0, 3, 7, 10
            // They share pitch classes when the 6th chord root is a minor 3rd above the m7 root

            // Try each pitch class as potential alternate root
            foreach (var potentialRoot in pcs)
            {
                var intervalsFromRoot = pcs
                    .Select(p => (p.Value - potentialRoot.Value + 12) % 12)
                    .OrderBy(i => i)
                    .ToList();

                // Check for m7 pattern: 0, 3, 7, 10
                if (intervalsFromRoot.SequenceEqual([0, 3, 7, 10]) && potentialRoot != pcs[0])
                {
                    alternates.Add($"{PitchClassName(potentialRoot)}m7");
                }
                // Check for maj6 pattern: 0, 4, 7, 9
                if (intervalsFromRoot.SequenceEqual([0, 4, 7, 9]) && potentialRoot != pcs[0])
                {
                    alternates.Add($"{PitchClassName(potentialRoot)}6");
                }
            }
        }

        return alternates.Count > 1 ? alternates.Distinct().ToList() : null;
    }

    // ================== HELPERS ==================

    private static (PitchClass Root, double Confidence) GuessRoot(
        List<PitchClass> pitchClasses,
        PitchClassSet pcs,
        PitchClass? bassNote)
    {
        if (pitchClasses.Count == 0)
            return (PitchClass.FromValue(0), 0.0);

        var bass = bassNote ?? pitchClasses[0];
        var best = bass;
        var bestScore = double.MinValue;

        foreach (var candidate in pitchClasses)
        {
            double score = 0;

            // Heuristic 1: Reward if bass == root (very common in guitar voicings)
            if (candidate == bass) score += 15;

            // Heuristic 2: Reward presence of major or minor 3rd above candidate
            var hasMajor3Rd = pcs.Contains(PitchClass.FromValue((candidate.Value + 4) % 12));
            var hasMinor3Rd = pcs.Contains(PitchClass.FromValue((candidate.Value + 3) % 12));
            if (hasMajor3Rd || hasMinor3Rd) score += 12;

            // Heuristic 3: Reward presence of perfect 5th above candidate
            var has5Th = pcs.Contains(PitchClass.FromValue((candidate.Value + 7) % 12));
            if (has5Th) score += 8;

            // Heuristic 4: Reward presence of 7th (makes it a full chord)
            var hasMinor7Th = pcs.Contains(PitchClass.FromValue((candidate.Value + 10) % 12));
            var hasMajor7Th = pcs.Contains(PitchClass.FromValue((candidate.Value + 11) % 12));
            if (hasMinor7Th || hasMajor7Th) score += 6;

            // Heuristic 5: Penalize if no 3rd AND no 5th (ambiguous power chord or cluster)
            if (!hasMajor3Rd && !hasMinor3Rd && !has5Th) score -= 5;

            // Heuristic 6: Slight preference for "common" roots (C, G, D, A, E, F)
            if (candidate.Value is 0 or 7 or 2 or 9 or 4 or 5) score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        // Calculate confidence: best score relative to max possible
        // Max possible: 15 + 12 + 8 + 6 + 1 = 42
        var confidence = Math.Clamp(bestScore / 42.0, 0.0, 1.0);

        return (best, confidence);
    }

    private static bool HasThird(PitchClassSet pcs, PitchClass root)
    {
        return pcs.Contains(PitchClass.FromValue((root.Value + 3) % 12)) ||
               pcs.Contains(PitchClass.FromValue((root.Value + 4) % 12));
    }

    private static bool HasSeventh(PitchClassSet pcs, PitchClass root)
    {
        return pcs.Contains(PitchClass.FromValue((root.Value + 10) % 12)) ||
               pcs.Contains(PitchClass.FromValue((root.Value + 11) % 12));
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
        return new(
            analysis.SlashNotation,
            analysis.Type.ToString(),
            analysis.IsCommonInversion
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
        for (var i = 1; i < midiNotes.Length; i++)
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

    private static (string? FamilyName, string? ModeName) ResolveModeNameFromIntervalVector(
        string intervalClassVector,
        int modeIndex,
        string? preferredFamilyName)
    {
        var degree = modeIndex + 1;
        var familyName = preferredFamilyName;
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

        return new(theoretical, actual);
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

    private static string PitchClassName(PitchClass pc)
    {
        return pc.Value switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#", 4 => "E", 5 => "F",
            6 => "F#", 7 => "G", 8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };
    }
}
