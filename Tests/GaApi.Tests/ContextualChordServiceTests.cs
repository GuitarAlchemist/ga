namespace GaApi.Tests;

using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using Microsoft.Extensions.Logging;
using Models;
using Moq;
using Services;
using Services.ChordQuery;

[TestFixture]
public class ContextualChordServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockCache = new Mock<ICachingService>();
        _mockLogger = new Mock<ILogger<ContextualChordService>>();
        _mockPlanner = new Mock<IChordQueryPlanner>();

        // Setup cache to always execute the factory function for List<ChordInContext>
        _mockCache
            .Setup(c => c.GetOrCreateRegularAsync(It.IsAny<string>(), It.IsAny<Func<Task<List<ChordInContext>>>>()))
            .Returns<string, Func<Task<List<ChordInContext>>>>((key, factory) => factory());

        // Setup planner to return a plan with all generators
        _mockPlanner
            .Setup(p => p.CreatePlan(It.IsAny<ChordQuery>()))
            .Returns<ChordQuery>(query => new ChordQueryPlan
            {
                CacheKey = "test_cache_key",
                GeneratorsToInvoke = new List<ChordGeneratorType>
                {
                    ChordGeneratorType.Diatonic,
                    ChordGeneratorType.Borrowed,
                    ChordGeneratorType.SecondaryDominants,
                    ChordGeneratorType.SecondaryTwoFive
                },
                FiltersToApply = new List<ChordFilterType>(),
                Query = query
            });

        _service = new ContextualChordService(_mockCache.Object, _mockPlanner.Object, _mockLogger.Object);
    }

    private Mock<ICachingService> _mockCache = null!;
    private Mock<ILogger<ContextualChordService>> _mockLogger = null!;
    private Mock<IChordQueryPlanner> _mockPlanner = null!;
    private ContextualChordService _service = null!;

    [Test]
    public async Task GetChordsForKeyAsync_CMajor_ReturnsSevenDiatonicChords()
    {
        // Arrange
        var key = Key.Major.MajorItems.First(); // C Major
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            StackingType = ChordStackingType.Tertian,
            OnlyNaturallyOccurring = true, // Only diatonic chords
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords, Is.Not.Empty, "Should return chords");
        Assert.That(chords.Count, Is.EqualTo(7), "Should return 7 diatonic chords");

        // Verify all chords have context
        Assert.That(chords.All(c => c.Context != null), Is.True, "All chords should have context");
        Assert.That(chords.All(c => c.Context!.Level == ContextLevel.Key), Is.True,
            "All chords should have key context");

        // Verify scale degrees
        Assert.That(chords.All(c => c.ScaleDegree.HasValue), Is.True, "All chords should have scale degree");
        Assert.That(chords.Select(c => c.ScaleDegree!.Value), Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7 }),
            "Should have all 7 scale degrees");
    }

    [Test]
    public async Task GetChordsForKeyAsync_WithOnlyNaturallyOccurring_ReturnsOnlyDiatonicChords()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            OnlyNaturallyOccurring = true,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords.All(c => c.IsNaturallyOccurring), Is.True, "All chords should be naturally occurring");
    }

    [Test]
    public async Task GetChordsForKeyAsync_WithMinCommonality_FiltersLowCommonalityChords()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            MinCommonality = 0.5,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords.All(c => c.Commonality >= 0.5), Is.True, "All chords should have commonality >= 0.5");
    }

    [Test]
    public async Task GetChordsForScaleAsync_Ionian_ReturnsModalChords()
    {
        // Arrange
        var scale = MajorScaleMode.Get(MajorScaleDegree.Ionian);
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForScaleAsync(scale, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords, Is.Not.Empty, "Should return chords");
        Assert.That(chords.Count, Is.EqualTo(7), "Should return 7 modal chords");

        // Verify all chords have mode context
        Assert.That(chords.All(c => c.Context != null), Is.True, "All chords should have context");
        Assert.That(chords.All(c => c.Context!.Level == ContextLevel.Mode), Is.True,
            "All chords should have mode context");
    }

    [Test]
    public async Task GetChordsForModeAsync_Dorian_ReturnsModalChords()
    {
        // Arrange
        var mode = MajorScaleMode.Get(MajorScaleDegree.Dorian);
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForModeAsync(mode, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords, Is.Not.Empty, "Should return chords");
        Assert.That(chords.All(c => c.IsNaturallyOccurring), Is.True, "All modal chords should be naturally occurring");
    }

    [Test]
    public async Task GetChordsForKeyAsync_WithLimit_RespectsLimit()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            Limit = 3
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords.Count, Is.LessThanOrEqualTo(3), "Should respect limit");
    }

    [Test]
    public async Task GetChordsForKeyAsync_Triads_ReturnsTriads()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Triad,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords, Is.Not.Empty, "Should return triads");
        // Triads should have 3 notes (root + 2 intervals)
        Assert.That(chords.All(c => c.Template.Formula.Intervals.Count == 2), Is.True, "All chords should be triads");
    }

    [Test]
    public async Task GetChordsForKeyAsync_Ninths_ReturnsNinthChords()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Ninth,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords, Is.Not.Empty, "Should return ninth chords");
        // Ninth chords should have 5 notes (root + 4 intervals)
        Assert.That(chords.All(c => c.Template.Formula.Intervals.Count == 4), Is.True,
            "All chords should be ninth chords");
    }

    [Test]
    public async Task GetChordsForKeyAsync_QuartalStacking_ReturnsQuartalChords()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            StackingType = ChordStackingType.Quartal,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        Assert.That(chords, Is.Not.Empty, "Should return quartal chords");
        Assert.That(chords.All(c => c.Template.StackingType == GA.Business.Core.Chords.ChordStackingType.Quartal),
            Is.True, "All chords should be quartal");
    }

    [Test]
    public async Task GetChordsForKeyAsync_CacheIsUsed()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            Limit = 50
        };

        // Act
        await _service.GetChordsForKeyAsync(key, filters);
        await _service.GetChordsForKeyAsync(key, filters);

        // Assert
        _mockCache.Verify(
            c => c.GetOrCreateRegularAsync(It.IsAny<string>(), It.IsAny<Func<Task<List<ChordInContext>>>>()),
            Times.Exactly(2),
            "Cache should be used");
    }

    [Test]
    public async Task GetChordsForKeyAsync_ChordsAreRankedByCommonality()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        // Chords should be ranked by commonality (descending)
        for (var i = 0; i < chords.Count - 1; i++)
        {
            Assert.That(chords[i].Commonality, Is.GreaterThanOrEqualTo(chords[i + 1].Commonality),
                $"Chord at index {i} should have higher or equal commonality than chord at index {i + 1}");
        }
    }

    [Test]
    public async Task GetChordsForKeyAsync_AllChordsHaveRequiredProperties()
    {
        // Arrange
        var key = Key.Major.MajorItems.First();
        var filters = new ChordFilters
        {
            Extension = ChordExtension.Seventh,
            Limit = 50
        };

        // Act
        var result = await _service.GetChordsForKeyAsync(key, filters);
        var chords = result.ToList();

        // Assert
        foreach (var chord in chords)
        {
            Assert.That(chord.Template, Is.Not.Null, "Template should not be null");
            Assert.That(chord.ContextualName, Is.Not.Null.And.Not.Empty, "ContextualName should not be null or empty");

            // ScaleDegree is only required for naturally occurring (diatonic) chords
            if (chord.IsNaturallyOccurring)
            {
                Assert.That(chord.ScaleDegree, Is.Not.Null, "Diatonic chords should have a scale degree");
            }

            Assert.That(chord.Commonality, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0),
                "Commonality should be between 0 and 1");
            Assert.That(chord.RomanNumeral, Is.Not.Null.And.Not.Empty, "RomanNumeral should not be null or empty");
            Assert.That(chord.FunctionalDescription, Is.Not.Null.And.Not.Empty,
                "FunctionalDescription should not be null or empty");
        }
    }
}
