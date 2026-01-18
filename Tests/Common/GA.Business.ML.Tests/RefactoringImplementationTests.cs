namespace GA.Business.ML.Tests;

using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Tabs;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using NUnit.Framework;

[TestFixture]
public class RefactoringImplementationTests
{
    private TabAnalysisService _tabService;

    [SetUp]
    public void Setup()
    {
        var generator = TestInfrastructure.TestServices.CreateGenerator();

        var tokenizer = new TabTokenizer();
        var converter = new TabToPitchConverter();
        _tabService = new TabAnalysisService(tokenizer, converter, generator);
    }

    [Test]
    public async Task Verify_VoicingHarmonicAnalyzer_Consolidation()
    {
        // Test C/E (First Inversion)
        // E (4) in Bass. C (0), G (7) above.
        var tab = @"
e|-------|
B|-------|
G|---0---|
D|---2---|
A|---3---|
E|---0---|
"; 
        // E-0 = 40 (E2). 
        // A-3 = 48 (C3).
        // D-2 = 52 (E3).
        // G-0 = 55 (G3).
        // Notes: E, C, E, G.
        // Bass: E. Root: C.
        // The old logic would guess Root = Bass (E) -> Em#5? 
        // VoicingHarmonicAnalyzer should identify it as C Major / E.

        var result = await _tabService.AnalyzeAsync(tab);
        var doc = result.Events.First().Document;

        // Verify Root Detection
        Assert.That(doc.RootPitchClass, Is.EqualTo(0), "Should detect Root C even with E in bass");
        
        // Verify Naming
        Assert.That(doc.ChordName, Does.Contain("C"), "Should be named C Major");
        Assert.That(doc.ChordName, Does.Contain("/E"), "Should indicate slash chord /E");

        // Verify Function (if possible to test without Key context, usually defaults to something or null)
        // HarmonicFunction logic usually requires a Key context passed in, 
        // but IdentifyChord finds the "ClosestKey".
        // C/E in C Major -> Tonic (I).
        Assert.That(doc.PossibleKeys, Does.Contain("Key of C"), "Should identify C Major key context");
        Assert.That(doc.HarmonicFunction, Is.Not.Null, "Should populate HarmonicFunction");
    }
}
