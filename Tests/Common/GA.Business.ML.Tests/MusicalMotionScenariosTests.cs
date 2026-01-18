namespace GA.Business.ML.Tests;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Wavelets;
using GA.Business.ML.Embeddings.Services;
using GA.Business.Core.Tonal.Cadences;
using GA.Business.Core.Notes.Primitives;
using NUnit.Framework;

[TestFixture]
public class MusicalMotionScenariosTests
{
    private ProgressionSignalService _signalService;

    [SetUp]
    public void Setup()
    {
        var phaseSphere = new PhaseSphereService();
        _signalService = new ProgressionSignalService(phaseSphere);
    }

    private VoicingDocument CreateChord(string name, int[] pitchClasses, double consonance = 1.0)
    {
        return new VoicingDocument
        {
            Id = name,
            SearchableText = name,
            ChordName = name,
            PitchClasses = pitchClasses,
            Consonance = consonance,
            Embedding = new double[216], // Dummy
            // Required placeholders
            RootPitchClass = 0, MidiNotes = [], SemanticTags = [], PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = "", IntervalClassVector = "", AnalysisEngine = "", AnalysisVersion = "", Jobs = [], TuningId = "", PitchClassSetId = "", Diagram = "", BarreRequired = false, HandStretch = 0, MinFret = 0, MaxFret = 0
        };
    }

    [Test]
    public void Test_All_Configured_Cadences()
    {
        var cadences = CadenceCatalog.Items;
        Assert.That(cadences, Is.Not.Empty, "Cadence catalog should not be empty");

        foreach (var cadence in cadences)
        {
            if (cadence.Chords == null || cadence.Chords.Count < 2) continue;

            TestContext.WriteLine($"Analyzing Cadence: {cadence.Name} ({string.Join("-", cadence.Chords)})");

            var progression = new List<VoicingDocument>();
            foreach (var chordName in cadence.Chords)
            {
                var pcs = ParseSimpleChord(chordName);
                progression.Add(CreateChord(chordName, pcs));
            }

            var signals = _signalService.ExtractSignals(progression);

            // Basic assertions
            Assert.That(signals.Stability.Length, Is.EqualTo(progression.Count));
            
            // Log results
            var startTension = signals.Tension.First();
            var endTension = signals.Tension.Last();
            var drift = signals.TonalDrift.Max() - signals.TonalDrift.Min();
            
            TestContext.WriteLine($"  Tension: {startTension:F2} -> {endTension:F2}");
            TestContext.WriteLine($"  Drift Range: {drift:F2}");
            
            // Heuristic Check: Resolution should usually lower tension
            // Note: We use default consonance=1.0 in CreateChord unless specified.
            // Since we parse name but don't set consonance dynamically here, 
            // tension will be 0.0 -> 0.0 unless we improve ParseSimpleChord to set consonance.
            // For now, we just verify no crash.
        }
    }

    private int[] ParseSimpleChord(string name)
    {
        // Enhanced parser for test purposes
        // Handles: C, Cm, C7, Cmaj7, C5, Csus, C9, C11, C13, C/E
        
        var rootStr = name.Length > 1 && (name[1] == '#' || name[1] == 'b') 
            ? name.Substring(0, 2) 
            : name.Substring(0, 1);
            
        int root = ParseRoot(rootStr);
        var quality = name.Substring(rootStr.Length).Split('/')[0]; // Ignore slash bass for PC content
        
        var intervals = new HashSet<int> { 0 }; // Root always present

        // Power Chord (5)
        if (quality == "5")
        {
            intervals.Add(7);
            return intervals.Select(i => (root + i) % 12).ToArray();
        }

        // Third
        if (quality.Contains("sus2")) intervals.Add(2);
        else if (quality.Contains("sus") || quality.Contains("sus4")) intervals.Add(5);
        else if (quality == "m" || quality.StartsWith("m") && !quality.StartsWith("maj")) intervals.Add(3); // Minor
        else intervals.Add(4); // Major default

        // Fifth (Default perfect, unless dim or alt)
        if (quality.Contains("b5") || quality.Contains("dim") || quality.Contains("°")) intervals.Add(6);
        else if (quality.Contains("#5") || quality.Contains("aug") || quality.Contains("+")) intervals.Add(8);
        else intervals.Add(7);

        // Seventh
        if (quality.Contains("maj7") || quality.Contains("maj9") || quality.Contains("maj13")) intervals.Add(11);
        else if (quality.Contains("7") || quality.Contains("9") || quality.Contains("11") || quality.Contains("13"))
        {
            if (quality.Contains("dim7")) intervals.Remove(6); // Re-add dim7 (9) if needed, but usually 0,3,6,9
            if (quality.Contains("dim7")) intervals.Add(9);
            else if (quality.Contains("m7b5") || quality.Contains("ø")) intervals.Add(10);
            else intervals.Add(10); // Dominant/Minor 7
        }

        // Extensions (Simplified)
        if (quality.Contains("9")) intervals.Add(2);
        if (quality.Contains("11")) intervals.Add(5);
        if (quality.Contains("13")) intervals.Add(9);
        
        // Alt (Approximation)
        if (quality.Contains("alt"))
        {
            intervals.Add(1); // b9
            intervals.Add(3); // #9 (enharmonic minor 3rd)
            intervals.Add(6); // #11
            intervals.Add(8); // b13
            intervals.Remove(7); // Remove perfect 5th often
            intervals.Remove(2); // Remove natural 9
        }

        return intervals.Select(i => (root + i) % 12).ToArray();
    }

    private int ParseRoot(string s)
    {
        return s switch
        {
            "C" => 0, "C#" => 1, "Db" => 1,
            "D" => 2, "D#" => 3, "Eb" => 3,
            "E" => 4,
            "F" => 5, "F#" => 6, "Gb" => 6,
            "G" => 7, "G#" => 8, "Ab" => 8,
            "A" => 9, "A#" => 10, "Bb" => 10,
            "B" => 11,
            _ => 0
        };
    }
}