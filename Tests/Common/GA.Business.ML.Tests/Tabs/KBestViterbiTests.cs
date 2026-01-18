namespace GA.Business.ML.Tests.Tabs;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.AI;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Notes;
using GA.Business.Core.Player;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;
using Moq;
using NUnit.Framework;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Abstractions;

[TestFixture]
public class KBestViterbiTests
{
    private AdvancedTabSolver _solver;
    private FretboardPositionMapper _mapper;
    private PhysicalCostService _costService;
    private Mock<IEmbeddingGenerator> _mockGenerator;
    private Mock<StyleProfileService> _mockStyleService;

    [SetUp]
    public void Setup()
    {
        var tuning = Tuning.Default;
        _mapper = new FretboardPositionMapper(tuning);
        _costService = new PhysicalCostService(new PlayerProfile());

        _mockGenerator = new Mock<IEmbeddingGenerator>();
        _mockGenerator.Setup(g => g.GenerateEmbeddingAsync(It.IsAny<VoicingDocument>()))
                      .ReturnsAsync(new double[216]);

        _mockStyleService = new Mock<StyleProfileService>(Mock.Of<IVectorIndex>());
        _mockStyleService.Setup(s => s.GetStyleCentroid(It.IsAny<string>()))
                         .Returns((double[])null); // No style bias for basic test

        _solver = new AdvancedTabSolver(
            _mapper,
            _costService,
            _mockStyleService.Object,
            _mockGenerator.Object
        );
    }

    [Test]
    public async Task SolveAsync_ReturnsKUniquePaths()
    {
        // Arrange
        // Simple progression: C - G - C
        // These chords have many valid fingerings (open, barre, etc.)
        // C: C3 E3 G3 (0 1 0 on A D G) or (3 5 5 on E A D)
        var chords = new List<List<Pitch>>
        {
            new() { Pitch.Sharp.Parse("C3"), Pitch.Sharp.Parse("E3"), Pitch.Sharp.Parse("G3") }, // C
            new() { Pitch.Sharp.Parse("G2"), Pitch.Sharp.Parse("B2"), Pitch.Sharp.Parse("D3") }, // G
            new() { Pitch.Sharp.Parse("C3"), Pitch.Sharp.Parse("E3"), Pitch.Sharp.Parse("G3") }  // C
        };

        int k = 5;

        // Act
        var result = await _solver.SolveAsync(chords, k: k);

        // Assert
        TestContext.WriteLine($"Input Chords: {string.Join(" -> ", chords.Select(c => string.Join(" ", c)))}");
        TestContext.WriteLine($"Paths found: Expected <= {k}, Actual={result.Count} (Viterbi K-best path search)");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null, "Solver result should not be null.");
            Assert.That(result.Count, Is.LessThanOrEqualTo(k), $"Should return at most {k} paths.");
            Assert.That(result.Count, Is.GreaterThan(1), "Common chords like C and G should have multiple valid fingering paths.");

            // Check uniqueness
            var distinctPaths = result.Distinct().Count();
            Assert.That(distinctPaths, Is.EqualTo(result.Count), "All returned paths must be unique.");

            // Verify path lengths and print them
            for (int i = 0; i < result.Count; i++)
            {
                var path = result[i];
                var pathString = string.Join(" -> ", path.Select(chord =>
                    "[" + string.Join(", ", chord.Select(pos => $"{pos.StringIndex}:{pos.Fret}")) + "]"));
                TestContext.WriteLine($"Path {i}: {pathString}");
                Assert.That(path.Count, Is.EqualTo(chords.Count), $"Path {i} length should match input chord count.");
            }
        });
    }
}
