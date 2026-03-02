namespace GA.Business.ML.Tests.Tabs;

using Abstractions;
using Domain.Core.Instruments;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Analysis;
using Embeddings;
using Rag.Models;
using ML.Tabs;
using Moq;
using Retrieval;

[TestFixture]
public class AlternativeFingeringServiceTests
{
    [SetUp]
    public void Setup()
    {
        var tuning = Tuning.Default;
        var mapper = new FretboardPositionMapper(tuning);
        var cost = new PhysicalCostService(new());

        // Mock dependencies of AdvancedTabSolver
        var mockStyle = new Mock<StyleProfileService>(Mock.Of<IVectorIndex>());
        var mockGen = new Mock<IEmbeddingGenerator>();
        var mockRanker = new Mock<IMlNaturalnessRanker>();

        _mockSolver = new(
            mapper,
            cost,
            mockStyle.Object,
            mockGen.Object,
            mockRanker.Object
        );

        _service = new(_mockSolver.Object);
    }

    private Mock<AdvancedTabSolver> _mockSolver;
    private AlternativeFingeringService _service;

    [Test]
    public async Task GetAlternativesAsync_CategorizesPathsCorrectly()
    {
        // Arrange
        var openChord = CreateChord([3, 2, 0, 1, 0]);
        var jazzChord = CreateChord([3, 5, 4, 5]);
        var highChord = CreateChord([10, 9, 8]);

        var pathOpen = new List<List<FretboardPosition>> { openChord };
        var pathJazz = new List<List<FretboardPosition>> { jazzChord };
        var pathHigh = new List<List<FretboardPosition>> { highChord };

        var mockPaths = new List<List<List<FretboardPosition>>> { pathOpen, pathJazz, pathHigh };

        _mockSolver.Setup(s => s.SolveAsync(
                It.IsAny<IEnumerable<IEnumerable<Pitch>>>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync(mockPaths);

        var input = new List<ChordVoicingRagDocument> { CreateDummyDoc(60) };

        // Act
        var result = await _service.GetAlternativesAsync(input);

        // Assert
        TestContext.WriteLine($"Alternative Fingering Options Found: {result.Count}");
        foreach (var opt in result)
        {
            TestContext.WriteLine($"  Label: {opt.Label}, Difficulty: {opt.DifficultyScore:F2}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));

            var campfire = result.FirstOrDefault(x => x.Label.Contains("Campfire"));
            Assert.That(campfire, Is.Not.Null, "Should identify Campfire/Open option");

            var jazz = result.FirstOrDefault(x => x.Label.Contains("Jazz"));
            Assert.That(jazz, Is.Not.Null, "Should identify Jazz option");

            var high = result.FirstOrDefault(x => x.Label.Contains("High"));
            Assert.That(high, Is.Not.Null, "Should identify High option");
        });
    }

    private List<FretboardPosition> CreateChord(int[] frets)
    {
        var list = new List<FretboardPosition>();
        for (var i = 0; i < frets.Length; i++)
        {
            list.Add(new(Str.FromValue(6 - i), frets[i], Pitch.FromMidiNote(60)));
        }

        return list;
    }

    private ChordVoicingRagDocument CreateDummyDoc(int midiNote) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            ChordName = "C",
            MidiNotes = [midiNote],
            PitchClasses = [midiNote % 12],
            PitchClassSet = (midiNote % 12).ToString(),
            SemanticTags = [],
            SearchableText = "C",
            PossibleKeys = [],
            YamlAnalysis = "{}",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            Diagram = "",
            IntervalClassVector = "000000",
            RootPitchClass = midiNote % 12,
            MinFret = 0,
            MaxFret = 0,
            HandStretch = 0,
            BarreRequired = false,
            Consonance = 1.0,
            HarmonicFunction = "Tonic",
            IsNaturallyOccurring = true
        };
}
