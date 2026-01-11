namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using NUnit.Framework;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Voicings.Generation;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[TestFixture]
public class VoicingIndexingServiceTests
{
    private VoicingIndexingService? _indexingService;

    [SetUp]
    public void Setup()
    {
        _indexingService = new VoicingIndexingService();
    }

    [Test]
    public async Task IndexVoicingsAsync_WithSmallSet_PopulatesDocuments()
    {
        // Arrange
        var fretboard = Fretboard.Default;
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, windowSize: 3, minPlayedNotes: 3)
            .Take(100)
            .ToList();
        
        var vectorCollection = new RelativeFretVectorCollection(6, 5);

        // Act
        var result = await _indexingService!.IndexVoicingsAsync(voicings, vectorCollection);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(_indexingService.DocumentCount, Is.GreaterThan(0));
        
        var firstDoc = _indexingService.Documents.First();
        Assert.That(firstDoc.SemanticTags, Is.Not.Null);
        Assert.That(firstDoc.SearchableText, Is.Not.Empty);
    }

    [Test]
    public async Task Indexing_ProducesValuableSemanticTags()
    {
        // Arrange
        var fretboard = Fretboard.Default;
        // Generate a specific set that we know should have certain tags
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, windowSize: 4, minPlayedNotes: 3);
        var openC = voicings.FirstOrDefault(v => v.ToString().Contains("3") && v.ToString().Contains("1") && v.ToString().Contains("0"));

        if (openC == null) Assert.Inconclusive("Target voicing not found in generated set");

        var vectorCollection = new RelativeFretVectorCollection(6, 5);

        // Act
        await _indexingService!.IndexVoicingsAsync(new[] { openC! }, vectorCollection);
        var doc = _indexingService.Documents.First();

        // Assert
        Assert.That(doc.SemanticTags, Is.Not.Null);
        Assert.That(doc.SemanticTags.Length, Is.GreaterThan(0));
        
        TestContext.WriteLine($"Generated tags: {string.Join(", ", doc.SemanticTags)}");
    }
}
