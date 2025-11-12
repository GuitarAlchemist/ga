namespace BSPIntegrationTests;

using GA.BSP.Core;
using NUnit.Framework;

[TestFixture]
public class BspCoreIntegrationTests
{
    [SetUp]
    public void Setup()
    {
        _bspTree = new TonalBspTree();
        _bspService = new TonalBspService();
    }

    private TonalBspService? _bspService;
    private TonalBspTree? _bspTree;

    [Test]
    public void TonalBSPTree_Creation_ShouldSucceed()
    {
        // Arrange & Act
        var tree = new TonalBspTree();

        // Assert
        Assert.That(tree, Is.Not.Null);
        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root.Region, Is.Not.Null);
        Assert.That(tree.Root.Region.Name, Is.Not.Empty);
    }

    [Test]
    public void TonalBSPTree_FindTonalRegion_WithCMajorTriad_ShouldReturnValidRegion()
    {
        // Arrange
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);

        // Act
        var region = _bspTree!.FindTonalRegion(cMajorTriad);

        // Assert
        Assert.That(region, Is.Not.Null);
        Assert.That(region.Name, Is.Not.Empty);
        Assert.That(Enum.IsDefined(typeof(TonalityType), region.TonalityType), Is.True);
    }

    [Test]
    public void TonalBSPService_SpatialQuery_WithCMajorTriad_ShouldReturnResults()
    {
        // Arrange
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var radius = 0.5;
        var strategy = TonalPartitionStrategy.CircleOfFifths;

        // Act
        var result = _bspService!.SpatialQuery(cMajorTriad, radius, strategy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Region, Is.Not.Null);
        Assert.That(result.Elements, Is.Not.Null);
        Assert.That(result.QueryTime, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.Confidence, Is.GreaterThan(0));
    }

    [Test]
    public void TonalBSPService_FindTonalContextForChord_WithPitchClassSet_ShouldReturnValidContext()
    {
        // Arrange
        var aMinorTriad = new PitchClassSet([PitchClass.A, PitchClass.C, PitchClass.E]);

        // Act
        var context = _bspService!.FindTonalContextForChord(aMinorTriad);

        // Assert
        Assert.That(context, Is.Not.Null);
        Assert.That(context.Region, Is.Not.Null);
        Assert.That(context.Elements, Is.Not.Null);
        Assert.That(context.Elements.Count, Is.GreaterThan(0));
        Assert.That(context.Confidence, Is.GreaterThan(0));
        Assert.That(context.QueryTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void TonalBSPService_MultipleQueries_ShouldBeConsistent()
    {
        // Arrange
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);

        // Act - Run the same query multiple times
        var result1 = _bspService!.FindTonalContextForChord(cMajorTriad);
        var result2 = _bspService!.FindTonalContextForChord(cMajorTriad);

        // Assert - Results should be consistent
        Assert.That(result1.Region.Name, Is.EqualTo(result2.Region.Name));
        Assert.That(result1.Confidence, Is.EqualTo(result2.Confidence));
        Assert.That(result1.Elements.Count, Is.EqualTo(result2.Elements.Count));
    }

    [Test]
    public void TonalRegion_Contains_WithMatchingChord_ShouldReturnTrue()
    {
        // Arrange
        var cMajorScale = new PitchClassSet([
            PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.F,
            PitchClass.G, PitchClass.A, PitchClass.B
        ]);
        var region = new TonalRegion("C Major", TonalityType.Major, cMajorScale, (int)PitchClass.C);
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);

        // Act
        var contains = region.Contains(cMajorTriad);

        // Assert
        Assert.That(contains, Is.True);
    }

    [Test]
    public void TonalChord_Creation_ShouldHaveCorrectProperties()
    {
        // Arrange
        var pitchClassSet = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var tonalityType = TonalityType.Major;
        var tonalCenter = (int)PitchClass.C;

        // Act
        var chord = new TonalChord("C Major", pitchClassSet, tonalityType, tonalCenter);

        // Assert
        Assert.That(chord.Name, Is.EqualTo("C Major"));
        Assert.That(chord.PitchClassSet, Is.EqualTo(pitchClassSet));
        Assert.That(chord.TonalityType, Is.EqualTo(tonalityType));
        Assert.That(chord.TonalCenter, Is.EqualTo(tonalCenter));
        Assert.That(chord.TonalStrength, Is.EqualTo(1.0)); // Default value
    }

    [Test]
    public void TonalScale_Creation_ShouldHaveCorrectProperties()
    {
        // Arrange
        var pitchClassSet = new PitchClassSet([
            PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.F,
            PitchClass.G, PitchClass.A, PitchClass.B
        ]);
        var tonalityType = TonalityType.Major;
        var tonalCenter = (int)PitchClass.C;

        // Act
        var scale = new TonalScale("C Major Scale", pitchClassSet, tonalityType, tonalCenter);

        // Assert
        Assert.That(scale.Name, Is.EqualTo("C Major Scale"));
        Assert.That(scale.PitchClassSet, Is.EqualTo(pitchClassSet));
        Assert.That(scale.TonalityType, Is.EqualTo(tonalityType));
        Assert.That(scale.TonalCenter, Is.EqualTo(tonalCenter));
        Assert.That(scale.TonalStrength, Is.EqualTo(1.0)); // Default value
    }

    [Test]
    public void BSPIntegration_FullWorkflow_ShouldCompleteSuccessfully()
    {
        // Arrange - Create a chord progression
        var progression = new List<(string name, PitchClassSet pitchClassSet)>
        {
            ("C Major", new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G])),
            ("A Minor", new PitchClassSet([PitchClass.A, PitchClass.C, PitchClass.E])),
            ("F Major", new PitchClassSet([PitchClass.F, PitchClass.A, PitchClass.C])),
            ("G Major", new PitchClassSet([PitchClass.G, PitchClass.B, PitchClass.D]))
        };

        // Act - Analyze each chord and the progression
        var chordContexts = new List<TonalBspQueryResult>();

        foreach (var (name, pitchClassSet) in progression)
        {
            var context = _bspService!.FindTonalContextForChord(pitchClassSet);
            chordContexts.Add(context);
        }

        // Analyze progression by checking each chord's context
        var progressionContexts =
            progression.Select(p => _bspService!.FindTonalContextForChord(p.pitchClassSet)).ToList();

        // Assert - Verify the complete workflow
        Assert.That(chordContexts.Count, Is.EqualTo(4));
        Assert.That(progressionContexts.Count, Is.EqualTo(4));

        // Verify each chord has a valid context
        foreach (var context in chordContexts)
        {
            Assert.That(context.Region, Is.Not.Null);
            Assert.That(context.Confidence, Is.GreaterThan(0));
            Assert.That(context.Elements.Count, Is.GreaterThan(0));
        }

        // Verify progression contexts
        foreach (var context in progressionContexts)
        {
            Assert.That(context.Region, Is.Not.Null);
            Assert.That(context.Confidence, Is.GreaterThan(0));
        }
    }
}
