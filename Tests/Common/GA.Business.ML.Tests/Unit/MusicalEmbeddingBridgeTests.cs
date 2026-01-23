namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Embeddings;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for the <see cref="MusicalEmbeddingBridge"/> MEAI adapter.
/// </summary>
/// <remarks>
/// These tests verify that the bridge correctly adapts OPTIC-K embeddings
/// to the Microsoft Extensions for AI (MEAI) IEmbeddingGenerator interface.
/// </remarks>
[TestFixture]
public class MusicalEmbeddingBridgeTests
{
    private MusicalEmbeddingGenerator _generator = null!;
    private MusicalEmbeddingBridge _bridge = null!;

    [SetUp]
    public void Setup()
    {
        // Create real generator using TestServices factory (provides all required services)
        _generator = TestInfrastructure.TestServices.CreateGenerator();
        _bridge = new MusicalEmbeddingBridge(_generator);
    }

    [TearDown]
    public void TearDown()
    {
        _bridge?.Dispose();
    }

    [Test]
    public void Metadata_ReturnsCorrectProviderInfo()
    {
        // Act
        var metadata = _bridge.Metadata;

        // Assert
        Assert.That(metadata.ProviderName, Is.EqualTo("GuitarAlchemist"));
        Assert.That(metadata.DefaultModelId, Does.StartWith("OPTIC-K-v"));
    }

    [Test]
    public void Dimension_MatchesSchemaDefinition()
    {
        // Act
        var dimension = _bridge.Dimension;

        // Assert
        Assert.That(dimension, Is.EqualTo(EmbeddingSchema.TotalDimension));
    }

    [Test]
    public async Task GenerateAsync_SingleDocument_ReturnsEmbedding()
    {
        // Arrange - create a minimal VoicingDocument
        var doc = CreateMinimalVoicingDocument();

        // Act
        var result = await _bridge.GenerateAsync([doc]);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Vector.Length, Is.EqualTo(EmbeddingSchema.TotalDimension));
    }

    [Test]
    public async Task GenerateAsync_MultipleDocuments_ReturnsAllEmbeddings()
    {
        // Arrange
        var docs = new[]
        {
            CreateMinimalVoicingDocument("doc1"),
            CreateMinimalVoicingDocument("doc2"),
            CreateMinimalVoicingDocument("doc3")
        };

        // Act
        var result = await _bridge.GenerateAsync(docs);

        // Assert
        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result.All(e => e.Vector.Length == EmbeddingSchema.TotalDimension), Is.True);
    }

    [Test]
    public async Task GenerateAsync_ReturnsUsageDetails()
    {
        // Arrange
        var docs = new[] { CreateMinimalVoicingDocument(), CreateMinimalVoicingDocument() };

        // Act
        var result = await _bridge.GenerateAsync(docs);

        // Assert
        Assert.That(result.Usage, Is.Not.Null);
        Assert.That(result.Usage!.InputTokenCount, Is.EqualTo(2));
        Assert.That(result.Usage.TotalTokenCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GenerateSingleAsync_ReturnsEmbedding()
    {
        // Arrange
        var doc = CreateMinimalVoicingDocument();

        // Act
        var result = await _bridge.GenerateSingleAsync(doc);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Vector.Length, Is.EqualTo(EmbeddingSchema.TotalDimension));
    }

    [Test]
    public void GetService_ReturnsGenerator()
    {
        // Act
        var generator = _bridge.GetService(typeof(MusicalEmbeddingGenerator));

        // Assert
        Assert.That(generator, Is.SameAs(_generator));
    }

    [Test]
    public void GetService_ReturnsBridge()
    {
        // Act
        var bridge = _bridge.GetService(typeof(MusicalEmbeddingBridge));

        // Assert
        Assert.That(bridge, Is.SameAs(_bridge));
    }

    [Test]
    public void GetService_UnknownType_ReturnsNull()
    {
        // Act
        var result = _bridge.GetService(typeof(string));

        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Creates a minimal VoicingDocument for testing purposes.
    /// </summary>
    private static VoicingDocument CreateMinimalVoicingDocument(string id = "test-1")
    {
        return new VoicingDocument
        {
            Id = id,
            SearchableText = "C Major chord open position",
            ChordName = "C Major",
            Diagram = "x-3-2-0-1-0",
            MidiNotes = [48, 52, 55, 60, 64], // C3, E3, G3, C4, E4
            PitchClasses = [0, 4, 7],
            PitchClassSet = "{0, 4, 7}",
            IntervalClassVector = "001110",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "3-11",
            YamlAnalysis = "{}",
            PossibleKeys = ["C Major", "G Major"],
            SemanticTags = ["Major", "Triad", "Open Position"],
            StackingType = "Tertian",
            Embedding = null // Will be generated
        };
    }
}
