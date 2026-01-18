namespace GA.Business.ML.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Notes;
using GA.Business.ML.Tabs;
using GA.Business.ML.Tests.TestInfrastructure;
using GA.Business.ML.Embeddings;
using NUnit.Framework;

[TestFixture]
public class GroundTruthRealizationTests
{
    private TabSequenceSolver _basicSolver;
    private AdvancedTabSolver _advancedSolver;
    private TabToPitchConverter _pitchConverter;
    private TabTokenizer _tokenizer;
    private FileBasedVectorIndex _testIndex;
    private MusicalEmbeddingGenerator _generator;

    [SetUp]
    public void Setup()
    {
        var tuning = Tuning.Default;
        var mapper = new FretboardPositionMapper(tuning);
        var cost = new PhysicalCostService();
        _basicSolver = new TabSequenceSolver(mapper, cost);
        
        _testIndex = TestServices.CreateTempIndex();
        _advancedSolver = TestServices.CreateAdvancedTabSolver(_testIndex);
        _generator = TestServices.CreateGenerator();
        
        _pitchConverter = new TabToPitchConverter();
        _tokenizer = new TabTokenizer();
    }

    [TearDown]
    public void Teardown()
    {
        if (_testIndex != null && System.IO.File.Exists(_testIndex.FilePath))
            System.IO.File.Delete(_testIndex.FilePath);
    }

    [Test]
    public async Task Verify_AdvancedSolver_Blues_Match()
    {
        // 1. Seed Index with "Jazz" prototypes (The shapes we want to favor)
        // E7 (7-9-7-9) and A7 (7-5-6-5)
        await SeedStyle("E7", [7, 9, 7, 9], "Jazz");
        await SeedStyle("A7", [7, 5, 6, 5], "Jazz");

        var bluesRiff = @"
e|---------------|
B|---9---5-------|
G|---7---6-------|
D|---9---5-------|
A|---7---7-------|
E|---------------|
";
        var blocks = _tokenizer.Tokenize(bluesRiff);
        var slices = blocks.SelectMany(b => b.Slices).Where(s => s.Notes.Count > 0).ToList();
        var score = slices.Select(s => {
            var mNotes = _pitchConverter.GetMidiNotes(s);
            return mNotes.Select(m => {
                var octave = GA.Business.Core.Intervals.Octave.FromValue((m / 12) - 1);
                var pc = GA.Business.Core.Atonal.PitchClass.FromValue(m % 12);
                return (Pitch)new Pitch.Sharp(pc.ToSharpNote(), octave);
            });
        }).ToList();

        // 2. Solve with Advanced Solver
        var allPaths = await _advancedSolver.SolveAsync(score, "Jazz", k: 1);
        var solved = allPaths[0];

        // 3. Verify Harmonic Correctness and Playability (Looser than exact match)
        TestContext.WriteLine("=== Advanced Solver Verification (Blues Riff) ===");
        var physicalCost = new PhysicalCostService();

        for (int i = 0; i < solved.Count; i++)
        {
            // Convert TabSlice notes to FretboardPositions for comparison
            var expectedPositions = slices[i].Notes.Select(n => new FretboardPosition(
                Str.FromValue(6 - n.StringIndex), n.Fret, 
                Pitch.Sharp.Parse("C4")) // Pitch value not used by equivalence checker in this context? 
                // Wait, AreHarmonicallyEquivalent uses Pitch.MidiNote.Value.
                // So I need the ACTUAL pitches for the expected positions.
            ).ToList();
            
            // Re-fetch expected pitches correctly
            var mNotes = _pitchConverter.GetMidiNotes(slices[i]);
            var expectedWithPitches = new List<FretboardPosition>();
            var sortedOrigNotes = slices[i].Notes.OrderByDescending(n => n.StringIndex).ToList();
            for(int k=0; k<sortedOrigNotes.Count; k++) {
                int m = mNotes[k];
                var pc = GA.Business.Core.Atonal.PitchClass.FromValue(m % 12);
                var oct = GA.Business.Core.Intervals.Octave.FromValue((m/12)-1);
                expectedWithPitches.Add(new FretboardPosition(Str.FromValue(6 - sortedOrigNotes[k].StringIndex), sortedOrigNotes[k].Fret, new Pitch.Sharp(pc.ToSharpNote(), oct)));
            }

            // A. Harmonic Equivalence (Must match pitches and octaves)
            MusicalAssertions.AreHarmonicallyEquivalent(expectedWithPitches, solved[i]);

            // B. Playability (Must be ergonomic)
            MusicalAssertions.IsPlayable(solved[i], physicalCost, maxCost: 10.0);

            TestContext.WriteLine($"Step {i}: Verified (Harmonic Match + Playable)");
        }

        // C. Smoothness (Total sequence should be ergonomic)
        MusicalAssertions.IsSmoothSequence(solved, physicalCost);
    }

    private async Task SeedStyle(string name, int[] frets, string style)
    {
        var nonZero = frets.Where(f => f > 0).ToList();
        int min = nonZero.Count > 0 ? nonZero.Min() : 0;
        int max = nonZero.Count > 0 ? nonZero.Max() : 0;

        // Strings 5, 4, 3, 2 offsets
        int[] stringOffsets = [45, 50, 55, 59]; // A2, D3, G3, B3
        var midiNotes = new List<int>();
        for(int i=0; i<frets.Length; i++) {
            midiNotes.Add(stringOffsets[i] + frets[i]);
        }

        var doc = new VoicingDocument {
            Id = Guid.NewGuid().ToString(),
            ChordName = name,
            SemanticTags = [style],
            MidiNotes = midiNotes.ToArray(),
            PitchClasses = midiNotes.Select(m => m % 12).ToArray(),
            SearchableText = name,
            RootPitchClass = midiNotes[0] % 12,
            
            // Physical
            MinFret = min,
            MaxFret = max,
            HandStretch = max - min,

            PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = "", IntervalClassVector = "", AnalysisEngine = "", AnalysisVersion = "", Jobs = [], TuningId = "", PitchClassSetId = "0", Diagram = ""
        };
        doc = doc with { Embedding = await _generator.GenerateEmbeddingAsync(doc) };
        _testIndex.Add(doc);
    }

    [Test]
    public void Verify_BluesTurnaround_Rediscovery()
    {
        var bluesRiff = @"
e|---------------|
B|---9---5-------|
G|---7---6-------|
D|---9---5-------|
A|---7---7-------|
E|---------------|
";
        var blocks = _tokenizer.Tokenize(bluesRiff);
        var slices = blocks.SelectMany(b => b.Slices).Where(s => s.Notes.Count > 0).ToList();
        
        var score = slices.Select(s => {
            var mNotes = _pitchConverter.GetMidiNotes(s);
            return mNotes.Select(m => {
                var octaveValue = (m / 12) - 1;
                var octave = GA.Business.Core.Intervals.Octave.FromValue(octaveValue);
                var pc = GA.Business.Core.Atonal.PitchClass.FromValue(m % 12);
                return (Pitch)new Pitch.Sharp(pc.ToSharpNote(), octave);
            });
        }).ToList();

        var solved = _basicSolver.Solve(score);

        Assert.That(solved.Count, Is.EqualTo(slices.Count));

        TestContext.WriteLine("=== Original vs Basic Solved (Blues Riff) ===");
        for (int i = 0; i < solved.Count; i++)
        {
            var origNotes = slices[i].Notes.ToList();
            var solvedNotes = solved[i].ToList();

            bool match = true;
            foreach (var orig in origNotes)
            {
                int expectedStrValue = 6 - orig.StringIndex;
                var solvedForThisString = solvedNotes.FirstOrDefault(s => s.StringIndex.Value == expectedStrValue);
                if (solvedForThisString == null || solvedForThisString.Fret != orig.Fret) match = false;
            }
            TestContext.WriteLine($"Step {i}: Match={match}");
        }
    }
}