namespace GA.Business.ML.Tests.Wavelets;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Wavelets;
using GA.Business.ML.Embeddings;
using NUnit.Framework;

[TestFixture]
public class WaveletMotionTests
{
    private ProgressionSignalService _signalService;
    private WaveletTransformService _waveletService;
    private ProgressionEmbeddingService _embeddingService;

    [SetUp]
    public void Setup()
    {
        _signalService = new ProgressionSignalService();
        _waveletService = new WaveletTransformService();
        _embeddingService = new ProgressionEmbeddingService(_signalService, _waveletService);
    }

    private VoicingDocument MockVoicing(int[] pcs, double consonance = 0.5, double entropy = 0.5)
    {
        var emb = new double[216];
        if (emb.Length > EmbeddingSchema.SpectralEntropy) emb[EmbeddingSchema.SpectralEntropy] = entropy;

        return new VoicingDocument
        {
            Id = Guid.NewGuid().ToString(),
            SearchableText = "",
            ChordName = "Test",
            Consonance = consonance,
            Embedding = emb,
            PitchClasses = pcs,
            MidiNotes = pcs.Select(p => 60 + p).ToArray(), // Assume Middle C octave
            
            // Required props
            RootPitchClass = pcs.Length > 0 ? pcs[0] : 0, 
            SemanticTags = [], PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = string.Join(",", pcs), IntervalClassVector = "000000", AnalysisEngine = "Test", AnalysisVersion = "1.0", Jobs = [], TuningId = "Standard", PitchClassSetId = "0", Diagram = "", BarreRequired = false, HandStretch = 0, MinFret = 0, MaxFret = 0
        };
    }

    [Test]
    public void Test_RisingTension_SignalDetection()
    {
        // Progression: C Major -> C7 -> C7b9 -> Cdim7
        // Tension should Rise. Stability should Fall.
        var progression = new List<VoicingDocument>
        {
            MockVoicing([0, 4, 7], consonance: 1.0), // C Major (Stable)
            MockVoicing([0, 4, 7, 10], consonance: 0.7), // C7
            MockVoicing([0, 4, 7, 10, 1], consonance: 0.4), // C7b9
            MockVoicing([0, 3, 6, 9], consonance: 0.2)  // Cdim7 (Unstable)
        };

        var signals = _signalService.ExtractSignals(progression);

        // Check Stability Fall
        Assert.That(signals.Stability[0], Is.GreaterThan(signals.Stability[3]));
        
        // Check Tension Rise
        Assert.That(signals.Tension[3], Is.GreaterThan(signals.Tension[0]));
        
        // Wavelet Decomposition should capture this Trend (Low Frequency energy)
        var decomp = _waveletService.Decompose(signals.Tension);
        var features = _waveletService.ExtractFeatures(decomp);
        
        Assert.That(features.Length, Is.EqualTo(16));
        Assert.That(features.Any(x => x != 0));
    }

    [Test]
    public void Test_StaticHarmony_ZeroVelocity()
    {
        // Same chord repeated 4 times
        var cMaj = new[] { 0, 4, 7 };
        var progression = Enumerable.Repeat(MockVoicing(cMaj, consonance: 1.0), 4).ToList();

        // Ensure embeddings are generated for distance calculation
        var generator = GA.Business.ML.Tests.TestInfrastructure.TestServices.CreateGenerator();
        for(int i=0; i<progression.Count; i++) {
            progression[i] = progression[i] with { Embedding = generator.GenerateEmbeddingAsync(progression[i]).GetAwaiter().GetResult() };
        }

        var signals = _signalService.ExtractSignals(progression);

        // Velocity should be 0 everywhere (distance between identical vectors)
        Assert.That(signals.Velocity.All(v => v < 0.001), Is.True);
        
        // Wavelet features for Velocity should be all zero
        var decomp = _waveletService.Decompose(signals.Velocity);
        var features = _waveletService.ExtractFeatures(decomp);
        Assert.That(features.All(f => Math.Abs(f) < 0.001), Is.True);
    }

    [Test]
    public void Test_Modulation_SpikeInVelocity()
    {
        // C Major -> C Major -> F# Major (Tritone Shift) -> F# Major
        var cMaj = new[] { 0, 4, 7 };
        var fsMaj = new[] { 6, 10, 1 };

        var progression = new List<VoicingDocument>
        {
            MockVoicing(cMaj, consonance: 1.0),
            MockVoicing(cMaj, consonance: 1.0),
            MockVoicing(fsMaj, consonance: 1.0), // Jump here
            MockVoicing(fsMaj, consonance: 1.0)
        };

        // Generate embeddings
        var generator = GA.Business.ML.Tests.TestInfrastructure.TestServices.CreateGenerator();
        for(int i=0; i<progression.Count; i++) {
            progression[i] = progression[i] with { Embedding = generator.GenerateEmbeddingAsync(progression[i]).GetAwaiter().GetResult() };
        }

        var signals = _signalService.ExtractSignals(progression);

        // Index 2: Distance(C, F#) should be significant
        Assert.That(signals.Velocity[2], Is.GreaterThan(0.5));
        Assert.That(signals.Velocity[1], Is.LessThan(0.001));
        
        var decomp = _waveletService.Decompose(signals.Velocity);
        double detailEnergy = decomp.DetailCoefficients.Sum(level => level.Sum(x => x*x));
        Assert.That(detailEnergy, Is.GreaterThan(0));
    }

    [Test]
    public void Test_TonalDrift_CircleOfFifths()
    {
        // C -> G -> D -> A (Circle of Fifths movement)
        var progression = new List<VoicingDocument>
        {
            MockVoicing([0, 4, 7]),  // C
            MockVoicing([7, 11, 2]), // G
            MockVoicing([2, 6, 9]),  // D
            MockVoicing([9, 1, 4])   // A
        };

        var signals = _signalService.ExtractSignals(progression);

        // Verify Tonal Drift is detected (non-zero)
        Assert.That(signals.TonalDrift.Any(d => d > 0.001), Is.True);
        
        var driftVariance = signals.TonalDrift.Max() - signals.TonalDrift.Min();
        Assert.That(driftVariance, Is.GreaterThan(0.001));
    }

    [Test]
    public void Test_AdaptiveLevels()
    {
        // Short signal (4 items) -> Level 1
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(4), Is.EqualTo(1));
        
        // Medium (8 items) -> Level 1? 
        // Logic: floor(log2(8)) - 2 = 3 - 2 = 1.
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(8), Is.EqualTo(1));
        
        // Long (16 items) -> floor(log2(16)) - 2 = 4 - 2 = 2.
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(16), Is.EqualTo(2));
        
        // Very Long (32 items) -> 5 - 2 = 3.
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(32), Is.EqualTo(3));
    }
}
