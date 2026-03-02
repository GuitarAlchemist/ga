namespace GA.Business.ML.Tests.Integration;

using Embeddings;
using Embeddings.Validation;
using Rag.Models;
using TestInfrastructure;

/// <summary>
///     Tests that validate the OPTIC-K embedding schema's partition-based retrieval.
///     These tests verify that similar musical concepts retrieve each other correctly
///     when searching by specific embedding dimensions.
/// </summary>
[TestFixture]
[Category("Validation")]
[Category("OPTIC-K")]
public class EmbeddingSchemaValidationTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _generator = TestServices.CreateGenerator();
        _index = new();

        // Seed the index with test voicings
        await SeedTestVoicingsAsync();
    }

    private MusicalEmbeddingGenerator _generator = null!;
    private PartitionAwareRagIndex _index = null!;

    private async Task<float[]> GenerateEmbeddingAsync(ChordVoicingRagDocument doc)
    {
        var doubleArray = await _generator.GenerateEmbeddingAsync(doc);
        return [.. doubleArray.Select(d => (float)d)];
    }

    [Test]
    public void Index_ContainsExpectedDocuments()
    {
        Assert.That(_index.Count, Is.GreaterThan(0), "Index should have documents");
        TestContext.WriteLine($"Index contains {_index.Count} documents");
    }

    [Test]
    public async Task SameDocument_HasPerfectSimilarity()
    {
        // Arrange
        var doc = CreateCMajorOpenVoicing();
        var embedding = await GenerateEmbeddingAsync(doc);

        // Act
        var breakdown = _index.ComputeSimilarityBreakdown(embedding, embedding);

        // Assert
        Assert.That(breakdown.WeightedOverall, Is.EqualTo(1.0).Within(0.001));
        foreach (var (partition, similarity) in breakdown.PartitionScores)
        {
            TestContext.WriteLine($"{partition}: {similarity:F4}");
            Assert.That(similarity, Is.EqualTo(1.0).Within(0.001), $"{partition} should have perfect self-similarity");
        }
    }

    [Test]
    public async Task Structure_SimilarChordsSharePitchClasses()
    {
        // Arrange - C Major in different positions should have same STRUCTURE
        var cMajorOpen = CreateCMajorOpenVoicing();
        var cMajorBarre = CreateCMajorBarreVoicing();

        var embOpen = await GenerateEmbeddingAsync(cMajorOpen);
        var embBarre = await GenerateEmbeddingAsync(cMajorBarre);

        // Act
        var breakdown = _index.ComputeSimilarityBreakdown(embOpen, embBarre);

        // Assert - Structure (pitch classes) should be very similar
        var structureSim = breakdown.PartitionScores["Structure"];
        TestContext.WriteLine($"Structure similarity (same chord, different positions): {structureSim:F4}");
        Assert.That(structureSim, Is.GreaterThan(0.9),
            "Same chord type should have high structure similarity");
    }

    [Test]
    public async Task Morphology_DiffersByPosition()
    {
        // Arrange - Same chord, different fretboard positions
        var cMajorOpen = CreateCMajorOpenVoicing();
        var cMajorBarre = CreateCMajorBarreVoicing();

        var embOpen = await GenerateEmbeddingAsync(cMajorOpen);
        var embBarre = await GenerateEmbeddingAsync(cMajorBarre);

        // Act
        var breakdown = _index.ComputeSimilarityBreakdown(embOpen, embBarre);

        // Assert - Morphology should differ (different positions)
        var morphologySim = breakdown.PartitionScores["Morphology"];
        TestContext.WriteLine($"Morphology similarity (same chord, different positions): {morphologySim:F4}");
        Assert.That(morphologySim, Is.LessThan(0.95),
            "Different positions should have different morphology");
    }

    [Test]
    public async Task RetrievalByStructure_FindsSameChordType()
    {
        // Arrange
        var queryDoc = CreateCMajorOpenVoicing();
        var queryEmb = await GenerateEmbeddingAsync(queryDoc);

        // Act
        var results = _index.SearchByPartition(queryEmb, OpticKPartitions.Structure, 5);

        // Assert
        TestContext.WriteLine("Top 5 results by Structure:");
        foreach (var result in results)
        {
            TestContext.WriteLine($"  {result.Document.ChordName}: {result.Similarity:F4}");
        }

        // Other major chords should rank high
        var topChordNames = results.Take(3).Select(r => r.Document.ChordName).ToList();
        Assert.That(topChordNames.Any(n => n?.Contains("Major") == true),
            "Top results should include Major chords");
    }

    [Test]
    public async Task RetrievalByMorphology_FindsSimilarPositions()
    {
        // Arrange - Query with open position voicing
        var queryDoc = CreateCMajorOpenVoicing();
        var queryEmb = await GenerateEmbeddingAsync(queryDoc);

        // Act
        var results = _index.SearchByPartition(queryEmb, OpticKPartitions.Morphology, 5);

        // Assert
        TestContext.WriteLine("Top 5 results by Morphology:");
        foreach (var result in results)
        {
            TestContext.WriteLine($"  {result.Document.ChordName} ({result.Document.Diagram}): {result.Similarity:F4}");
        }

        // Results should favor similar fret positions
        var topResult = results[0];
        Assert.That(topResult.Similarity, Is.GreaterThan(0.5),
            "Should find morphologically similar voicings");
    }

    [Test]
    public async Task PartitionRetrievalReport_ShowsBreakdown()
    {
        // Arrange
        var queryDoc = CreateCMajorOpenVoicing();
        var queryEmb = await GenerateEmbeddingAsync(queryDoc);
        _index.Add(queryDoc, queryEmb);

        // Act
        var report = _index.AnalyzePartitionRetrieval(queryEmb, queryDoc.Id);

        // Assert
        TestContext.WriteLine(report.ToReport());
        Assert.That(report.IsTopWithCombined, Is.True, "Query doc should be top result for combined search");
    }

    [Test]
    public async Task DifferentChordTypes_HaveLowStructureSimilarity()
    {
        // Arrange - Major vs Diminished
        var cMajor = CreateCMajorOpenVoicing();
        var bDim = CreateBDiminishedVoicing();

        var embMajor = await GenerateEmbeddingAsync(cMajor);
        var embDim = await GenerateEmbeddingAsync(bDim);

        // Act
        var breakdown = _index.ComputeSimilarityBreakdown(embMajor, embDim);

        // Assert
        var structureSim = breakdown.PartitionScores["Structure"];
        TestContext.WriteLine($"Structure similarity (Major vs Diminished): {structureSim:F4}");
        Assert.That(structureSim, Is.LessThan(0.8),
            "Different chord types should have lower structure similarity");
    }

    [Test]
    public void AllPartitions_HaveNonZeroDimensions()
    {
        // Verify schema consistency
        foreach (var partition in OpticKPartitions.All)
        {
            TestContext.WriteLine(
                $"{partition.Name}: Offset={partition.Offset}, Dim={partition.Dimension}, End={partition.End}, Weight={partition.Weight}");
            Assert.That(partition.Dimension, Is.GreaterThan(0), $"{partition.Name} should have positive dimension");
            Assert.That(partition.End, Is.LessThanOrEqualTo(EmbeddingSchema.TotalDimension),
                $"{partition.Name} should not exceed total dimension");
        }
    }

    [Test]
    public async Task GeneratedEmbedding_HasCorrectDimension()
    {
        var doc = CreateCMajorOpenVoicing();
        var embedding = await GenerateEmbeddingAsync(doc);

        Assert.That(embedding.Length, Is.EqualTo(EmbeddingSchema.TotalDimension));
        TestContext.WriteLine($"Embedding dimension: {embedding.Length}");
    }

    [Test]
    public async Task WeightedSearch_BalancesPartitions()
    {
        // Arrange
        var queryDoc = CreateCMajorOpenVoicing();
        var queryEmb = await GenerateEmbeddingAsync(queryDoc);

        // Act
        var results = _index.Search(queryEmb, 5);

        // Assert
        TestContext.WriteLine("Top 5 results (weighted search):");
        foreach (var result in results)
        {
            TestContext.WriteLine($"  {result.Document.ChordName}: Overall={result.Similarity:F4}");
            foreach (var (part, sim) in result.PartitionSimilarities)
            {
                TestContext.WriteLine($"    {part}: {sim:F4}");
            }
        }

        Assert.That(results.Count, Is.GreaterThan(0), "Should return results");
    }

    private async Task SeedTestVoicingsAsync()
    {
        var voicings = new[]
        {
            CreateCMajorOpenVoicing(),
            CreateCMajorBarreVoicing(),
            CreateGMajorOpenVoicing(),
            CreateAMinorOpenVoicing(),
            CreateEMinorOpenVoicing(),
            CreateDMajorOpenVoicing(),
            CreateFMajorBarreVoicing(),
            CreateBDiminishedVoicing(),
            CreateDominantSeventhVoicing()
        };

        foreach (var voicing in voicings)
        {
            var embedding = await GenerateEmbeddingAsync(voicing);
            _index.Add(voicing, embedding);
        }
    }

    private static ChordVoicingRagDocument CreateCMajorOpenVoicing() => new()
    {
        Id = "c-major-open",
        ChordName = "C Major",
        Diagram = "x-3-2-0-1-0",
        MidiNotes = [48, 52, 55, 60, 64],
        PitchClasses = [0, 4, 7],
        PitchClassSet = "{0, 4, 7}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "C Major open position",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["C Major"], SemanticTags = ["Major", "Triad", "Open"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateCMajorBarreVoicing() => new()
    {
        Id = "c-major-barre",
        ChordName = "C Major",
        Diagram = "x-3-5-5-5-3",
        MidiNotes = [48, 55, 60, 64, 67],
        PitchClasses = [0, 4, 7],
        PitchClassSet = "{0, 4, 7}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "C Major barre chord",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["C Major"], SemanticTags = ["Major", "Triad", "Barre"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateGMajorOpenVoicing() => new()
    {
        Id = "g-major-open",
        ChordName = "G Major",
        Diagram = "3-2-0-0-0-3",
        MidiNotes = [43, 47, 50, 55, 59, 67],
        PitchClasses = [7, 11, 2],
        PitchClassSet = "{2, 7, 11}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "G Major open position",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["G Major"], SemanticTags = ["Major", "Triad", "Open"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateAMinorOpenVoicing() => new()
    {
        Id = "a-minor-open",
        ChordName = "A Minor",
        Diagram = "x-0-2-2-1-0",
        MidiNotes = [45, 52, 57, 60, 64],
        PitchClasses = [9, 0, 4],
        PitchClassSet = "{0, 4, 9}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "A Minor open position",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["A Minor"], SemanticTags = ["Minor", "Triad", "Open"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateEMinorOpenVoicing() => new()
    {
        Id = "e-minor-open",
        ChordName = "E Minor",
        Diagram = "0-2-2-0-0-0",
        MidiNotes = [40, 47, 52, 55, 59, 64],
        PitchClasses = [4, 7, 11],
        PitchClassSet = "{4, 7, 11}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "E Minor open position",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["E Minor"], SemanticTags = ["Minor", "Triad", "Open"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateDMajorOpenVoicing() => new()
    {
        Id = "d-major-open",
        ChordName = "D Major",
        Diagram = "x-x-0-2-3-2",
        MidiNotes = [50, 57, 62, 66],
        PitchClasses = [2, 6, 9],
        PitchClassSet = "{2, 6, 9}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "D Major open position",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["D Major"], SemanticTags = ["Major", "Triad", "Open"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateFMajorBarreVoicing() => new()
    {
        Id = "f-major-barre",
        ChordName = "F Major",
        Diagram = "1-3-3-2-1-1",
        MidiNotes = [41, 48, 53, 57, 60, 65],
        PitchClasses = [5, 9, 0],
        PitchClassSet = "{0, 5, 9}",
        IntervalClassVector = "001110",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "F Major barre chord",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
        PossibleKeys = ["F Major"], SemanticTags = ["Major", "Triad", "Barre"], StackingType = "Tertian",
        Embedding = null
    };

    private static ChordVoicingRagDocument CreateBDiminishedVoicing() => new()
    {
        Id = "b-diminished",
        ChordName = "B Diminished",
        Diagram = "x-2-3-4-3-x",
        MidiNotes = [47, 53, 59, 62],
        PitchClasses = [11, 2, 5],
        PitchClassSet = "{2, 5, 11}",
        IntervalClassVector = "002001",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "B Diminished chord",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "3-10", YamlAnalysis = "",
        PossibleKeys = ["C Major"], SemanticTags = ["Diminished", "Triad"], StackingType = "Tertian", Embedding = null
    };

    private static ChordVoicingRagDocument CreateDominantSeventhVoicing() => new()
    {
        Id = "g7-dominant",
        ChordName = "G Dominant 7",
        Diagram = "3-2-0-0-0-1",
        MidiNotes = [43, 47, 50, 55, 59, 65],
        PitchClasses = [7, 11, 2, 5],
        PitchClassSet = "{2, 5, 7, 11}",
        IntervalClassVector = "012111",
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "G Dominant 7th chord",
        Jobs = [], TuningId = "Standard", PitchClassSetId = "4-27", YamlAnalysis = "",
        PossibleKeys = ["C Major", "G Major"], SemanticTags = ["Dominant", "Seventh"], StackingType = "Tertian",
        Embedding = null
    };
}
