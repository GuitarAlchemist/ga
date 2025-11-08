namespace GaApi.Tests;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using Microsoft.Extensions.Logging;
using Models;
using Moq;
using Services;

[TestFixture]
public class VoicingFilterServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockCache = new Mock<ICachingService>();
        _mockLogger = new Mock<ILogger<VoicingFilterService>>();

        // Setup cache to always execute the factory function for List<VoicingWithAnalysis>
        _mockCache
            .Setup(c => c.GetOrCreateRegularAsync(It.IsAny<string>(),
                It.IsAny<Func<Task<List<VoicingWithAnalysis>>>>()))
            .Returns<string, Func<Task<List<VoicingWithAnalysis>>>>((key, factory) => factory());

        _service = new VoicingFilterService(_mockCache.Object, _mockLogger.Object);

        // Create a test chord template (C major triad)
        var scale = MajorScaleMode.Get(MajorScaleDegree.Ionian);
        _testTemplate = ChordTemplateFactory.CreateModalChords(scale).First();
    }

    private Mock<ICachingService> _mockCache = null!;
    private Mock<ILogger<VoicingFilterService>> _mockLogger = null!;
    private VoicingFilterService _service = null!;
    private ChordTemplate _testTemplate = null!;
    private readonly PitchClass _testRoot = PitchClass.C;

    [Test]
    public async Task GetVoicingsForChordAsync_ReturnsVoicings()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings, Is.Not.Empty, "Should return voicings");
    }

    [Test]
    public async Task GetVoicingsForChordAsync_AllVoicingsHaveAnalysis()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings, Is.Not.Empty);
        foreach (var voicing in voicings)
        {
            Assert.That(voicing.Physical, Is.Not.Null, "Physical analysis should not be null");
            Assert.That(voicing.Psychoacoustic, Is.Not.Null, "Psychoacoustic analysis should not be null");
            Assert.That(voicing.Positions, Is.Not.Null.And.Not.Empty, "Positions should not be null or empty");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithMaxDifficulty_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            MaxDifficulty = PlayabilityLevel.Beginner,
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => v.Physical.Playability <= PlayabilityLevel.Beginner),
                Is.True, "All voicings should be beginner level or easier");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithFretRange_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            FretRange = new FretRange(0, 5), // First 5 frets
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => v.Physical.LowestFret >= 0 && v.Physical.HighestFret <= 5),
                Is.True, "All voicings should be within fret range 0-5");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithNoOpenStrings_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            NoOpenStrings = true,
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => !v.Physical.HasOpenStrings),
                Is.True, "No voicings should have open strings");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithNoMutedStrings_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            NoMutedStrings = true,
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => !v.Physical.HasMutedStrings),
                Is.True, "No voicings should have muted strings");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithNoBarres_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            NoBarres = true,
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => !v.Physical.RequiresBarre),
                Is.True, "No voicings should require barre");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithMinConsonance_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            MinConsonance = 0.7,
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => v.Psychoacoustic.Consonance >= 0.7),
                Is.True, "All voicings should have consonance >= 0.7");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithStylePreference_FiltersCorrectly()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            StylePreference = "Jazz",
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            Assert.That(voicings.All(v => v.StyleTags.Contains("Jazz", StringComparer.OrdinalIgnoreCase)),
                Is.True, "All voicings should have Jazz style tag");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithLimit_RespectsLimit()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 5
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings.Count, Is.LessThanOrEqualTo(5), "Should respect limit");
    }

    [Test]
    public async Task GetVoicingsForChordAsync_VoicingsAreRankedByUtility()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Count > 1)
        {
            // Voicings should be ranked by utility score (descending)
            for (var i = 0; i < voicings.Count - 1; i++)
            {
                Assert.That(voicings[i].UtilityScore, Is.GreaterThanOrEqualTo(voicings[i + 1].UtilityScore),
                    $"Voicing at index {i} should have higher or equal utility score than voicing at index {i + 1}");
            }
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_AllVoicingsHaveValidUtilityScore()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings, Is.Not.Empty);
        foreach (var voicing in voicings)
        {
            Assert.That(voicing.UtilityScore, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0),
                "Utility score should be between 0 and 1");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_PhysicalAnalysisIsValid()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings, Is.Not.Empty);
        foreach (var voicing in voicings)
        {
            Assert.That(voicing.Physical.FretSpan, Is.GreaterThanOrEqualTo(0), "Fret span should be >= 0");
            Assert.That(voicing.Physical.LowestFret, Is.GreaterThanOrEqualTo(0), "Lowest fret should be >= 0");
            Assert.That(voicing.Physical.HighestFret, Is.GreaterThanOrEqualTo(voicing.Physical.LowestFret),
                "Highest fret should be >= lowest fret");
            Assert.That(voicing.Physical.StringCount, Is.GreaterThan(0), "String count should be > 0");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_PsychoacousticAnalysisIsValid()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings, Is.Not.Empty);
        foreach (var voicing in voicings)
        {
            Assert.That(voicing.Psychoacoustic.Consonance, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0),
                "Consonance should be between 0 and 1");
            Assert.That(voicing.Psychoacoustic.Brightness, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0),
                "Brightness should be between 0 and 1");
            Assert.That(voicing.Psychoacoustic.Clarity, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0),
                "Clarity should be between 0 and 1");
            Assert.That(voicing.Psychoacoustic.Register, Is.Not.Null.And.Not.Empty,
                "Register should not be null or empty");
            Assert.That(voicing.Psychoacoustic.Density, Is.Not.Null.And.Not.Empty,
                "Density should not be null or empty");
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_CacheIsUsed()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 20
        };

        // Act
        await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);

        // Assert
        _mockCache.Verify(
            c => c.GetOrCreateRegularAsync(It.IsAny<string>(), It.IsAny<Func<Task<List<VoicingWithAnalysis>>>>()),
            Times.Exactly(2),
            "Cache should be used");
    }

    [Test]
    public async Task GetVoicingsForChordAsync_StyleTagsAreAssigned()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            Limit = 50 // Get more voicings to increase chance of finding different styles
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        Assert.That(voicings, Is.Not.Empty);

        // At least some voicings should have style tags
        var voicingsWithTags = voicings.Where(v => v.StyleTags.Any()).ToList();
        Assert.That(voicingsWithTags, Is.Not.Empty, "At least some voicings should have style tags");
    }

    [Test]
    public async Task GetVoicingsForChordAsync_MultipleFilters_WorkTogether()
    {
        // Arrange
        var filters = new VoicingFilters
        {
            MaxDifficulty = PlayabilityLevel.Intermediate,
            FretRange = new FretRange(0, 12),
            NoBarres = true,
            MinConsonance = 0.5,
            Limit = 20
        };

        // Act
        var result = await _service.GetVoicingsForChordAsync(_testTemplate, _testRoot, filters);
        var voicings = result.ToList();

        // Assert
        if (voicings.Any())
        {
            foreach (var voicing in voicings)
            {
                Assert.That(voicing.Physical.Playability, Is.LessThanOrEqualTo(PlayabilityLevel.Intermediate),
                    "Playability should be intermediate or easier");
                Assert.That(voicing.Physical.LowestFret, Is.GreaterThanOrEqualTo(0), "Lowest fret should be >= 0");
                Assert.That(voicing.Physical.HighestFret, Is.LessThanOrEqualTo(12), "Highest fret should be <= 12");
                Assert.That(voicing.Physical.RequiresBarre, Is.False, "Should not require barre");
                Assert.That(voicing.Psychoacoustic.Consonance, Is.GreaterThanOrEqualTo(0.5),
                    "Consonance should be >= 0.5");
            }
        }
    }
}
