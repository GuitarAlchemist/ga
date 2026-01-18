namespace GA.Business.ML.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Tabs;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Wavelets;
using NUnit.Framework;

[TestFixture]
public class EndToEndTabAnalysisTests
{
    private TabTokenizer _tokenizer = null!;
    private TabToPitchConverter _converter = null!;
    private MusicalEmbeddingGenerator _generator = null!;
    private TabAnalysisService _tabService = null!;
    private ProgressionSignalService _signalService = null!;
    private WaveletTransformService _waveletService = null!;

    [SetUp]
    public void Setup()
    {
        _generator = TestInfrastructure.TestServices.CreateGenerator();
        _tokenizer = new TabTokenizer();
        _converter = new TabToPitchConverter();
        _tabService = new TabAnalysisService(_tokenizer, _converter, _generator);
        _signalService = new ProgressionSignalService();
        _waveletService = new WaveletTransformService();
    }

    [Test]
    public async Task Analyze_StairwayToHeaven_Intro()
    {
        // Stairway to Heaven Intro (Approximate Tab)
        // Am -> Am(maj7)/G# -> Am7/G -> D/F# -> Fmaj7
        // Chromatic descent: A(5) -> G#(4) -> G(3) -> F#(2) -> F(1) on D string (or equivalent)
        
        var tab = @"
e|-------5-7-----7-8-----8-2-----2-0---------0-----------------|
B|-----5-----5-------5-------3-------1-----1---1---------------|
G|---5---------5-------5-------2-------2---------2-------------|
D|-7-------6-------5-------4-------3-------2-------------------|
A|-------------------------------------------------------------|
E|-------------------------------------------------------------|
";
        // Step 1: Parse & Analyze Chords
        var result = await _tabService.AnalyzeAsync(tab);
        
        Assert.That(result.Events.Count, Is.GreaterThan(10), "Should detect multiple notes/chords");

        // The tab parser emits *slices*. Stairway is arpeggiated.
        // So we get single notes or dyads.
        // However, the *Motion* analysis should still detect the chromatic shift in the D string.
        
        // Extract Progression (Sequence of Voicings)
        var progression = result.Events.Select(e => e.Document).ToList();

        // Step 2: Extract Signals
        var signals = _signalService.ExtractSignals(progression);

        // Step 3: Verify Motion Features
        
        // A. Chromatic Descent Check
        // The D string notes are 7(A), 6(G#), 5(G), 4(F#), 3(F).
        // This is a smooth linear motion in pitch space.
        // Spectral Velocity between adjacent steps should be consistent and relatively small (stepwise).
        // If there was a modulation (key change), we'd see a spike.
        
        // Let's verify we detected valid signals
        Assert.That(signals.Velocity.Length, Is.EqualTo(progression.Count));
        
        // B. Tension Analysis
        // Am(maj7) [G#] is more dissonant than Am [A].
        // So Tension should rise when we hit the G#.
        // Slice 0-3: Am (A, C, E, A). Stable.
        // Slice 4-7: Am(maj7) (G#, C, E, A). Tension!
        
        // Find indices roughly (assuming 4 notes per chord approx)
        // Note: The parser might group simultaneous notes. This tab is arpeggiated, so likely 1 note per slice.
        // D-7 (A) starts at event 0.
        // D-6 (G#) starts around event 4.
        
        // Let's inspect average tension in first "measure" vs second.
        // Measure 1 (Am): Low Tension.
        // Measure 2 (Am/G#): Higher Tension.
        
        // Calculate average tension for first few events vs next few
        // Assuming ~4 events per shape
        if (signals.Tension.Length >= 8)
        {
            var tensionAm = signals.Tension.Take(4).Average();
            var tensionAmMaj7 = signals.Tension.Skip(4).Take(4).Average();
            
            TestContext.WriteLine($"Am Tension: {tensionAm:F3}");
            TestContext.WriteLine($"Am(maj7) Tension: {tensionAmMaj7:F3}");
            
            // Verify Tension Rise
            // Note: Consonance of Minor triad is high. Consonance of min(maj7) is lower.
            // Tension = 1 - Consonance.
            // So AmMaj7 tension > Am tension.
            Assert.That(tensionAmMaj7, Is.GreaterThan(tensionAm), "Tension should rise with G# (Major 7th interval)");
        }

        // Step 4: Wavelet Analysis
        var decomp = _waveletService.Decompose(signals.Tension);
        var features = _waveletService.ExtractFeatures(decomp);
        
        // Just verify we got a valid feature vector
        Assert.That(features.Length, Is.EqualTo(16));
        
        TestContext.WriteLine("Wavelet Analysis complete.");
    }
}
