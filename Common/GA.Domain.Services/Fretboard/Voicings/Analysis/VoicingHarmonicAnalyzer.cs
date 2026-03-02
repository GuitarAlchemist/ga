namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using Business.Core.Analysis;
using Business.Core.Analysis.Voicings;
using Chords;
using GA.Domain.Core.Theory.Harmony;

public static class VoicingHarmonicAnalyzer
{
    public static VoicingCharacteristics Analyze(Voicing voicing)
    {
        var midiNotes = voicing.Notes.Select(n => n.Value).OrderBy(n => n).ToArray();
        var pitchClasses = voicing.Notes.Select(n => n.PitchClass).Distinct().OrderBy(pc => pc.Value).ToList();

        // Basic identification via PitchClassSet
        var pcSet = new PitchClassSet(pitchClasses);
        var chordId = IdentifyChord(pcSet, pitchClasses, PitchClass.FromValue(midiNotes[0]));

        // Calculate consonance/dissonance
        var dissonanceScore = CalculateDissonance(midiNotes);
        var consonance = 1.0 - Math.Min(dissonanceScore / 10.0, 1.0); // Normalize to 0-1

        // Interval spread
        var intervalSpread = midiNotes.Last() - midiNotes.First();

        // Generate semantic tags based on characteristics
        var semanticTags = GenerateSemanticTags(chordId, intervalSpread, consonance);

        var dropVoicing = DetectDropVoicing(midiNotes);

        return new(
            chordId,
            dissonanceScore,
            consonance,
            intervalSpread,
            pitchClasses.Count,
            pcSet.IntervalClassVector.ToString(),
            intervalSpread > 12,
            dropVoicing,
            false,
            [],
            semanticTags
        );
    }

    private static string? DetectDropVoicing(int[] midiNotes)
    {
        if (midiNotes.Length < 4) return null;
        var sorted = midiNotes.OrderBy(n => n).ToArray();

        // General Drop-2 detection: Raise the bass note 1 octave.
        // If the resulting set is "closed" (span <= 12), it's identified as a Drop-2 voicing.
        var raisedBass = sorted[0] + 12;
        var newSet = sorted.Skip(1).Append(raisedBass).OrderBy(n => n).ToArray();
        if (newSet.Max() - newSet.Min() <= 12) return "Drop-2";

        return null;
    }

    private static Dictionary<int, ChordTemplate>? _templateCache;

    public static ChordIdentification IdentifyChord(PitchClassSet pcSet, IEnumerable<PitchClass> notes,
        PitchClass bassNote)
    {
        // Lazy initialization of the template cache, ensuring we pick the simplest template for each interval set
        if (_templateCache == null)
        {
            _templateCache = ChordTemplateFactory.GenerateAllPossibleChords()
                .GroupBy(t => t.PitchClassSet.Id.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(GetComplexity).First()
                );
        }

        var noteValues = notes.Select(n => n.Value).Distinct().ToList();
        var matches = new List<(PitchClass Root, ChordTemplate Template)>();

        foreach (var rootValue in noteValues)
        {
            var transposedId = pcSet.Id.Transpose(12 - rootValue);

            // Find best matching template for this root from the cache
            if (_templateCache.TryGetValue(transposedId.Value, out var match))
            {
                matches.Add((PitchClass.FromValue(rootValue), match));
            }
        }

        if (matches.Count == 0)
        {
            // Fallback
            return new(AnalysisConstants.Unknown, bassNote.ToString(), AnalysisConstants.UnknownQuality, true, "Function",
                "Quality", null, null);
        }

        // Rank order:
        // 1. Complexity (Triads > Sevenths > Extensions)
        // 2. Bass Note is Root (Root Position > Inversion)
        var ranked = matches
            .Select(m => new { Match = m, Score = GetComplexity(m.Template) })
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Match.Root.Value == bassNote.Value ? 0 : 1)
            .ToList();

        var bestMatch = ranked.First().Match;
        var template = bestMatch.Template;

        var chordName = template.Name;
        // Improve naming for standard chords
        if (template.Extension == ChordExtension.Triad && IsStandardIntervals(template))
        {
            chordName = $"{GetNoteName(bestMatch.Root.Value)} {template.Quality}";
        }
        else if (template.Extension == ChordExtension.Seventh && IsStandardIntervals(template))
        {
             // TODO: Add standard intervals check for sevenths if needed, for now just handle triads or check quality
             // Use simple concatenation if it looks standard-ish?
             // But IsStandardIntervals only checks triads currently.
        }

        // Add slash notation if bass note differs from root
        if (bassNote.Value != bestMatch.Root.Value)
        {
            chordName += $"/{GetNoteName(bassNote.Value)}";
        }

        return new(
            chordName,
            bestMatch.Root.ToString(),
            template.Quality.ToString(),
            true,
            AnalysisConstants.FunctionalHarmony,
            template.Quality.ToString(),
            null,
            null);
    }

    private static string GetNoteName(int pc)
    {
        string[] names = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
        return names[pc % 12];
    }

    private static int GetComplexity(ChordTemplate t)
    {
        int score = 0;

        // Extensions
        score += t.Extension switch
        {
            ChordExtension.Triad => 0,
            ChordExtension.Sus2 => 1,
            ChordExtension.Sus4 => 2,
            ChordExtension.Sixth => 3,
            ChordExtension.Seventh => 10,
            ChordExtension.Add9 => 11,
            ChordExtension.Ninth => 20,
            _ => 30
        };

        // Quality preferences (Major/Minor/Dominant are most common)
        score += t.Quality switch
        {
            ChordQuality.Major => 0,
            ChordQuality.Minor => 0,
            ChordQuality.Dominant => 1,
            ChordQuality.Diminished => 2,
            ChordQuality.Augmented => 3,
            _ => 5
        };

        // Stacking
        if (t.StackingType != ChordStackingType.Tertian) score += 5;

        // Penalize non-standard triads to favor standard chords (e.g. C/E over E mb6)
        if (t.Extension == ChordExtension.Triad && t.StackingType == ChordStackingType.Tertian)
        {
            if (!IsStandardIntervals(t)) score += 10;
        }

        return score;
    }

    private static bool IsStandardIntervals(ChordTemplate t)
    {
        var pcs = t.PitchClassSet.Select(p => p.Value).OrderBy(x => x).ToList();
        return
            pcs.SequenceEqual([0, 4, 7]) || // Major
            pcs.SequenceEqual([0, 3, 7]) || // Minor
            pcs.SequenceEqual([0, 3, 6]) || // Diminished
            pcs.SequenceEqual([0, 4, 8]); // Augmented
    }

    private static double CalculateDissonance(int[] midiNotes)
    {
        // Placeholder dissonance calculation (e.g. based on intervals)
        double dissonance = 0;
        for (var i = 0; i < midiNotes.Length; i++)
        {
            for (var j = i + 1; j < midiNotes.Length; j++)
            {
                var interval = Math.Abs(midiNotes[i] - midiNotes[j]);
                if (interval % 12 == 1 || interval % 12 == 6)
                {
                    dissonance += 1.0; // Minor 2nd or Tritone
                }
            }
        }

        return dissonance;
    }

    private static string[] GenerateSemanticTags(ChordIdentification chordId, int intervalSpread, double consonance)
    {
        var tags = new List<string>();
        
        // 1. Basic Harmonic Quality
        if (consonance > 0.7) tags.Add("consonant");
        else if (consonance < 0.3) tags.Add("dissonant");
        
        // 2. Spread/Register
        if (intervalSpread > 24) tags.Add("wide-voicing");
        else if (intervalSpread < 12) tags.Add("close-voicing");
        
        // 3. Quality Tags
        string? quality = null;
        if (!string.IsNullOrEmpty(chordId.Quality))
        {
            quality = chordId.Quality.ToLowerInvariant();
            tags.Add(quality);
            
            // 4. Genre Mapping (Heuristic)
            if (quality.Contains("maj7") || quality.Contains("maj9") || quality.Contains("13") || quality.Contains("dominant"))
                tags.Add("jazz");
            if (quality.Contains("sus2") || quality.Contains("sus4") || quality.Contains("add9"))
                tags.Add("pop");
            if (quality.Contains("minor") && consonance > 0.6)
                tags.Add("melancholy");
            if (quality.Contains("diminished") || quality.Contains("augmented") || quality.Contains("alt"))
                tags.Add("tension");
        }

        // 5. Functional Moods
        if (consonance > 0.8 && quality != null && quality.Contains("major")) tags.Add("bright");
        if (consonance < 0.4) tags.Add("dark");
        if (intervalSpread > 12 && intervalSpread < 24) tags.Add("balanced");

        return [.. tags.Distinct()];
    }
}
