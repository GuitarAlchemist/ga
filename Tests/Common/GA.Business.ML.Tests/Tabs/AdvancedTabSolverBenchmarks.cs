namespace GA.Business.ML.Tests.Tabs;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using GA.Business.ML.Tabs;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.Core.Player;
using GA.Business.Core.Fretboard;
using GA.Business.Core.AI;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Abstractions;

[TestFixture]
[Category("Benchmark")]
public class AdvancedTabSolverBenchmarks
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

        _solver = new AdvancedTabSolver(
            _mapper,
            _costService,
            _mockStyleService.Object,
            _mockGenerator.Object,
            new PlayerProfile()
        );
    }

    [Test]
    public async Task Benchmark_SolveAsync_8Bars_StandardJazz()
    {
        // Arrange
        // 8 chords: Dm7 - G7 - Cmaj7 - A7 - Dm7 - G7 - Cmaj7 - C6
        var chords = new List<List<Pitch>>
        {
            // Dm7 (Root-5-7-3): D3 A3 C4 F4 (x-5-7-5-6-x)
            new() { Pitch.Sharp.Parse("D3"), Pitch.Sharp.Parse("A3"), Pitch.Sharp.Parse("C4"), Pitch.Sharp.Parse("F4") },
            // G13/7 (Root-7-3-13): G2 F3 B3 E4 (3-x-3-4-5-x)
            new() { Pitch.Sharp.Parse("G2"), Pitch.Sharp.Parse("F3"), Pitch.Sharp.Parse("B3"), Pitch.Sharp.Parse("E4") },
            // Cmaj7 (Root-5-7-3): C3 G3 B3 E4 (x-3-5-4-5-x)
            new() { Pitch.Sharp.Parse("C3"), Pitch.Sharp.Parse("G3"), Pitch.Sharp.Parse("B3"), Pitch.Sharp.Parse("E4") },
            // A7 (Root-5-7-3): A2 E3 G3 C#4 (5-7-5-6-x-x) -> A2 is E5. E3 is A7. G3 is D5. C#4 is G6.
            new() { Pitch.Sharp.Parse("A2"), Pitch.Sharp.Parse("E3"), Pitch.Sharp.Parse("G3"), Pitch.Sharp.Parse("C#4") },
            // Repeat
            new() { Pitch.Sharp.Parse("D3"), Pitch.Sharp.Parse("A3"), Pitch.Sharp.Parse("C4"), Pitch.Sharp.Parse("F4") },
            new() { Pitch.Sharp.Parse("G2"), Pitch.Sharp.Parse("F3"), Pitch.Sharp.Parse("B3"), Pitch.Sharp.Parse("E4") },
            new() { Pitch.Sharp.Parse("C3"), Pitch.Sharp.Parse("G3"), Pitch.Sharp.Parse("B3"), Pitch.Sharp.Parse("E4") },
            // C6: C3 G3 A3 E4 (x-3-5-2-5-x) -> A3 is G2. C3 is A3. G3 is D5. E4 is B5.
            // A3 on G string is Fret 2.
            // C3(A3), G3(D5), A3(G2), E4(B5).
            // Frets: 3, 5, 2, 5. Stretch 2-5 = 3. Playable.
            new() { Pitch.Sharp.Parse("C3"), Pitch.Sharp.Parse("G3"), Pitch.Sharp.Parse("A3"), Pitch.Sharp.Parse("E4") }
        };

        // Act
        var sw = Stopwatch.StartNew();
        var allPaths = await _solver.SolveAsync(chords);
        sw.Stop();

        var result = allPaths[0];

        // Print
        Console.WriteLine($"Solved 8 chords in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Steps: {result.Count}");

        // Assert
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500), "Viterbi pathfinding exceeded 500ms budget");
        Assert.That(result, Has.Count.EqualTo(8));
    }
}
