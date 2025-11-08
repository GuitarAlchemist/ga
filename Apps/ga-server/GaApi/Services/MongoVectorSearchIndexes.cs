namespace GaApi.Services;

using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
///     MongoDB vector search index definitions for semantic search
///     Provides methods to create and query vector search indexes for chord voicings and templates
/// </summary>
public static class MongoVectorSearchIndexes
{
    /// <summary>
    ///     Create vector search index for chord voicings collection
    /// </summary>
    public static async Task CreateChordVoicingsIndexAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<BsonDocument>("chord_voicings");

        var indexDefinition = new BsonDocument
        {
            {
                "mappings", new BsonDocument
                {
                    { "dynamic", true },
                    {
                        "fields", new BsonDocument
                        {
                            {
                                "embedding", new BsonDocument
                                {
                                    { "type", "knnVector" },
                                    { "dimensions", 1536 },
                                    { "similarity", "cosine" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var command = new BsonDocument
        {
            { "createSearchIndexes", "chord_voicings" },
            {
                "indexes", new BsonArray
                {
                    new BsonDocument
                    {
                        { "name", "chord_voicing_vector_index" },
                        { "definition", indexDefinition }
                    }
                }
            }
        };

        try
        {
            await database.RunCommandAsync<BsonDocument>(command);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexAlreadyExists")
        {
            // Index already exists, ignore
        }
    }

    /// <summary>
    ///     Create vector search index for chord templates collection
    /// </summary>
    public static async Task CreateChordTemplatesIndexAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<BsonDocument>("chord_templates");

        var indexDefinition = new BsonDocument
        {
            {
                "mappings", new BsonDocument
                {
                    { "dynamic", true },
                    {
                        "fields", new BsonDocument
                        {
                            {
                                "embedding", new BsonDocument
                                {
                                    { "type", "knnVector" },
                                    { "dimensions", 1536 },
                                    { "similarity", "cosine" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var command = new BsonDocument
        {
            { "createSearchIndexes", "chord_templates" },
            {
                "indexes", new BsonArray
                {
                    new BsonDocument
                    {
                        { "name", "chord_template_vector_index" },
                        { "definition", indexDefinition }
                    }
                }
            }
        };

        try
        {
            await database.RunCommandAsync<BsonDocument>(command);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexAlreadyExists")
        {
            // Index already exists, ignore
        }
    }

    /// <summary>
    ///     Perform vector search on chord voicings collection
    /// </summary>
    public static async Task<List<BsonDocument>> VectorSearchChordVoicingsAsync(
        IMongoDatabase database,
        float[] queryEmbedding,
        int limit = 10,
        BsonDocument? filter = null)
    {
        var collection = database.GetCollection<BsonDocument>("chord_voicings");

        var pipeline = new List<BsonDocument>
        {
            new("$vectorSearch", new BsonDocument
            {
                { "index", "chord_voicing_vector_index" },
                { "path", "embedding" },
                { "queryVector", new BsonArray(queryEmbedding) },
                { "numCandidates", limit * 10 },
                { "limit", limit }
            })
        };

        if (filter != null)
        {
            pipeline.Add(new BsonDocument("$match", filter));
        }

        var results = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return results;
    }

    /// <summary>
    ///     Build MongoDB filter from search criteria
    /// </summary>
    public static BsonDocument? BuildMongoFilter(
        double? minPlayabilityScore = null,
        double? maxPlayabilityScore = null,
        string? difficulty = null,
        bool? isPlayable = null,
        bool? hasBarreChord = null,
        bool? usesPinky = null,
        bool? isErgonomic = null,
        int? minFret = null,
        int? maxFret = null,
        int? maxFretSpan = null,
        string? chordQuality = null)
    {
        var filters = new List<BsonDocument>();

        if (minPlayabilityScore.HasValue)
        {
            filters.Add(new BsonDocument("playabilityScore", new BsonDocument("$gte", minPlayabilityScore.Value)));
        }

        if (maxPlayabilityScore.HasValue)
        {
            filters.Add(new BsonDocument("playabilityScore", new BsonDocument("$lte", maxPlayabilityScore.Value)));
        }

        if (!string.IsNullOrEmpty(difficulty))
        {
            filters.Add(new BsonDocument("difficulty", difficulty));
        }

        if (isPlayable.HasValue)
        {
            filters.Add(new BsonDocument("isPlayable", isPlayable.Value));
        }

        if (hasBarreChord.HasValue)
        {
            filters.Add(new BsonDocument("hasBarreChord", hasBarreChord.Value));
        }

        if (usesPinky.HasValue)
        {
            filters.Add(new BsonDocument("usesPinky", usesPinky.Value));
        }

        if (isErgonomic.HasValue)
        {
            filters.Add(new BsonDocument("isErgonomic", isErgonomic.Value));
        }

        if (minFret.HasValue)
        {
            filters.Add(new BsonDocument("lowestFret", new BsonDocument("$gte", minFret.Value)));
        }

        if (maxFret.HasValue)
        {
            filters.Add(new BsonDocument("highestFret", new BsonDocument("$lte", maxFret.Value)));
        }

        if (maxFretSpan.HasValue)
        {
            filters.Add(new BsonDocument("fretSpan", new BsonDocument("$lte", maxFretSpan.Value)));
        }

        if (!string.IsNullOrEmpty(chordQuality))
        {
            filters.Add(new BsonDocument("quality", chordQuality));
        }

        if (filters.Count == 0)
        {
            return null;
        }

        if (filters.Count == 1)
        {
            return filters[0];
        }

        return new BsonDocument("$and", new BsonArray(filters));
    }
}
