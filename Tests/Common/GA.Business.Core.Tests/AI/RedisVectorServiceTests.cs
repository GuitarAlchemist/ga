namespace GA.Business.Core.Tests.AI;

using Business.AI;
using Core.Atonal;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Fretboard.Shapes;

/// <summary>
///     Integration tests for Redis Vector Service
///     Tests vector indexing, similarity search, caching, and session management
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Redis")]
public class RedisVectorServiceTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Check if Redis is available
        try
        {
            // TODO: Initialize Redis connection
            // _redisService = new RedisVectorService(connectionString);
            // _redisAvailable = await _redisService.PingAsync();
            _redisAvailable = false; // Set to false until Redis Stack is configured
        }
        catch
        {
            _redisAvailable = false;
        }

        if (!_redisAvailable)
        {
            Assert.Ignore("Redis Stack not available. Skipping integration tests.");
        }
    }

    [SetUp]
    public void SetUp()
    {
        if (!_redisAvailable)
        {
            Assert.Ignore("Redis Stack not available");
        }
    }

    private IRedisVectorService? _redisService;
    private bool _redisAvailable;

    [Test]
    public async Task IndexPitchClassSetsAsync_ShouldIndexAllSets()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        // Act
        await _redisService!.IndexPitchClassSetsAsync();

        // Assert
        // Verify that all pitch class sets are indexed
        var testSet = new PitchClassSet([0, 4, 7]); // C major triad
        var similar = await _redisService.FindSimilarPitchClassSetsAsync(testSet.IntervalClassVector, 5);

        Assert.That(similar, Is.Not.Null);
        Assert.That(similar, Is.Not.Empty);
        Assert.That(similar.Count(), Is.LessThanOrEqualTo(5));
    }

    [Test]
    public async Task IndexShapesAsync_ShouldIndexAllShapes()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var shapes = CreateTestShapes();

        // Act
        await _redisService!.IndexShapesAsync(shapes);

        // Assert
        var searchResults = await _redisService.SearchShapesAsync("easy C major box shape");
        Assert.That(searchResults, Is.Not.Null);
        Assert.That(searchResults, Is.Not.Empty);
    }

    [Test]
    public async Task FindSimilarPitchClassSetsAsync_ShouldReturnSimilarSets()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);
        await _redisService!.IndexPitchClassSetsAsync();

        var querySet = new PitchClassSet([0, 4, 7]); // C major triad

        // Act
        var results = (await _redisService.FindSimilarPitchClassSetsAsync(querySet.IntervalClassVector, 5)).ToList();

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.LessThanOrEqualTo(5));

        // First result should be the query set itself or very similar
        Assert.That(results[0].Distance, Is.LessThan(0.1));
    }

    [Test]
    public async Task FindSimilarShapesAsync_ShouldReturnSimilarShapes()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var shapes = CreateTestShapes();
        await _redisService!.IndexShapesAsync(shapes);

        var queryShape = shapes[0];
        var options = new ShapeSearchOptions { MaxResults = 3 };

        // Act
        var results = (await _redisService.FindSimilarShapesAsync(queryShape, options)).ToList();

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.LessThanOrEqualTo(3));

        // Results should be ordered by similarity
        for (var i = 0; i < results.Count - 1; i++)
        {
            Assert.That(results[i].Distance, Is.LessThanOrEqualTo(results[i + 1].Distance));
        }
    }

    [Test]
    public async Task SearchShapesAsync_ShouldReturnRelevantShapes()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var shapes = CreateTestShapes();
        await _redisService!.IndexShapesAsync(shapes);

        // Act
        var results = await _redisService.SearchShapesAsync("easy C major box shape near 5th fret");

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results, Is.Not.Empty);

        // Results should have high ergonomics (easy)
        Assert.That(results.All(s => s.Ergonomics > 0.5), Is.True);
    }

    [Test]
    public async Task CacheHeatMapAsync_ShouldStoreAndRetrieve()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var shapeId = "test-shape-123";
        var optionsHash = "options-hash-456";
        var heatMap = new double[12, 6];

        // Fill with test data
        for (var i = 0; i < 12; i++)
        {
            for (var j = 0; j < 6; j++)
            {
                heatMap[i, j] = i * 0.1 + j * 0.01;
            }
        }

        // Act
        await _redisService!.CacheHeatMapAsync(shapeId, optionsHash, heatMap, TimeSpan.FromMinutes(5));
        var retrieved = await _redisService.GetCachedHeatMapAsync(shapeId, optionsHash);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.GetLength(0), Is.EqualTo(12));
        Assert.That(retrieved.GetLength(1), Is.EqualTo(6));

        // Verify data integrity
        for (var i = 0; i < 12; i++)
        {
            for (var j = 0; j < 6; j++)
            {
                Assert.That(retrieved[i, j], Is.EqualTo(heatMap[i, j]).Within(0.0001));
            }
        }
    }

    [Test]
    public async Task GetCachedHeatMapAsync_ShouldReturnNullForMissingKey()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var shapeId = "non-existent-shape";
        var optionsHash = "non-existent-hash";

        // Act
        var result = await _redisService!.GetCachedHeatMapAsync(shapeId, optionsHash);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CacheHeatMapAsync_ShouldExpireAfterTTL()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var shapeId = "test-shape-ttl";
        var optionsHash = "options-hash-ttl";
        var heatMap = new double[12, 6];
        var ttl = TimeSpan.FromSeconds(2);

        // Act
        await _redisService!.CacheHeatMapAsync(shapeId, optionsHash, heatMap, ttl);

        // Verify it exists immediately
        var immediate = await _redisService.GetCachedHeatMapAsync(shapeId, optionsHash);
        Assert.That(immediate, Is.Not.Null);

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        var expired = await _redisService.GetCachedHeatMapAsync(shapeId, optionsHash);

        // Assert
        Assert.That(expired, Is.Null);
    }

    [Test]
    public async Task StoreUserSessionAsync_ShouldStoreAndRetrieve()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var userId = "test-user-123";
        var session = new UserSession
        {
            UserId = userId,
            SkillLevel = 0.7,
            PreferredDiagness = 0.4,
            MaxComfortableSpan = 5,
            PracticeHistory =
            [
                new()
                {
                    ShapeId = "shape-1",
                    Timestamp = DateTime.UtcNow,
                    SuccessRate = 0.85
                }
            ]
        };

        // Act
        await _redisService!.StoreUserSessionAsync(userId, session);
        var retrieved = await _redisService.GetUserSessionAsync(userId);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.UserId, Is.EqualTo(userId));
        Assert.That(retrieved.SkillLevel, Is.EqualTo(0.7).Within(0.001));
        Assert.That(retrieved.PreferredDiagness, Is.EqualTo(0.4).Within(0.001));
        Assert.That(retrieved.MaxComfortableSpan, Is.EqualTo(5));
        Assert.That(retrieved.PracticeHistory, Has.Count.EqualTo(1));
        Assert.That(retrieved.PracticeHistory[0].ShapeId, Is.EqualTo("shape-1"));
    }

    [Test]
    public async Task GetUserSessionAsync_ShouldReturnNullForNonExistentUser()
    {
        // Arrange
        Assert.That(_redisService, Is.Not.Null);

        var userId = "non-existent-user";

        // Act
        var result = await _redisService!.GetUserSessionAsync(userId);

        // Assert
        Assert.That(result, Is.Null);
    }

    private static List<FretboardShape> CreateTestShapes()
    {
        const string tuningId = "standard-6-string";

        // C major triad - open position
        var shape1Positions = new List<PositionLocation>
        {
            new(Str.FromValue(1), Fret.Open),
            new(Str.FromValue(2), Fret.FromValue(2)),
            new(Str.FromValue(3), Fret.FromValue(2)),
            new(Str.FromValue(4), Fret.FromValue(1)),
            new(Str.FromValue(5), Fret.Open),
            new(Str.FromValue(6), Fret.Open)
        };
        var pcs1 = new PitchClassSet([0, 4, 7]);

        // C minor triad - open position
        var shape2Positions = new List<PositionLocation>
        {
            new(Str.FromValue(1), Fret.Open),
            new(Str.FromValue(2), Fret.FromValue(2)),
            new(Str.FromValue(3), Fret.FromValue(2)),
            new(Str.FromValue(4), Fret.Open),
            new(Str.FromValue(5), Fret.Open),
            new(Str.FromValue(6), Fret.Open)
        };
        var pcs2 = new PitchClassSet([0, 3, 7]);

        // C major triad - 5th position
        var shape3Positions = new List<PositionLocation>
        {
            new(Str.FromValue(1), Fret.FromValue(3)),
            new(Str.FromValue(2), Fret.FromValue(5)),
            new(Str.FromValue(3), Fret.FromValue(5)),
            new(Str.FromValue(4), Fret.FromValue(4)),
            new(Str.FromValue(5), Fret.FromValue(3)),
            new(Str.FromValue(6), Fret.FromValue(3))
        };
        var pcs3 = new PitchClassSet([0, 4, 7]);

        return
        [
            new()
            {
                Id = FretboardShape.GenerateId(tuningId, shape1Positions),
                TuningId = tuningId,
                PitchClassSet = pcs1,
                Icv = pcs1.IntervalClassVector,
                Positions = shape1Positions,
                StringMask = FretboardShape.ComputeStringMask(shape1Positions),
                MinFret = 0,
                MaxFret = 2,
                Diagness = FretboardShape.ComputeDiagness(shape1Positions),
                Ergonomics = FretboardShape.ComputeErgonomics(shape1Positions, 2),
                FingerCount = 3,
                Tags = new Dictionary<string, string> { ["shape"] = "box", ["difficulty"] = "easy" }
            },

            new()
            {
                Id = FretboardShape.GenerateId(tuningId, shape2Positions),
                TuningId = tuningId,
                PitchClassSet = pcs2,
                Icv = pcs2.IntervalClassVector,
                Positions = shape2Positions,
                StringMask = FretboardShape.ComputeStringMask(shape2Positions),
                MinFret = 0,
                MaxFret = 2,
                Diagness = FretboardShape.ComputeDiagness(shape2Positions),
                Ergonomics = FretboardShape.ComputeErgonomics(shape2Positions, 2),
                FingerCount = 2,
                Tags = new Dictionary<string, string> { ["shape"] = "box", ["difficulty"] = "easy" }
            },

            new()
            {
                Id = FretboardShape.GenerateId(tuningId, shape3Positions),
                TuningId = tuningId,
                PitchClassSet = pcs3,
                Icv = pcs3.IntervalClassVector,
                Positions = shape3Positions,
                StringMask = FretboardShape.ComputeStringMask(shape3Positions),
                MinFret = 3,
                MaxFret = 5,
                Diagness = FretboardShape.ComputeDiagness(shape3Positions),
                Ergonomics = FretboardShape.ComputeErgonomics(shape3Positions, 2),
                FingerCount = 4,
                Tags = new Dictionary<string, string> { ["shape"] = "box", ["difficulty"] = "medium" }
            }
        ];
    }
}
