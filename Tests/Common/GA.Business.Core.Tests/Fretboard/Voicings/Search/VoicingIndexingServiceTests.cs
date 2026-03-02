namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Services.Fretboard.Voicings.Generation;
using GA.Business.ML.Search;

[TestFixture]
public class VoicingIndexingServiceTests
{
    [SetUp]
    public void Setup() => _indexingService = new();

    private VoicingIndexingService? _indexingService;

    [Test]
    public async Task IndexVoicingsAsync_WithSmallSet_PopulatesDocuments()
    {
        // Arrange
        var fretboard = Fretboard.Default;
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, 3, 3)
            .Take(100)
            .ToList();
        var vectorCollection = new RelativeFretVectorCollection(6);
        // Act
        var result = await _indexingService!.IndexVoicingsAsync(voicings, vectorCollection);
        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(_indexingService.DocumentCount, Is.GreaterThan(0));
        var firstDoc = _indexingService.Documents[0];
        Assert.That(firstDoc.SemanticTags, Is.Not.Null);
        Assert.That(firstDoc.SearchableText, Is.Not.Empty);
    }

    [Test]
    public async Task Indexing_ProducesValuableSemanticTags()
    {
        // Arrange
        var fretboard = Fretboard.Default;
        // Generate a specific set that we know should have certain tags
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, 4, 3);
        var openC = voicings.FirstOrDefault(v =>
        {
            var diagram = VoicingExtensions.GetPositionDiagram(v.Positions);
            return diagram.Contains("3") && diagram.Contains("1") && diagram.Contains("0");
        });
        if (openC == null)
        {
            Assert.Inconclusive("Target voicing not found in generated set");
        }

        var vectorCollection = new RelativeFretVectorCollection(6);
        // Act
        await _indexingService!.IndexVoicingsAsync(new[] { openC! }, vectorCollection);
        var doc = _indexingService.Documents[0];
        // Assert
        Assert.That(doc.SemanticTags, Is.Not.Null);
        Assert.That(doc.SemanticTags.Length, Is.GreaterThan(0));
        TestContext.WriteLine($"Generated tags: {string.Join(", ", doc.SemanticTags)}");
    }
}
