namespace GA.Business.Core.Tests.Embeddings;

using GA.Business.Core.AI.Embeddings;
using GA.Business.Core.Fretboard.Voicings.Search;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Tests for v1.2.1 Extended Texture Features (indices 78-95).
/// </summary>
[TestFixture]
public class ExtendedTextureFeatureTests
{
    private MusicalEmbeddingGenerator _generator;

    [SetUp]
    public void SetUp()
    {
        _generator = new MusicalEmbeddingGenerator(
            new IdentityVectorService(),
            new TheoryVectorService(),
            new MorphologyVectorService(),
            new ContextVectorService(),
            new SymbolicVectorService()
        );
    }

    [Test]
    public void EmbeddingSchema_Version_Is_V121()
    {
        Assert.That(EmbeddingSchema.Version, Is.EqualTo("OPTIC-K-v1.2.1"));
        Assert.That(EmbeddingSchema.TotalDimension, Is.EqualTo(96));
    }

    [Test]
    public async Task GenerateEmbedding_Returns96Dimensions()
    {
        var doc = CreateTestDocument(midiNotes: [40, 47, 52, 56]);
        
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        
        Assert.That(embedding.Length, Is.EqualTo(96));
    }

    [Test]
    public async Task HarmonicInertia_StableChord_HighValue()
    {
        // High consonance = high stability = high inertia
        var stableDoc = CreateTestDocument(midiNotes: [48, 52, 55], consonance: 0.9);
        var embedding = await _generator.GenerateEmbeddingAsync(stableDoc);
        
        var inertia = embedding[EmbeddingSchema.HarmonicInertia];
        Assert.That(inertia, Is.GreaterThan(0.7), "Stable chord should have high inertia");
    }

    [Test]
    public async Task ResolutionPressure_TenseChord_HighValue()
    {
        // Low consonance = high tension = high resolution pressure
        var tenseDoc = CreateTestDocument(midiNotes: [48, 54, 60], consonance: 0.2);
        var embedding = await _generator.GenerateEmbeddingAsync(tenseDoc);
        
        var pressure = embedding[EmbeddingSchema.ResolutionPressure];
        Assert.That(pressure, Is.GreaterThan(0.5), "Tense chord should have high resolution pressure");
    }

    [Test]
    public async Task DoublingRatio_DoubledNotes_PositiveValue()
    {
        // 4 notes, 3 unique pitch classes = doubling ratio of 0.25
        var doc = CreateTestDocument(midiNotes: [48, 52, 55, 60], pitchClasses: [0, 4, 7, 0]);
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        
        var doublingRatio = embedding[EmbeddingSchema.Textural_DoublingRatio];
        Assert.That(doublingRatio, Is.GreaterThan(0.0), "Doubled notes should have positive doubling ratio");
    }

    [Test]
    public async Task RootDoubled_WhenRootAppearsMultipleTimes_ReturnsOne()
    {
        // Root (C=0) appears twice
        var doc = CreateTestDocument(midiNotes: [48, 52, 55, 60], pitchClasses: [0, 4, 7, 0], rootPitchClass: 0);
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        
        var rootDoubled = embedding[EmbeddingSchema.Textural_RootDoubled];
        Assert.That(rootDoubled, Is.EqualTo(1.0), "Root doubled flag should be 1.0");
    }

    [Test]
    public async Task BassMelodySpan_WideVoicing_HighValue()
    {
        // Wide span: E2 (40) to E4 (64) = 24 semitones
        var wideDoc = CreateTestDocument(midiNotes: [40, 52, 64]);
        var embedding = await _generator.GenerateEmbeddingAsync(wideDoc);
        
        var span = embedding[EmbeddingSchema.Extended_BassMelodySpan];
        Assert.That(span, Is.GreaterThan(0.4), "Wide voicing should have high bass-melody span");
    }

    [Test]
    public async Task OpenPosition_SpanGreaterThanOctave_ReturnsOne()
    {
        // Span > 12 semitones
        var openDoc = CreateTestDocument(midiNotes: [40, 47, 55, 64]);
        var embedding = await _generator.GenerateEmbeddingAsync(openDoc);
        
        var openPosition = embedding[EmbeddingSchema.Extended_OpenPosition];
        Assert.That(openPosition, Is.EqualTo(1.0), "Open position flag should be 1.0 for wide voicing");
    }

    [Test]
    public async Task OpenPosition_SpanWithinOctave_ReturnsZero()
    {
        // Span <= 12 semitones
        var closedDoc = CreateTestDocument(midiNotes: [60, 64, 67]);
        var embedding = await _generator.GenerateEmbeddingAsync(closedDoc);
        
        var openPosition = embedding[EmbeddingSchema.Extended_OpenPosition];
        Assert.That(openPosition, Is.EqualTo(0.0), "Closed position flag should be 0.0");
    }

    [Test]
    public async Task ThirdDoubled_WhenThirdAppearsMultipleTimes_ReturnsOne()
    {
        // C chord with E doubled: C, E, G, E
        var doc = CreateTestDocument(
            midiNotes: [48, 52, 55, 64], 
            pitchClasses: [0, 4, 7, 4], // C, E, G, E
            rootPitchClass: 0
        );
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        
        var thirdDoubled = embedding[EmbeddingSchema.Extended_ThirdDoubled];
        Assert.That(thirdDoubled, Is.EqualTo(1.0), "Third doubled flag should be 1.0");
    }

    [Test]
    public async Task FifthDoubled_PowerChord_ReturnsOne()
    {
        // Power chord: C, G, C, G (root and 5th doubled)
        var doc = CreateTestDocument(
            midiNotes: [48, 55, 60, 67], 
            pitchClasses: [0, 7, 0, 7],
            rootPitchClass: 0
        );
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        
        var fifthDoubled = embedding[EmbeddingSchema.Extended_FifthDoubled];
        Assert.That(fifthDoubled, Is.EqualTo(1.0), "Fifth doubled flag should be 1.0 for power chord");
    }

    [Test]
    public async Task OmittedRoot_RootlessVoicing_ReturnsOne()
    {
        // Rootless m7 voicing: 3rd, 5th, 7th (no root)
        var rootlessDoc = CreateTestDocument(
            midiNotes: [52, 55, 59],
            pitchClasses: [4, 7, 11], // E, G, B (no C)
            rootPitchClass: 0 // Root would be C
        );
        var embedding = await _generator.GenerateEmbeddingAsync(rootlessDoc);
        
        var omittedRoot = embedding[EmbeddingSchema.Extended_OmittedRoot];
        Assert.That(omittedRoot, Is.EqualTo(1.0), "Omitted root flag should be 1.0");
    }

    [Test]
    public async Task InnerVoiceDensity_MidRangeVoicing_HighValue()
    {
        // All notes in mid-range (between E3=52 and E5=76)
        var midDoc = CreateTestDocument(midiNotes: [55, 60, 64, 67]);
        var embedding = await _generator.GenerateEmbeddingAsync(midDoc);
        
        var innerDensity = embedding[EmbeddingSchema.Extended_InnerVoiceDensity];
        Assert.That(innerDensity, Is.GreaterThan(0.5), "Mid-range voicing should have high inner voice density");
    }

    [Test]
    public async Task LocalClustering_ClusterVoicing_HighValue()
    {
        // Cluster chord: C, D, E (adjacent semitones)
        var clusterDoc = CreateTestDocument(midiNotes: [60, 62, 64]);
        var embedding = await _generator.GenerateEmbeddingAsync(clusterDoc);
        
        var clustering = embedding[EmbeddingSchema.Spectral_LocalClustering];
        Assert.That(clustering, Is.GreaterThan(0.5), "Cluster voicing should have high local clustering");
    }

    [Test]
    public async Task RoughnessProxy_LowCluster_HighValue()
    {
        // Low cluster: E2, F2, F#2 (dissonant in low register)
        var lowClusterDoc = CreateTestDocument(midiNotes: [40, 41, 42]);
        var embedding = await _generator.GenerateEmbeddingAsync(lowClusterDoc);
        
        var roughness = embedding[EmbeddingSchema.Spectral_RoughnessProxy];
        Assert.That(roughness, Is.GreaterThan(0.5), "Low cluster should have high roughness");
    }

    private VoicingDocument CreateTestDocument(
        int[] midiNotes,
        int[]? pitchClasses = null,
        int? rootPitchClass = null,
        double consonance = 0.5)
    {
        pitchClasses ??= midiNotes.Select(m => m % 12).ToArray();
        var pcsString = "{" + string.Join(",", pitchClasses.Distinct().OrderBy(p => p)) + "}";
        
        return new VoicingDocument
        {
            Id = "test",
            SearchableText = "Test chord",
            ChordName = "Test",
            Diagram = "x-x-x-x-x-x",
            MidiNotes = midiNotes,
            PitchClasses = pitchClasses,
            PitchClassSet = pcsString,
            PitchClassSetId = "test-pcs",
            IntervalClassVector = "000000",
            RootPitchClass = rootPitchClass ?? pitchClasses.FirstOrDefault(),
            MidiBassNote = midiNotes.Min(),
            Consonance = consonance,
            Brightness = 0.5,
            HarmonicFunction = "Tonic",
            MinFret = 0,
            MaxFret = 4,
            HandStretch = 4,
            BarreRequired = false,
            IsRootless = rootPitchClass.HasValue && !pitchClasses.Contains(rootPitchClass.Value),
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "",
            TuningId = "standard",
            AnalysisEngine = "test",
            AnalysisVersion = "1.0",
            Jobs = []
        };
    }
}
