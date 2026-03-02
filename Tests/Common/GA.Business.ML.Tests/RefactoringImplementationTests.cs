namespace GA.Business.ML.Tests;

using ML.Tabs;
using TestInfrastructure;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using Core.Analysis.Voicings;
using Domain.Services.Fretboard.Voicings.Analysis;

[TestFixture]
public class RefactoringImplementationTests
{
    [SetUp]
    public void Setup()
    {
        var generator = TestServices.CreateGenerator();

        var tokenizer = new TabTokenizer();
        var converter = new TabToPitchConverter();
        _tabService = new(tokenizer, converter, generator);
    }

    private TabAnalysisService _tabService;

    [Test]
    public async Task Verify_VoicingHarmonicAnalyzer_Consolidation()
    {
        // Test C/E (First Inversion)
        // E (4) in Bass. C (0), G (7) above.
        // Tab: x32010 usually, or similar.
        // The one from failure log: e|0, B|1, G|0, D|2, A|3, E|0.
        // E(0) A(3-C) D(2-E) G(0-G) B(1-C) e(0-E) -> {4, 0, 4, 7, 0, 4} -> {0,4,7} C Major with E bass.
        var tab = @"
e|---0---|
B|---1---|
G|---0---|
D|---2---|
A|---3---|
E|---0---|
";

        var result = await _tabService.AnalyzeAsync(tab);
        var doc = result.Events.First().Document;

        // Verify Root Detection
        Assert.That(doc.RootPitchClass, Is.EqualTo(0), "Should detect Root C even with E in bass");

        // Verify Naming
        Assert.That(doc.ChordName, Does.Contain("C"), "Should be named C ...");
        Assert.That(doc.ChordName, Does.Contain("/E"), "Should indicate slash chord /E");

        // Verify Function
        Assert.That(doc.PossibleKeys, Does.Contain("Key of C"), "Should identify C Major key context");
        Assert.That(doc.HarmonicFunction, Is.Not.Null, "Should populate HarmonicFunction");
    }

    [Test]
    public void IdentifyChord_CorrectlyIdentifies_StandardTriads()
    {
        // Am (x02210) -> A, E, A, C, E -> {9, 4, 0}
        var am = Identify(
            [9, 4, 9, 0, 4], 
            9); // A bass
            
        Assert.That(am.RootPitchClass, Is.EqualTo("9"));
        Assert.That(am.Quality, Is.EqualTo("Minor"));
        
        // Verify Parsing (Crucial for TabAnalysisService integration)
        var parsed = PitchClass.TryParse(am.RootPitchClass, null, out var p);
        Assert.That(parsed, Is.True, "PitchClass.TryParse failed for '9'");
        Assert.That(p.Value, Is.EqualTo(9));

        // G (320003) -> G B D G B G -> {7, 11, 2}
        var g = Identify(
            [7, 11, 2],
            7); // G bass
            
        Assert.That(g.RootPitchClass, Is.EqualTo("7"));
        Assert.That(g.Quality, Is.EqualTo("Major"));

        // F (133211) -> F C F A C F -> {5, 0, 9}
        var f = Identify(
            [5, 0, 9],
            5); // F bass
            
        Assert.That(f.RootPitchClass, Is.EqualTo("5"));
        Assert.That(f.Quality, Is.EqualTo("Major"));
        
        // E (022100) -> E B E G# B E -> {4, 11, 8}
        var e = Identify(
            [4, 11, 8],
            4); // E bass
            
        Assert.That(e.RootPitchClass, Is.EqualTo("4"));
        Assert.That(e.Quality, Is.EqualTo("Major"));
    }

    private static ChordIdentification Identify(int[] notes, int bass)
    {
        var pcs = notes.Select(n => PitchClass.FromValue(n)).ToList();
        var set = new PitchClassSet(pcs);
        var bassPc = PitchClass.FromValue(bass);
        
        return VoicingHarmonicAnalyzer.IdentifyChord(set, pcs, bassPc);
    }
}
