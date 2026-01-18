namespace GA.Business.ML.Musical.Analysis;

using Core.Tonal.Cadences;
using Core.Fretboard.Voicings.Search;
using Core.Atonal;
using Core.Tonal;
using Core.Chords;

public class CadenceDetector
{
    private readonly List<CadencePattern> _patterns;
    private readonly CadenceChordParser _parser;

    public CadenceDetector()
    {
        _parser = new CadenceChordParser();
        _patterns = InitializePatterns();
    }

    /// <summary>
    /// Analyzes a progression to find potential cadences at the end.
    /// </summary>
    public CadenceMatch? DetectCadence(List<VoicingDocument> progression)
    {
        if (progression.Count < 2) return null;

        // Check the end of the progression (last 2, 3, 4 chords)
        // We prioritize longer matches (e.g. ii-V-I > V-I)
        
        for (int length = 4; length >= 2; length--)
        {
            if (progression.Count < length) continue;

            var slice = progression.TakeLast(length).ToList();
            var match = MatchPattern(slice);
            
            if (match != null) return match;
        }

        return null;
    }

    private CadenceMatch? MatchPattern(List<VoicingDocument> slice)
    {
        // If any chord has no detected root, we can't match root movement patterns
        if (slice.Any(d => d.RootPitchClass == null)) return null;

        var inputRoots = slice.Select(d => d.RootPitchClass!.Value).ToList();
        var inputQualities = slice.Select(d => DeriveQuality(d)).ToList();

        foreach (var pattern in _patterns)
        {
            if (pattern.Length != slice.Count) continue;

            if (Matches(inputRoots, inputQualities, pattern))
            {
                // Determine Key based on the match
                // If pattern ends on I (Target), the last chord root is the Key.
                // If pattern ends on V (Half), the last chord root is V of Key.
                var lastRoot = inputRoots.Last();
                var keyRoot = (lastRoot - pattern.RelativeRoots.Last() + 12) % 12; // Normalize
                
                return new CadenceMatch(pattern.Definition, PitchClass.FromValue(keyRoot));
            }
        }

        return null;
    }

    private bool Matches(List<int> roots, List<ChordQuality> qualities, CadencePattern pattern)
    {
        // 1. Check Qualities
        for (int i = 0; i < qualities.Count; i++)
        {
            if (!IsQualityCompatible(qualities[i], pattern.Qualities[i])) return false;
        }

        // 2. Check Relative Root Movements
        // Calculate intervals between adjacent roots in input
        for (int i = 0; i < roots.Count - 1; i++)
        {
            var inputInterval = (roots[i+1] - roots[i] + 12) % 12;
            var patternInterval = (pattern.RelativeRoots[i+1] - pattern.RelativeRoots[i] + 12) % 12;

            if (inputInterval != patternInterval) return false;
        }

        return true;
    }

    private bool IsQualityCompatible(ChordQuality input, ChordQuality pattern)
    {
        if (input == pattern) return true;
        
        // Allow Dom7 to match Major (V -> V7)
        if (pattern == ChordQuality.Major && input == ChordQuality.Dominant) return true;
        if (pattern == ChordQuality.Dominant && input == ChordQuality.Major) return true;
        
        // Allow Min7 to match Minor
        if (pattern == ChordQuality.Minor && input == ChordQuality.Minor7) return true;
        if (pattern == ChordQuality.Minor7 && input == ChordQuality.Minor) return true;

        // Allow Maj7 to match Major
        if (pattern == ChordQuality.Major && input == ChordQuality.Major7) return true;
        if (pattern == ChordQuality.Major7 && input == ChordQuality.Major) return true;

        return false;
    }

    private ChordQuality DeriveQuality(VoicingDocument doc)
    {
        // Use Parser logic
        return _parser.ParseQuality(doc.ChordName);
    }

    private List<CadencePattern> InitializePatterns()
    {
        var patterns = new List<CadencePattern>();
        
        // Load from CadenceCatalog and convert to patterns
        foreach (var def in CadenceCatalog.Items)
        {
            if (def.Chords == null || def.Chords.Count == 0) continue;

            var relativeRoots = new List<int>();
            var qualities = new List<ChordQuality>();

            foreach (var chordName in def.Chords)
            {
                // Use Parser
                int root = _parser.ParseRoot(chordName);
                var qual = _parser.ParseQuality(chordName);
                
                relativeRoots.Add(root); 
                qualities.Add(qual);
            }

            patterns.Add(new CadencePattern(def, relativeRoots, qualities));
        }

        return patterns;
    }

    private record CadencePattern(CadenceDefinition Definition, List<int> RelativeRoots, List<ChordQuality> Qualities)
    {
        public int Length => RelativeRoots.Count;
    }
}

public record CadenceMatch(CadenceDefinition Definition, PitchClass Key);