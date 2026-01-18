namespace GA.Business.Core.Tests.Embeddings;

using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using NUnit.Framework;

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
            new SymbolicVectorService(),
            new PhaseSphereService()
        );
    }

    [Test]
    public void EmbeddingSchema_Version_Is_V14()
    {
        // Arrange & Act
        var version = EmbeddingSchema.Version;
        var totalDim = EmbeddingSchema.TotalDimension;

        // Assert
        TestContext.WriteLine($"Verifying Schema Version: Expected=OPTIC-K-v1.4, Actual={version} (Ensures consistency with the latest defined specification)");
        TestContext.WriteLine($"Total Dimension: Expected=216, Actual={totalDim} (The fixed length of the OPTIC-K-v1.4 feature vector)");

        Assert.Multiple(() =>
        {
            Assert.That(version, Is.EqualTo("OPTIC-K-v1.4"), "The schema version must match the expected constant.");
            Assert.That(totalDim, Is.EqualTo(216), "The total dimension of the embedding vector must be exactly 216.");
        });
    }

    [Test]
    public async Task SpectralEntropy_CleanTriad_HigherThan_SingleNote()
    {
        // Arrange
        // Single Note (Impulse in time = Flat spectrum) -> Max Entropy -> Result ~ 0.0
        var singleDoc = CreateTestDocument(midiNotes: [60]);
        // Major Triad (Some peaks k=3,4,5) -> Lower Entropy -> Result > 0.0
        var triadDoc = CreateTestDocument(midiNotes: [60, 64, 67]); // C, E, G

        // Act
        var singleEmb = await _generator.GenerateEmbeddingAsync(singleDoc);
        var triadEmb = await _generator.GenerateEmbeddingAsync(triadDoc);

        var singleEntropy = singleEmb[EmbeddingSchema.SpectralEntropy];
        var triadEntropy = triadEmb[EmbeddingSchema.SpectralEntropy];

        // Assert
        TestContext.WriteLine($"Single Note (MIDI 60) Entropy Score: {singleEntropy:F4}");
        TestContext.WriteLine($"C Major Triad (60, 64, 67) Entropy Score: {triadEntropy:F4}");
        TestContext.WriteLine($"Comparison: Expected {triadEntropy:F4} > {singleEntropy:F4} + 0.1, Actual Result={triadEntropy > singleEntropy + 0.1} (Triads have more organized spectral peaks than a flat impulse spectrum)");

        // Expect Triad (Structured) to have higher "Organized" score than Single Note (Flat/MaxEntropy)
        Assert.That(triadEntropy, Is.GreaterThan(singleEntropy + 0.1),
            $"Triad entropy score ({triadEntropy:F4}) should be significantly higher than single note ({singleEntropy:F4}) because triads are more spectrally organized.");
    }

    [Test]
    public async Task SalientChroma_Doubling_ChangesSpectrum()
    {
        // Arrange
        // Case A: C Major Triad (shell) - {C, E, G} -> Chroma [1, 0, 0, 0, 1, 0, 0, 1, ...]
        var docA = CreateTestDocument(midiNotes: [48, 52, 55]);     // C, E, G
        // Case B: C Major with doubled Root - {C, C, C, E, G} -> Chroma [3, 0, 0, 0, 1, 0, 0, 1, ...]
        var docB = CreateTestDocument(midiNotes: [48, 60, 72, 52, 55]); // C, C, C, E, G

        // Act
        var embA = await _generator.GenerateEmbeddingAsync(docA);
        var embB = await _generator.GenerateEmbeddingAsync(docB);

        // Assert
        // Compare Fourier Magnitudes (indices 96-101)
        bool anyDifference = false;
        TestContext.WriteLine("Comparing Fourier Magnitudes for shell vs doubled voicing:");
        for (int i = 96; i <= 101; i++)
        {
            var diff = Math.Abs(embA[i] - embB[i]);
            TestContext.WriteLine($"  Index {i}: {embA[i]:F4} vs {embB[i]:F4} (diff: {diff:F4})");
            if (diff > 0.001)
            {
                anyDifference = true;
            }
        }

        Assert.That(anyDifference, Is.True,
            "Doubling notes must affect at least one Fourier magnitude when using Salient Chroma");
    }

    [Test]
    public async Task HarmonicInertia_StableChord_HighValue()
    {
        // Arrange
        // High consonance = high stability = high inertia
        var stableDoc = CreateTestDocument(midiNotes: [48, 52, 55], consonance: 0.9);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(stableDoc);
        var inertia = embedding[EmbeddingSchema.HarmonicInertia];

        // Assert
        TestContext.WriteLine($"Stable Chord Consonance: 0.9, Harmonic Inertia: {inertia}");
        Assert.That(inertia, Is.GreaterThan(0.7), "Stable chord should have high inertia");
    }

    [Test]
    public async Task ResolutionPressure_TenseChord_HighValue()
    {
        // Arrange
        // Low consonance = high tension = high resolution pressure
        var tenseDoc = CreateTestDocument(midiNotes: [48, 54, 60], consonance: 0.2);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(tenseDoc);
        var pressure = embedding[EmbeddingSchema.ResolutionPressure];

        // Assert
        TestContext.WriteLine($"Tense Chord Consonance: 0.2, Resolution Pressure: {pressure}");
        Assert.That(pressure, Is.GreaterThan(0.5), "Tense chord should have high resolution pressure");
    }

    [Test]
    public async Task DoublingRatio_DoubledNotes_PositiveValue()
    {
        // Arrange
        // 4 notes, 3 unique pitch classes = doubling ratio of 0.25
        var doc = CreateTestDocument(midiNotes: [48, 52, 55, 60], pitchClasses: [0, 4, 7, 0]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        var doublingRatio = embedding[EmbeddingSchema.TexturalDoublingRatio];

        // Assert
        TestContext.WriteLine($"Doubled Notes: [48, 52, 55, 60], Doubling Ratio: {doublingRatio}");
        Assert.That(doublingRatio, Is.GreaterThan(0.0), "Doubled notes should have positive doubling ratio");
    }

    [Test]
    public async Task RootDoubled_WhenRootAppearsMultipleTimes_ReturnsOne()
    {
        // Arrange
        // Root (C=0) appears twice
        var doc = CreateTestDocument(midiNotes: [48, 52, 55, 60], pitchClasses: [0, 4, 7, 0], rootPitchClass: 0);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        var rootDoubled = embedding[EmbeddingSchema.TexturalRootDoubled];

        // Assert
        TestContext.WriteLine($"Root: 0, Pitch Classes: [0, 4, 7, 0], Root Doubled Flag: Expected=1.0, Actual={rootDoubled} (Root C=0 is present twice in the voicing)");
        Assert.That(rootDoubled, Is.EqualTo(1.0), "Root doubled flag should be 1.0 when the root pitch class appears more than once.");
    }

    [Test]
    public async Task BassMelodySpan_WideVoicing_HighValue()
    {
        // Arrange
        // Wide span: E2 (40) to E4 (64) = 24 semitones
        var wideDoc = CreateTestDocument(midiNotes: [40, 52, 64]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(wideDoc);
        var span = embedding[EmbeddingSchema.ExtendedBassMelodySpan];

        // Assert
        TestContext.WriteLine($"Wide Voicing Span (40 to 64): {span}");
        Assert.That(span, Is.GreaterThan(0.4), "Wide voicing should have high bass-melody span");
    }

    [Test]
    public async Task OpenPosition_SpanGreaterThanOctave_ReturnsOne()
    {
        // Arrange
        // Span > 12 semitones
        var openDoc = CreateTestDocument(midiNotes: [40, 47, 55, 64]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(openDoc);
        var openPosition = embedding[EmbeddingSchema.ExtendedOpenPosition];

        // Assert
        TestContext.WriteLine($"Open Voicing Span: {openDoc.MidiNotes.Max() - openDoc.MidiNotes.Min()} semitones, Open Position Flag: {openPosition}");
        Assert.That(openPosition, Is.EqualTo(1.0), "Open position flag should be 1.0 for wide voicing");
    }

    [Test]
    public async Task OpenPosition_SpanWithinOctave_ReturnsZero()
    {
        // Arrange
        // Span <= 12 semitones
        var closedDoc = CreateTestDocument(midiNotes: [60, 64, 67]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(closedDoc);
        var openPosition = embedding[EmbeddingSchema.ExtendedOpenPosition];

        // Assert
        TestContext.WriteLine($"Closed Voicing Span: {closedDoc.MidiNotes.Max() - closedDoc.MidiNotes.Min()} semitones, Open Position Flag: {openPosition}");
        Assert.That(openPosition, Is.EqualTo(0.0), "Closed position flag should be 0.0");
    }

    [Test]
    public async Task ThirdDoubled_WhenThirdAppearsMultipleTimes_ReturnsOne()
    {
        // Arrange
        // C chord with E doubled: C, E, G, E
        var doc = CreateTestDocument(
            midiNotes: [48, 52, 55, 64],
            pitchClasses: [0, 4, 7, 4], // C, E, G, E
            rootPitchClass: 0
        );

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        var thirdDoubled = embedding[EmbeddingSchema.ExtendedThirdDoubled];

        // Assert
        TestContext.WriteLine($"Root: 0, Pitch Classes: [0, 4, 7, 4], Third Doubled Flag: {thirdDoubled}");
        Assert.That(thirdDoubled, Is.EqualTo(1.0), "Third doubled flag should be 1.0");
    }

    [Test]
    public async Task FifthDoubled_PowerChord_ReturnsOne()
    {
        // Arrange
        // Power chord: C, G, C, G (root and 5th doubled)
        var doc = CreateTestDocument(
            midiNotes: [48, 55, 60, 67],
            pitchClasses: [0, 7, 0, 7],
            rootPitchClass: 0
        );

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        var fifthDoubled = embedding[EmbeddingSchema.ExtendedFifthDoubled];

        // Assert
        TestContext.WriteLine($"Root: 0, Pitch Classes: [0, 7, 0, 7], Fifth Doubled Flag: {fifthDoubled}");
        Assert.That(fifthDoubled, Is.EqualTo(1.0), "Fifth doubled flag should be 1.0 for power chord");
    }

    [Test]
    public async Task OmittedRoot_RootlessVoicing_ReturnsOne()
    {
        // Arrange
        // Rootless m7 voicing: 3rd, 5th, 7th (no root)
        var rootlessDoc = CreateTestDocument(
            midiNotes: [52, 55, 59],
            pitchClasses: [4, 7, 11], // E, G, B (no C)
            rootPitchClass: 0 // Root would be C
        );

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(rootlessDoc);
        var omittedRoot = embedding[EmbeddingSchema.ExtendedOmittedRoot];

        // Assert
        TestContext.WriteLine($"Root: 0, Pitch Classes: [4, 7, 11], Omitted Root Flag: {omittedRoot}");
        Assert.That(omittedRoot, Is.EqualTo(1.0), "Omitted root flag should be 1.0");
    }

    [Test]
    public async Task InnerVoiceDensity_MidRangeVoicing_HighValue()
    {
        // Arrange
        // All notes in mid-range (between E3=52 and E5=76)
        var midDoc = CreateTestDocument(midiNotes: [55, 60, 64, 67]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(midDoc);
        var innerDensity = embedding[EmbeddingSchema.ExtendedInnerVoiceDensity];

        // Assert
        TestContext.WriteLine($"Mid-range Voicing: [55, 60, 64, 67], Inner Voice Density: {innerDensity}");
        Assert.That(innerDensity, Is.GreaterThan(0.5), "Mid-range voicing should have high inner voice density");
    }

    [Test]
    public async Task LocalClustering_ClusterVoicing_HighValue()
    {
        // Arrange
        // Cluster chord: C, D, E (adjacent semitones)
        var clusterDoc = CreateTestDocument(midiNotes: [60, 62, 64]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(clusterDoc);
        var clustering = embedding[EmbeddingSchema.SpectralLocalClustering];

        // Assert
        TestContext.WriteLine($"Cluster Voicing: [60, 62, 64], Local Clustering: {clustering}");
        Assert.That(clustering, Is.GreaterThan(0.5), "Cluster voicing should have high local clustering");
    }

    [Test]
    public async Task RoughnessProxy_LowCluster_HighValue()
    {
        // Arrange
        // Low cluster: E2, F2, F#2 (dissonant in low register)
        var lowClusterDoc = CreateTestDocument(midiNotes: [40, 41, 42]);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(lowClusterDoc);
        var roughness = embedding[EmbeddingSchema.SpectralRoughnessProxy];

        // Assert
        TestContext.WriteLine($"Low Cluster Voicing: [40, 41, 42], Roughness Proxy: {roughness}");
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
