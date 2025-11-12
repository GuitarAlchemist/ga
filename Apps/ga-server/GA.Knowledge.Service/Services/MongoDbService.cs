namespace GA.Knowledge.Service.Services;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using GA.Knowledge.Service.Models;
using MongoDB.Bson;
using MongoDB.Driver;

public class MongoDbService
{
    private readonly MongoDbSettings _settings;

    public MongoDbService(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        var client = new MongoClient(_settings.ConnectionString);
        Database = client.GetDatabase(_settings.DatabaseName);
    }

    // Expose database for other services
    public IMongoDatabase Database { get; }

    public IMongoCollection<Chord> Chords =>
        Database.GetCollection<Chord>(_settings.Collections.Chords);

    public IMongoCollection<BsonDocument> ChordTemplates =>
        Database.GetCollection<BsonDocument>(_settings.Collections.ChordTemplates);

    public IMongoCollection<BsonDocument> Scales =>
        Database.GetCollection<BsonDocument>(_settings.Collections.Scales);

    public IMongoCollection<BsonDocument> Progressions =>
        Database.GetCollection<BsonDocument>(_settings.Collections.Progressions);

    // Music room collections
    public IMongoCollection<MusicRoomDocument> MusicRoomLayouts =>
        Database.GetCollection<MusicRoomDocument>("musicRoomLayouts");

    public IMongoCollection<RoomGenerationJob> RoomGenerationJobs =>
        Database.GetCollection<RoomGenerationJob>("roomGenerationJobs");

    // Chord query methods
    public async Task<List<Chord>> GetChordsByQualityAsync(string quality, int limit = 100)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.Quality, quality);
        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<List<Chord>> GetChordsByExtensionAsync(string extension, int limit = 100)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.Extension, extension);
        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<List<Chord>> GetChordsByStackingTypeAsync(string stackingType, int limit = 100)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.StackingType, stackingType);
        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<List<Chord>> GetChordsByQualityAndExtensionAsync(
        string quality,
        string extension,
        int limit = 100)
    {
        var filter = Builders<Chord>.Filter.And(
            Builders<Chord>.Filter.Eq(c => c.Quality, quality),
            Builders<Chord>.Filter.Eq(c => c.Extension, extension)
        );
        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<List<Chord>> GetChordsByPitchClassSetAsync(List<int> pitchClassSet, int limit = 100)
    {
        var bsonArray = new BsonArray(pitchClassSet);
        var filter = Builders<Chord>.Filter.Eq(c => c.PitchClassSet, bsonArray);
        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<List<Chord>> GetChordsByNoteCountAsync(int noteCount, int limit = 100)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.NoteCount, noteCount);
        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }


    public Task<List<Chord>> GetChordsByIntervalSemitonesAsync(List<int> semitones, int limit = 100)
    {
        // This is a complex query - for now, we'll skip it or implement it differently
        // You would need to query by the Intervals.Semitones array
        return Task.FromResult(new List<Chord>());
    }

    public async Task<List<Chord>> GetChordsByScaleAsync(string parentScale, int? scaleDegree = null, int limit = 100)
    {
        FilterDefinition<Chord> filter;

        if (scaleDegree.HasValue)
        {
            filter = Builders<Chord>.Filter.And(
                Builders<Chord>.Filter.Eq(c => c.ParentScale, parentScale),
                Builders<Chord>.Filter.Eq(c => c.ScaleDegree, scaleDegree.Value)
            );
        }
        else
        {
            filter = Builders<Chord>.Filter.Eq(c => c.ParentScale, parentScale);
        }

        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    // Advanced queries
    public async Task<Dictionary<string, long>> GetChordCountsByQualityAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Quality" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        var results = await Chords.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return results.ToDictionary(
            doc => doc["_id"].AsString,
            doc => (long)doc["count"].AsInt32
        );
    }

    public async Task<Dictionary<string, long>> GetChordCountsByStackingTypeAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$StackingType" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        var results = await Chords.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return results.ToDictionary(
            doc => doc["_id"].AsString,
            doc => (long)doc["count"].AsInt32
        );
    }

    public async Task<long> GetTotalChordCountAsync()
    {
        return await Chords.CountDocumentsAsync(new BsonDocument());
    }

    public async Task<List<string>> GetDistinctQualitiesAsync()
    {
        return await Chords.Distinct<string>("Quality", new BsonDocument()).ToListAsync();
    }

    public async Task<List<string>> GetDistinctExtensionsAsync()
    {
        return await Chords.Distinct<string>("Extension", new BsonDocument()).ToListAsync();
    }

    public async Task<List<string>> GetDistinctStackingTypesAsync()
    {
        return await Chords.Distinct<string>("StackingType", new BsonDocument()).ToListAsync();
    }

    public async Task<Chord?> GetChordByIdAsync(string id)
    {
        var filter = Builders<Chord>.Filter.Eq("_id", ObjectId.Parse(id));
        return await Chords.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<Chord>> GetSimilarChordsAsync(string chordId, int limit = 10)
    {
        try
        {
            // Get the chord first to find similar ones
            var chord = await GetChordByIdAsync(chordId);
            if (chord == null)
            {
                return [];
            }

            // Find chords with same quality or extension (excluding the original chord)
            var filter = Builders<Chord>.Filter.And(
                Builders<Chord>.Filter.Ne("Id", chordId),
                Builders<Chord>.Filter.Or(
                    Builders<Chord>.Filter.Eq("Quality", chord.Quality),
                    Builders<Chord>.Filter.Eq("Extension", chord.Extension)
                )
            );

            return await Chords.Find(filter).Limit(limit).ToListAsync();
        }
        catch (Exception)
        {
            // If chord not found or any error, return empty list
            // The calling service will handle this appropriately
            return [];
        }
    }

    public async Task<ChordStatistics> GetChordStatisticsAsync()
    {
        var totalCount = await GetTotalChordCountAsync();

        // Get quality distribution
        var qualityPipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$Quality",
                ["count"] = new BsonDocument("$sum", 1)
            })
        };
        var qualityResults = await Chords.Aggregate<BsonDocument>(qualityPipeline).ToListAsync();
        var qualityDistribution = qualityResults.ToDictionary(
            doc => doc["_id"].AsString,
            doc => doc["count"].AsInt32
        );

        // Get extension distribution
        var extensionPipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$Extension",
                ["count"] = new BsonDocument("$sum", 1)
            })
        };
        var extensionResults = await Chords.Aggregate<BsonDocument>(extensionPipeline).ToListAsync();
        var extensionDistribution = extensionResults.ToDictionary(
            doc => doc["_id"].AsString,
            doc => doc["count"].AsInt32
        );

        // Get stacking type distribution
        var stackingPipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$StackingType",
                ["count"] = new BsonDocument("$sum", 1)
            })
        };
        var stackingResults = await Chords.Aggregate<BsonDocument>(stackingPipeline).ToListAsync();
        var stackingDistribution = stackingResults.ToDictionary(
            doc => doc["_id"].AsString,
            doc => doc["count"].AsInt32
        );

        // Get note count distribution
        var noteCountPipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$NoteCount",
                ["count"] = new BsonDocument("$sum", 1)
            })
        };
        var noteCountResults = await Chords.Aggregate<BsonDocument>(noteCountPipeline).ToListAsync();
        var noteCountDistribution = noteCountResults.ToDictionary(
            doc => doc["_id"].AsInt32,
            doc => doc["count"].AsInt32
        );

        return new ChordStatistics
        {
            TotalChords = totalCount,
            QualityDistribution = qualityDistribution,
            ExtensionDistribution = extensionDistribution,
            StackingTypeDistribution = stackingDistribution,
            NoteCountDistribution = noteCountDistribution
        };
    }

    public async Task<List<Chord>> SearchChordsAsync(string query, int limit = 100)
    {
        // Create text search filter
        var filter = Builders<Chord>.Filter.Or(
            Builders<Chord>.Filter.Regex(c => c.Name, new BsonRegularExpression(query, "i")),
            Builders<Chord>.Filter.Regex(c => c.Quality, new BsonRegularExpression(query, "i")),
            Builders<Chord>.Filter.Regex(c => c.Extension, new BsonRegularExpression(query, "i"))
        );

        return await Chords.Find(filter).Limit(limit).ToListAsync();
    }

    public string GetConnectionString()
    {
        return _settings.ConnectionString;
    }

    public string GetDatabaseName()
    {
        return _settings.DatabaseName;
    }

    // ========================================
    // STREAMING METHODS (IAsyncEnumerable)
    // ========================================

    /// <summary>
    ///     Stream chords by quality (progressive delivery)
    /// </summary>
    public async IAsyncEnumerable<Chord> GetChordsByQualityStreamAsync(
        string quality,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.Quality, quality);
        using var cursor = await Chords.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var chord in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Stream chords by extension (progressive delivery)
    /// </summary>
    public async IAsyncEnumerable<Chord> GetChordsByExtensionStreamAsync(
        string extension,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.Extension, extension);
        using var cursor = await Chords.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var chord in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Stream chords by stacking type (progressive delivery)
    /// </summary>
    public async IAsyncEnumerable<Chord> GetChordsByStackingTypeStreamAsync(
        string stackingType,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.StackingType, stackingType);
        using var cursor = await Chords.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var chord in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Stream chords by pitch class set (progressive delivery)
    /// </summary>
    public async IAsyncEnumerable<Chord> GetChordsByPitchClassSetStreamAsync(
        List<int> pitchClassSet,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bsonArray = new BsonArray(pitchClassSet);
        var filter = Builders<Chord>.Filter.Eq(c => c.PitchClassSet, bsonArray);
        using var cursor = await Chords.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var chord in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Stream chords by note count (progressive delivery)
    /// </summary>
    public async IAsyncEnumerable<Chord> GetChordsByNoteCountStreamAsync(
        int noteCount,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = Builders<Chord>.Filter.Eq(c => c.NoteCount, noteCount);
        using var cursor = await Chords.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var chord in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chord;
            }
        }
    }

    /// <summary>
    ///     Stream chords by parent scale (progressive delivery)
    /// </summary>
    public async IAsyncEnumerable<Chord> GetChordsByScaleStreamAsync(
        string parentScale,
        int? scaleDegree,
        int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        FilterDefinition<Chord> filter;

        if (scaleDegree.HasValue)
        {
            filter = Builders<Chord>.Filter.And(
                Builders<Chord>.Filter.Eq(c => c.ParentScale, parentScale),
                Builders<Chord>.Filter.Eq(c => c.ScaleDegree, scaleDegree.Value)
            );
        }
        else
        {
            filter = Builders<Chord>.Filter.Eq(c => c.ParentScale, parentScale);
        }

        using var cursor = await Chords.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var chord in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chord;
            }
        }
    }
}
