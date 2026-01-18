namespace GA.Business.ML.Tests.Tabs;

using System.Threading.Tasks;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Tabs;
using NUnit.Framework;

[TestFixture]
public class TabAnalysisServiceTests
{
    private TabTokenizer _tokenizer = null!;
    private TabToPitchConverter _converter = null!;
    private MusicalEmbeddingGenerator _generator = null!;
    private TabAnalysisService _service = null!;

    [SetUp]
    public void Setup()
    {
        _tokenizer = new TabTokenizer();
        _converter = new TabToPitchConverter();
        _generator = TestInfrastructure.TestServices.CreateGenerator();
        _service = new TabAnalysisService(_tokenizer, _converter, _generator);
    }

    [Test]
    public void TestAnalyzeSmokeOnTheWater()
    {
        // Arrange
        // Simple 0-3-5 riff on Low E (Standard Tuning)
        // E string: 0 (E2), 3 (G2), 5 (A2)
        var tab = @"
e|-----------------|
B|-----------------|
G|-----------------|
D|-----------------|
A|-----------------|
E|--0--3--5--------|
";
        // Act
        var result = _service.AnalyzeAsync(tab).GetAwaiter().GetResult();

        // Assert
        TestContext.WriteLine($"Analyzed tab: {tab}");
        TestContext.WriteLine($"Event count: Expected=3, Actual={result.Events.Count} (Tab contains 3 distinct note/chord events)");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null, "Analysis result should not be null.");
            Assert.That(result.Events.Count, Is.EqualTo(3), "Should have exactly 3 events for the 0-3-5 riff.");

            var ev0 = result.Events[0];
            TestContext.WriteLine($"Event 0 MIDI: Expected=contains 40 (E2), Actual={string.Join(", ", ev0.Document.MidiNotes)}");
            Assert.That(ev0.Document.MidiNotes, Contains.Item(40), "Event 0 should contain MIDI 40 (E2).");
            Assert.That(ev0.Embedding, Is.Not.Null, "Event 0 embedding should not be null.");
            Assert.That(ev0.Embedding.Length, Is.EqualTo(EmbeddingSchema.TotalDimension), "Embedding dimension should match schema.");

            var ev1 = result.Events[1];
            TestContext.WriteLine($"Event 1 MIDI: Expected=contains 43 (G2), Actual={string.Join(", ", ev1.Document.MidiNotes)}");
            Assert.That(ev1.Document.MidiNotes, Contains.Item(43), "Event 1 should contain MIDI 43 (G2).");

            var ev2 = result.Events[2];
            TestContext.WriteLine($"Event 2 MIDI: Expected=contains 45 (A2), Actual={string.Join(", ", ev2.Document.MidiNotes)}");
            Assert.That(ev2.Document.MidiNotes, Contains.Item(45), "Event 2 should contain MIDI 45 (A2).");
        });
    }
}
