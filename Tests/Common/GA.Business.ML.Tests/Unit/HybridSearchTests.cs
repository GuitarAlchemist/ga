namespace GA.Business.ML.Tests.Unit;

using Embeddings;
using Rag.Models;
using Moq;

[TestFixture]
public class HybridSearchTests
{
    [SetUp]
    public void Setup()
    {
        _mockIndex = new();
        _retrievalService = new(_mockIndex.Object);
    }

    private Mock<IVectorIndex> _mockIndex;
    private SpectralRetrievalService _retrievalService;

    [Test]
    public void Search_WithQualityFilter_ReturnsOnlyMatchingDocuments()
    {
        // Arrange: Create documents with proper embeddings
        var docs = new List<ChordVoicingRagDocument>
        {
            CreateDoc("1", "C Major", "x-x-x", "Major"),
            CreateDoc("2", "C Minor", "x-x-x", "Minor"),
            CreateDoc("3", "A Major 7", "x-x-x", "Major")
        };

        // Mock the Documents property - SpectralRetrievalService filters in-memory
        _mockIndex.Setup(i => i.Documents).Returns(docs);

        var queryVector = new float[EmbeddingSchema.TotalDimension];

        // Act: Filter by "Minor" quality tag
        var results = _retrievalService.Search(queryVector, quality: "Minor").ToList();

        // Assert: Only C Minor should match
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results.First().Doc.ChordName, Is.EqualTo("C Minor"));
    }

    [Test]
    public void Search_WithMultipleFilters_FiltersCorrectly()
    {
        // Arrange
        var docs = new List<ChordVoicingRagDocument>
        {
            CreateDoc("1", "C Major 7 Drop 2", "x-x-x", "Major", "7", "Drop 2"),
            CreateDoc("2", "C Major 7 Tertian", "x-x-x", "Major", "7"),
            CreateDoc("3", "C Minor 6", "x-x-x", "Minor", "6", "Drop 2")
        };

        _mockIndex.Setup(i => i.Documents).Returns(docs);

        var queryVector = new float[EmbeddingSchema.TotalDimension];

        // Act: Filter with multiple constraints
        var results = _retrievalService.Search(
            queryVector,
            quality: "Major",
            extension: "7",
            stackingType: "Drop 2").ToList();

        // Assert: Only the C Major 7 Drop 2 should match all filters
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results.First().Doc.ChordName, Is.EqualTo("C Major 7 Drop 2"));
    }

    [Test]
    public void Search_WithNoFilters_ReturnsAllDocuments()
    {
        // Arrange
        var docs = new List<ChordVoicingRagDocument>
        {
            CreateDoc("1", "C", "x", "Major"),
            CreateDoc("2", "D", "x", "Minor"),
            CreateDoc("3", "E", "x", "Diminished")
        };

        _mockIndex.Setup(i => i.Documents).Returns(docs);

        var queryVector = new float[EmbeddingSchema.TotalDimension];

        // Act: No filters - should return all documents
        var results = _retrievalService.Search(queryVector).ToList();

        // Assert: All 3 documents returned
        Assert.That(results.Count, Is.EqualTo(3));
    }

    [Test]
    public void Search_WithNoteCountFilter_FiltersCorrectly()
    {
        // Arrange - use CreateDocWithNotes to set MidiNotes in initializer
        var doc3Notes = CreateDoc("1", "Triad", "x-x-x", "Major", midiNotes: [60, 64, 67]);
        var doc4Notes = CreateDoc("2", "Seventh", "x-x-x-x", "Major", midiNotes: [60, 64, 67, 71]);

        var docs = new List<ChordVoicingRagDocument> { doc3Notes, doc4Notes };
        _mockIndex.Setup(i => i.Documents).Returns(docs);

        var queryVector = new float[EmbeddingSchema.TotalDimension];

        // Act: Filter to 4-note voicings only
        var results = _retrievalService.Search(queryVector, noteCount: 4).ToList();

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results.First().Doc.ChordName, Is.EqualTo("Seventh"));
    }

    private ChordVoicingRagDocument CreateDoc(
        string id,
        string name,
        string diagram,
        string tag,
        string? extension = null,
        string stackingType = "Tertian",
        int[]? midiNotes = null)
    {
        var tags = new List<string> { tag };
        if (extension != null)
        {
            tags.Add(extension);
        }

        return new()
        {
            Id = id,
            ChordName = name,
            SearchableText = name + " " + tag,
            SemanticTags = [.. tags],
            Diagram = diagram,
            StackingType = stackingType,

            // Required dummies
            MidiNotes = midiNotes ?? [],
            PitchClasses = [],
            PitchClassSet = "",
            IntervalClassVector = "",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            YamlAnalysis = "{}",
            PossibleKeys = [],
            Embedding = new float[EmbeddingSchema.TotalDimension]
        };
    }
}
