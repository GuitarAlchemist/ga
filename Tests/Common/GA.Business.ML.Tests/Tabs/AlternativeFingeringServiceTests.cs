namespace GA.Business.ML.Tests.Tabs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes;
using GA.Business.Core.Player;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Fretboard;
using GA.Business.Core.AI;
using GA.Business.Core.Abstractions;
using Moq;
using NUnit.Framework;

[TestFixture]
public class AlternativeFingeringServiceTests
{
    private Mock<AdvancedTabSolver> _mockSolver;
    private AlternativeFingeringService _service;

    [SetUp]
    public void Setup()
    {
        var tuning = Tuning.Default;
        var mapper = new FretboardPositionMapper(tuning);
        var cost = new PhysicalCostService(new PlayerProfile(), null);

        // Mock dependencies of AdvancedTabSolver
        var mockStyle = new Mock<StyleProfileService>(Mock.Of<IVectorIndex>());
        var mockGen = new Mock<IEmbeddingGenerator>();

        _mockSolver = new Mock<AdvancedTabSolver>(
            mapper,
            cost,
            mockStyle.Object,
            mockGen.Object,
            null // PlayerProfile
        );

        _service = new AlternativeFingeringService(_mockSolver.Object);
    }

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

        var input = new List<VoicingDocument> { CreateDummyDoc(60) };

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
        for (int i = 0; i < frets.Length; i++)
        {
            list.Add(new FretboardPosition(Str.FromValue(6 - i), frets[i], Pitch.FromMidiNote(60)));
        }
        return list;
    }

    private VoicingDocument CreateDummyDoc(int midiNote)
    {
        return new VoicingDocument
        {
            Id = Guid.NewGuid().ToString(),
            ChordName = "C",
            MidiNotes = new[] { midiNote },
            PitchClasses = new[] { midiNote % 12 },
            PitchClassSet = (midiNote % 12).ToString(),
            SemanticTags = Array.Empty<string>(),
            SearchableText = "C",
            PossibleKeys = Array.Empty<string>(),
            YamlAnalysis = "{}",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = Array.Empty<string>(),
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
}
