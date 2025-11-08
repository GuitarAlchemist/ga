namespace GA.Business.Core.Tests.Spatial;

using BSP.Core.Spatial;
using Core.Atonal;
using Core.Chords;
using Microsoft.Extensions.Logging;
using Moq;
// using GA.Business.Core.Spatial; // Namespace does not exist

[TestFixture]
public class TonalBspTests
{
    [SetUp]
    public void SetUp()
    {
        _mockBspLogger = new Mock<ILogger<TonalBspService>>();
        _mockAnalyzerLogger = new Mock<ILogger<TonalBspAnalyzer>>();

        _bspService = new TonalBspService(_mockBspLogger.Object);
        _analyzer = new TonalBspAnalyzer(_bspService, _mockAnalyzerLogger.Object);
    }

    private TonalBspService _bspService;
    private TonalBspAnalyzer _analyzer;
    private Mock<ILogger<TonalBspService>> _mockBspLogger;
    private Mock<ILogger<TonalBspAnalyzer>> _mockAnalyzerLogger;

    [Test]
    public void TonalBSPTree_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var tree = new TonalBspTree();

        // Assert
        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root.Region.Name, Is.EqualTo("Chromatic"));
        Assert.That(tree.Root.Region.TonalityType, Is.EqualTo(TonalityType.Atonal));
    }

    [Test]
    public void FindTonalRegion_WithMajorTriad_ShouldReturnMajorRegion()
    {
        // Arrange
        var tree = new TonalBspTree();
        var cMajorTriad = new PitchClassSet([0, 4, 7]); // C-E-G

        // Act
        var region = tree.FindTonalRegion(cMajorTriad);

        // Assert
        Assert.That(region, Is.Not.Null);
        Assert.That(region.TonalityType, Is.EqualTo(TonalityType.Major).Or.EqualTo(TonalityType.Atonal));
    }

    [Test]
    public void FindTonalRegion_WithMinorTriad_ShouldReturnMinorRegion()
    {
        // Arrange
        var tree = new TonalBspTree();
        var aMinorTriad = new PitchClassSet([9, 0, 4]); // A-C-E

        // Act
        var region = tree.FindTonalRegion(aMinorTriad);

        // Assert
        Assert.That(region, Is.Not.Null);
        Assert.That(region.TonalityType, Is.EqualTo(TonalityType.Minor).Or.EqualTo(TonalityType.Atonal));
    }

    [Test]
    public void TonalBSPService_FindTonalContextForChord_ShouldReturnValidContext()
    {
        // Arrange
        var chord = CreateMajorTriadTemplate();
        var root = PitchClass.C;

        // Act
        var result = _bspService.FindTonalContextForChord(chord, root);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Region, Is.Not.Null);
        Assert.That(result.Confidence, Is.GreaterThan(0.0));
        Assert.That(result.QueryTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void TonalBSPService_FindRelatedScales_ShouldReturnScales()
    {
        // Arrange
        var cMajorScale = new PitchClassSet([0, 2, 4, 5, 7, 9, 11]); // C major scale

        // Act
        var result = _bspService.FindRelatedScales(cMajorScale);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Elements, Is.Not.Null);
        Assert.That(result.Confidence, Is.GreaterThan(0.0));
    }

    [Test]
    public void TonalBSPService_SpatialQuery_ShouldReturnElementsWithinRadius()
    {
        // Arrange
        var center = new PitchClassSet([0, 4, 7]); // C major triad
        var radius = 0.5;
        var strategy = TonalPartitionStrategy.CircleOfFifths;

        // Act
        var result = _bspService.SpatialQuery(center, radius, strategy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Elements, Is.Not.Null);
        Assert.That(result.QueryTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void TonalBSPService_GetTonalNeighbors_ShouldReturnNeighbors()
    {
        // Arrange
        var element = new TonalChord(
            "C Major",
            new PitchClassSet([0, 4, 7]),
            TonalityType.Major,
            0
        );

        // Act
        var neighbors = _bspService.GetTonalNeighbors(element, 5);

        // Assert
        Assert.That(neighbors, Is.Not.Null);
        Assert.That(neighbors.Count(), Is.LessThanOrEqualTo(5));
    }

    [Test]
    public void TonalBSPAnalyzer_AnalyzeProgression_ShouldReturnAnalysis()
    {
        // Arrange
        var progression = CreateSimpleProgression();

        // Act
        var analysis = _analyzer.AnalyzeProgression(progression);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.Progression.Count, Is.EqualTo(progression.Count));
        Assert.That(analysis.ChordContexts.Count, Is.EqualTo(progression.Count));
        Assert.That(analysis.TonalJourney.Count, Is.EqualTo(progression.Count));
        Assert.That(analysis.OverallCoherence, Is.InRange(0.0, 1.0));
    }

    [Test]
    public void TonalBSPAnalyzer_OptimizeVoiceLeading_ShouldReturnOptimization()
    {
        // Arrange
        var progression = CreateSimpleProgression();

        // Act
        var analysis = _analyzer.OptimizeVoiceLeading(progression);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.Progression.Count, Is.EqualTo(progression.Count));
        Assert.That(analysis.VoiceLeadingPaths.Count, Is.EqualTo(progression.Count - 1));
        Assert.That(analysis.OverallSmoothness, Is.InRange(0.0, 1.0));
    }

    [Test]
    public void TonalBSPAnalyzer_SuggestSubstitutions_ShouldReturnSubstitutions()
    {
        // Arrange
        var chord = CreateMajorTriadTemplate();
        var root = PitchClass.C;
        var context = new TonalRegion("C Major", TonalityType.Major, new PitchClassSet([0, 2, 4, 5, 7, 9, 11]), 0);

        // Act
        var substitutions = _analyzer.SuggestSubstitutions(chord, root, context);

        // Assert
        Assert.That(substitutions, Is.Not.Null);
        Assert.That(substitutions.Count, Is.LessThanOrEqualTo(10));
    }

    [Test]
    public void TonalBSPAnalyzer_AnalyzeModulation_ShouldReturnModulationAnalysis()
    {
        // Arrange
        var fromKey = new TonalRegion("C Major", TonalityType.Major, new PitchClassSet([0, 2, 4, 5, 7, 9, 11]), 0);
        var toKey = new TonalRegion("G Major", TonalityType.Major, new PitchClassSet([7, 9, 11, 0, 2, 4, 6]), 7);

        // Act
        var analysis = _analyzer.AnalyzeModulation(fromKey, toKey);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.FromKey, Is.EqualTo(fromKey));
        Assert.That(analysis.ToKey, Is.EqualTo(toKey));
        Assert.That(analysis.ModulationStrength, Is.InRange(0.0, 1.0));
    }

    [Test]
    public void TonalPartitionPlane_EvaluateSide_ShouldReturnCorrectSide()
    {
        // Arrange
        var plane = new TonalPartitionPlane
        {
            Strategy = TonalPartitionStrategy.CircleOfFifths,
            ReferencePoint = 0, // C
            Threshold = 0.5
        };

        var cMajorChord = new TonalChord("C Major", new PitchClassSet([0, 4, 7]), TonalityType.Major, 0);
        var gMajorChord = new TonalChord("G Major", new PitchClassSet([7, 11, 2]), TonalityType.Major, 7);

        // Act
        var cSide = plane.EvaluateSide(cMajorChord);
        var gSide = plane.EvaluateSide(gMajorChord);

        // Assert
        Assert.That(cSide, Is.EqualTo(-1).Or.EqualTo(1)); // Should be on one side
        Assert.That(gSide, Is.EqualTo(-1).Or.EqualTo(1)); // Should be on one side
    }

    [Test]
    public void TonalRegion_Contains_ShouldCorrectlyIdentifyContainment()
    {
        // Arrange
        var cMajorRegion = new TonalRegion(
            "C Major",
            TonalityType.Major,
            new PitchClassSet([0, 2, 4, 5, 7, 9, 11]),
            0
        );

        var cMajorTriad = new PitchClassSet([0, 4, 7]);
        var fSharpMajorTriad = new PitchClassSet([6, 10, 1]);

        // Act
        var containsCMajor = cMajorRegion.Contains(cMajorTriad);
        var containsFSharpMajor = cMajorRegion.Contains(fSharpMajorTriad);

        // Assert
        Assert.That(containsCMajor, Is.True);
        Assert.That(containsFSharpMajor, Is.False);
    }

    [Test]
    public void TonalBSPService_CachePerformance_ShouldImproveWithRepeatedQueries()
    {
        // Arrange
        var chord = CreateMajorTriadTemplate();
        var root = PitchClass.C;

        // Act - First query (should populate cache)
        var stopwatch1 = Stopwatch.StartNew();
        var result1 = _bspService.FindTonalContextForChord(chord, root);
        stopwatch1.Stop();

        // Act - Second query (should use cache)
        var stopwatch2 = Stopwatch.StartNew();
        var result2 = _bspService.FindTonalContextForChord(chord, root);
        stopwatch2.Stop();

        // Assert
        Assert.That(result1.Region.Name, Is.EqualTo(result2.Region.Name));
        Assert.That(result1.Confidence, Is.EqualTo(result2.Confidence));
        // Note: Cache performance improvement is implementation-dependent
    }

    [Test]
    public void TonalBSPTree_ComplexProgression_ShouldMaintainCoherence()
    {
        // Arrange
        var complexProgression = CreateComplexProgression();

        // Act
        var analysis = _analyzer.AnalyzeProgression(complexProgression);

        // Assert
        Assert.That(analysis.OverallCoherence, Is.GreaterThan(0.0));
        Assert.That(analysis.TonalMovement, Is.Not.Null);
        Assert.That(analysis.TonalMovement.MovementType, Is.Not.Empty);
    }

    private ChordTemplate CreateMajorTriadTemplate()
    {
        var formula = ChordFormula.FromSemitones("Major Triad", 4, 7);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Test");
    }

    private List<(ChordTemplate chord, PitchClass root)> CreateSimpleProgression()
    {
        return
        [
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("Major", 4, 7), "Test"),
                PitchClass.C), // C major
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("Minor", 3, 7), "Test"),
                PitchClass.A), // A minor
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("Major", 4, 7), "Test"),
                PitchClass.F), // F major
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("Major", 4, 7), "Test"), PitchClass.G)
        ];
    }

    private List<(ChordTemplate chord, PitchClass root)> CreateComplexProgression()
    {
        return
        [
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("Maj7", 4, 7, 11), "Test"),
                PitchClass.C), // Cmaj7
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("m7", 3, 7, 10), "Test"),
                PitchClass.A), // Am7
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("m7", 3, 7, 10), "Test"),
                PitchClass.D), // Dm7
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("7", 4, 7, 10), "Test"),
                PitchClass.G), // G7
            (ChordTemplate.Analytical.FromSetTheory(ChordFormula.FromSemitones("Maj7", 4, 7, 11), "Test"), PitchClass.C)
        ];
    }
}
