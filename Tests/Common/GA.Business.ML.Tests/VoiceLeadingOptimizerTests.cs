namespace GA.Business.ML.Tests;

using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Tabs;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Tests.TestInfrastructure;
using GA.Business.Core.Fretboard.Voicings.Search;

[TestFixture]
public class VoiceLeadingOptimizerTests
{
    private VoiceLeadingOptimizer _optimizer;
    private FileBasedVectorIndex _index;

    [SetUp]
    public async Task Setup()
    {
        var services = await TestServices.CreateAsync();
        _index = services.Index;
        var solver = TestServices.CreateAdvancedTabSolver(_index);
        _optimizer = new VoiceLeadingOptimizer(solver, _index);
    }

    [Test]
    public async Task Optimize_JumpyProgression_ReturnsSmoothPath()
    {
        // 1. Create a "Jumpy" progression: C Major (Open) -> G Major (High)
        var cMaj = new VoicingDocument
        {
            Id = "c", ChordName = "C Major", MidiNotes = new[] { 48, 52, 55 }, 
            PitchClasses = new[] { 0, 4, 7 }, SearchableText = "C", SemanticTags = new[]{"test"},
            PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = "0,4,7", IntervalClassVector = "000000", AnalysisEngine = "Test", AnalysisVersion = "1.0", Jobs = [], TuningId = "Standard", PitchClassSetId = "0", Diagram = ""
        };
        var gMaj = new VoicingDocument
        {
            Id = "g", ChordName = "G Major", MidiNotes = new[] { 55, 59, 62 }, // G3, B3, D4
            PitchClasses = new[] { 7, 11, 2 }, SearchableText = "G", SemanticTags = new[]{"test"},
            PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = "7,11,2", IntervalClassVector = "000000", AnalysisEngine = "Test", AnalysisVersion = "1.0", Jobs = [], TuningId = "Standard", PitchClassSetId = "0", Diagram = ""
        };

        var progression = new List<VoicingDocument> { cMaj, gMaj };

        // 2. Optimize
        var result = await _optimizer.OptimizeAsync(progression, "Jazz");

        TestContext.WriteLine("Optimized Path:");
        foreach (var step in result)
        {
            TestContext.WriteLine($"  Chord:");
            foreach (var p in step) TestContext.WriteLine($"    String {p.StringIndex.Value}, Fret {p.Fret}");
        }

        Assert.That(result.Count, Is.EqualTo(2));
        // Verify smoothness: C Maj (frets 3,2,0) -> G Maj should be near frets 3-5, NOT 10-15
        var gFrets = result[1].Select(p => p.Fret).ToList();
        Assert.That(gFrets.Average(), Is.LessThan(7), "Should find a smooth realization near the nut");
    }
}
