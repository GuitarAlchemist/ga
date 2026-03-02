namespace GA.Business.ML.Tests.Tabs;

using Embeddings;
using ML.Tabs;
using TestInfrastructure;

[TestFixture]
public class TabAnalysisServiceTests
{
    [SetUp]
    public void Setup()
    {
        _tokenizer = new();
        _converter = new();
        _generator = TestServices.CreateGenerator();
        _service = new(_tokenizer, _converter, _generator, new());
    }

    private TabTokenizer _tokenizer = null!;
    private TabToPitchConverter _converter = null!;
    private MusicalEmbeddingGenerator _generator = null!;
    private TabAnalysisService _service = null!;

    [Test]
    public void TestAnalyzeSmokeOnTheWater()
    {
        // Arrange
        // Simple 0-3-5 riff on Low E (Standard Tuning)
        // E string: 0 (E2), 3 (G2), 5 (A2)
        var tab = """

                  e|-----------------|
                  B|-----------------|
                  G|-----------------|
                  D|-----------------|
                  A|-----------------|
                  E|--0--3--5--------|

                  """;
        // Act
        var result = _service.AnalyzeAsync(tab).GetAwaiter().GetResult();

        // Assert
        TestContext.WriteLine($"Analyzed tab: {tab}");
        TestContext.WriteLine(
            $"Event count: Expected=3, Actual={result.Events.Count} (Tab contains 3 distinct note/chord events)");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null, "Analysis result should not be null.");
            Assert.That(result.Events.Count, Is.EqualTo(3), "Should have exactly 3 events for the 0-3-5 riff.");

            var ev0 = result.Events[0];
            TestContext.WriteLine(
                $"Event 0 MIDI: Expected=contains 40 (E2), Actual={string.Join(", ", ev0.Document.MidiNotes)}");
            Assert.That(ev0.Document.MidiNotes, Contains.Item(40), "Event 0 should contain MIDI 40 (E2).");
            Assert.That(ev0.Embedding, Is.Not.Null, "Event 0 embedding should not be null.");
            Assert.That(ev0.Embedding.Length, Is.EqualTo(EmbeddingSchema.TotalDimension),
                "Embedding dimension should match schema.");

            var ev1 = result.Events[1];
            TestContext.WriteLine(
                $"Event 1 MIDI: Expected=contains 43 (G2), Actual={string.Join(", ", ev1.Document.MidiNotes)}");
            Assert.That(ev1.Document.MidiNotes, Contains.Item(43), "Event 1 should contain MIDI 43 (G2).");

            var ev2 = result.Events[2];
            TestContext.WriteLine(
                $"Event 2 MIDI: Expected=contains 45 (A2), Actual={string.Join(", ", ev2.Document.MidiNotes)}");
            Assert.That(ev2.Document.MidiNotes, Contains.Item(45), "Event 2 should contain MIDI 45 (A2).");
        });
    }

    [Test]
    public void TestAnalyzeSmokeOnTheWaterPowerChords()
    {
        // Arrange
        // Smoke on the water with power chords (E5, G5, A5)
        var tab = """

                  e|-----------------|
                  B|-----------------|
                  G|-----------------|
                  D|--2--5--7--------|
                  A|--2--5--7--------|
                  E|--0--3--5--------|

                  """;
        // Act
        var result = _service.AnalyzeAsync(tab).GetAwaiter().GetResult();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Events.Count, Is.EqualTo(3));

            foreach(var e in result.Events) {
                TestContext.WriteLine($"Power Chord: {e.Document.ChordName}");
            }
        });
    }

    [Test]
    public void TestVerifyKeyDetectionForSimpleProgression()
    {
        // Arrange
        // Simple I-V-vi-IV in G Major (G - D - Em - C)
        var tab = """

            e|--3--2--0--0--|
            B|--3--3--0--1--|
            G|--0--2--0--0--|
            D|--0--0--2--2--|
            A|--2-----2--3--|
            E|--3-----0-----|

            """;
        // Act
        var result = _service.AnalyzeAsync(tab).GetAwaiter().GetResult();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Events.Count, Is.EqualTo(4));
            
            foreach(var e in result.Events) {
                TestContext.WriteLine($"Chord from progression: {e.Document.ChordName} - PossibleKeys: {(string.Join(", ", e.Document.PossibleKeys))}");
            }
            
            var ev0 = result.Events[0];
            Assert.That(ev0.Document.PossibleKeys, Contains.Item("Key of G"));
        });
    }
}
