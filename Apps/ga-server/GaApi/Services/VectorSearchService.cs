namespace GaApi.Services;

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;

public class VectorSearchService
{
    private readonly IMongoDatabase _database;
    private readonly string _embeddingModel;
    private readonly LocalEmbeddingService? _localEmbedding;
    private readonly ILogger<VectorSearchService> _logger;
    private readonly AzureOpenAIClient? _openAiClient;
    private readonly MongoDbSettings _settings;
    private readonly bool _useLocal;

    public VectorSearchService(
        IOptions<MongoDbSettings> settings,
        IConfiguration configuration,
        LocalEmbeddingService localEmbedding,
        ILogger<VectorSearchService> logger)
    {
        _settings = settings.Value;
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
        _logger = logger;
        _localEmbedding = localEmbedding;

        // Initialize OpenAI client if API key is configured
        var apiKey = configuration["OpenAI:ApiKey"];
        _embeddingModel = configuration["OpenAI:Model"] ?? "text-embedding-3-small";

        if (!string.IsNullOrEmpty(apiKey))
        {
            _openAiClient = new AzureOpenAIClient(new Uri("https://api.openai.com/v1"), new AzureKeyCredential(apiKey));
            _useLocal = false;
            _logger.LogInformation("Using OpenAI embeddings");
        }
        else if (_localEmbedding.IsAvailable)
        {
            _useLocal = true;
            _logger.LogInformation("Using local embeddings");
        }
        else
        {
            _logger.LogWarning("No embedding service available");
        }
    }

    private IMongoCollection<BsonDocument> ChordsCollection =>
        _database.GetCollection<BsonDocument>(_settings.Collections.Chords);

    private IMongoCollection<BsonDocument> ChordTemplatesCollection =>
        _database.GetCollection<BsonDocument>(_settings.Collections.ChordTemplates);

    /// <summary>
    ///     Generate embedding for a text query
    /// </summary>
    public async Task<double[]> GenerateEmbeddingAsync(string text)
    {
        switch (_useLocal)
        {
            case true when _localEmbedding != null:
            {
                // Use local embedding model
                var embedding = _localEmbedding.GenerateEmbedding(text);
                return await Task.FromResult(embedding.Select(x => (double)x).ToArray());
            }
            default:
            {
                if (_openAiClient == null)
                {
                    throw new InvalidOperationException(
                        "No embedding service available. Please configure OpenAI API key or run LocalEmbedding tool.");
                }

                // Use OpenAI
                var embeddingClient = _openAiClient.GetEmbeddingClient(_embeddingModel);
                var response = await embeddingClient.GenerateEmbeddingAsync(text);
                var floats = response.Value.ToFloats();
                return [.. floats.ToArray().Select(f => (double)f)];
            }
        }
    }

    /// <summary>
    ///     Perform semantic search using vector similarity
    /// </summary>
    public async Task<List<ChordSearchResult>> SemanticSearchAsync(
        string query,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            // Generate embedding for query
            var queryEmbedding = await GenerateEmbeddingAsync(query);

            // Perform vector search
            var pipeline = new[]
            {
                new BsonDocument("$vectorSearch", new BsonDocument
                {
                    { "index", "chord_vector_index" },
                    { "path", "Embedding" },
                    { "queryVector", new BsonArray(queryEmbedding) },
                    { "numCandidates", numCandidates },
                    { "limit", limit }
                }),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Id", 1 },
                    { "Name", 1 },
                    { "Quality", 1 },
                    { "Extension", 1 },
                    { "StackingType", 1 },
                    { "NoteCount", 1 },
                    { "Description", 1 },
                    { "score", new BsonDocument("$meta", "vectorSearchScore") }
                })
            };

            var results = await ChordsCollection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();

            return [.. results.Select(doc => new ChordSearchResult
            {
                Id = doc.GetValue("Id", 0).AsInt32,
                Name = doc.GetValue("Name", "").AsString,
                Quality = doc.GetValue("Quality", "").AsString,
                Extension = doc.GetValue("Extension", "").AsString,
                StackingType = doc.GetValue("StackingType", "").AsString,
                NoteCount = doc.GetValue("NoteCount", 0).AsInt32,
                Description = doc.GetValue("Description", "").AsString,
                Score = doc.GetValue("score", 0.0).AsDouble
            })];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    ///     Find chords similar to a given chord
    /// </summary>
    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            // Get the chord's embedding
            var filter = Builders<BsonDocument>.Filter.Eq("Id", chordId);
            var chord = await ChordsCollection.Find(filter).FirstOrDefaultAsync();

            if (chord == null)
            {
                throw new ArgumentException($"Chord with ID {chordId} not found");
            }

            if (!chord.Contains("Embedding"))
            {
                throw new InvalidOperationException($"Chord {chordId} does not have an embedding");
            }

            var embedding = chord["Embedding"].AsBsonArray
                .Select(x => x.AsDouble)
                .ToArray();

            // Perform vector search
            var pipeline = new[]
            {
                new BsonDocument("$vectorSearch", new BsonDocument
                {
                    { "index", "chord_vector_index" },
                    { "path", "Embedding" },
                    { "queryVector", new BsonArray(embedding) },
                    { "numCandidates", numCandidates },
                    { "limit", limit + 1 } // +1 to exclude the query chord itself
                }),
                new BsonDocument("$match", new BsonDocument
                {
                    { "Id", new BsonDocument("$ne", chordId) } // Exclude the query chord
                }),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Id", 1 },
                    { "Name", 1 },
                    { "Quality", 1 },
                    { "Extension", 1 },
                    { "StackingType", 1 },
                    { "NoteCount", 1 },
                    { "Description", 1 },
                    { "score", new BsonDocument("$meta", "vectorSearchScore") }
                })
            };

            var results = await ChordsCollection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();

            return [.. results.Select(doc => new ChordSearchResult
            {
                Id = doc.GetValue("Id", 0).AsInt32,
                Name = doc.GetValue("Name", "").AsString,
                Quality = doc.GetValue("Quality", "").AsString,
                Extension = doc.GetValue("Extension", "").AsString,
                StackingType = doc.GetValue("StackingType", "").AsString,
                NoteCount = doc.GetValue("NoteCount", 0).AsInt32,
                Description = doc.GetValue("Description", "").AsString,
                Score = doc.GetValue("score", 0.0).AsDouble
            })];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar chords for chord ID: {ChordId}", chordId);
            throw;
        }
    }

    /// <summary>
    ///     Hybrid search: combine vector search with keyword filters
    /// </summary>
    public async Task<List<ChordSearchResult>> HybridSearchAsync(
        string query,
        string? quality = null,
        string? extension = null,
        string? stackingType = null,
        int? noteCount = null,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            // Generate embedding for query
            var queryEmbedding = await GenerateEmbeddingAsync(query);

            // Build match filter for keyword filters
            var matchFilters = new List<BsonDocument>();
            if (!string.IsNullOrEmpty(quality))
            {
                matchFilters.Add(new BsonDocument("Quality", quality));
            }

            if (!string.IsNullOrEmpty(extension))
            {
                matchFilters.Add(new BsonDocument("Extension", extension));
            }

            if (!string.IsNullOrEmpty(stackingType))
            {
                matchFilters.Add(new BsonDocument("StackingType", stackingType));
            }

            if (noteCount.HasValue)
            {
                matchFilters.Add(new BsonDocument("NoteCount", noteCount.Value));
            }

            // Build pipeline
            var pipelineStages = new List<BsonDocument>
            {
                new("$vectorSearch", new BsonDocument
                {
                    { "index", "chord_vector_index" },
                    { "path", "Embedding" },
                    { "queryVector", new BsonArray(queryEmbedding) },
                    { "numCandidates", numCandidates },
                    { "limit", limit * 2 } // Get more candidates for filtering
                })
            };

            // Add match stage if we have filters
            if (matchFilters.Count > 0)
            {
                pipelineStages.Add(new BsonDocument("$match", new BsonDocument("$and", new BsonArray(matchFilters))));
            }

            // Add final limit and projection
            pipelineStages.Add(new BsonDocument("$limit", limit));
            pipelineStages.Add(new BsonDocument("$project", new BsonDocument
            {
                { "Id", 1 },
                { "Name", 1 },
                { "Quality", 1 },
                { "Extension", 1 },
                { "StackingType", 1 },
                { "NoteCount", 1 },
                { "Description", 1 },
                { "score", new BsonDocument("$meta", "vectorSearchScore") }
            }));

            var results = await ChordsCollection
                .Aggregate<BsonDocument>(pipelineStages)
                .ToListAsync();

            return [.. results.Select(doc => new ChordSearchResult
            {
                Id = doc.GetValue("Id", 0).AsInt32,
                Name = doc.GetValue("Name", "").AsString,
                Quality = doc.GetValue("Quality", "").AsString,
                Extension = doc.GetValue("Extension", "").AsString,
                StackingType = doc.GetValue("StackingType", "").AsString,
                NoteCount = doc.GetValue("NoteCount", 0).AsInt32,
                Description = doc.GetValue("Description", "").AsString,
                Score = doc.GetValue("score", 0.0).AsDouble
            })];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing hybrid search for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    ///     Semantic search over chord templates (grouped by pitch class set)
    /// </summary>
    public async Task<List<ChordTemplateSearchResult>> SemanticTemplateSearchAsync(
        string query,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            var queryEmbedding = await GenerateEmbeddingAsync(query);

            var pipeline = new[]
            {
                new BsonDocument("$vectorSearch", new BsonDocument
                {
                    { "index", "chord_template_vector_index" },
                    { "path", "Embedding" },
                    { "queryVector", new BsonArray(queryEmbedding) },
                    { "numCandidates", numCandidates },
                    { "limit", limit }
                }),
                new BsonDocument("$project", new BsonDocument
                {
                    { "PitchClassSet", 1 },
                    { "Templates", 1 },
                    { "score", new BsonDocument("$meta", "vectorSearchScore") }
                })
            };

            var results = await ChordTemplatesCollection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();

            return [.. results.Select(MapTemplateResult)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic template search for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    ///     Hybrid search over chord templates: combine vector search with template attribute filters
    /// </summary>
    public async Task<List<ChordTemplateSearchResult>> HybridTemplateSearchAsync(
        string query,
        string? quality = null,
        string? extension = null,
        string? stackingType = null,
        int? noteCount = null,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            var queryEmbedding = await GenerateEmbeddingAsync(query);

            var andElemMatch = new BsonDocument();
            if (!string.IsNullOrEmpty(quality))
            {
                andElemMatch.Add("Quality", quality);
            }

            if (!string.IsNullOrEmpty(extension))
            {
                andElemMatch.Add("Extension", extension);
            }

            if (!string.IsNullOrEmpty(stackingType))
            {
                andElemMatch.Add("StackingType", stackingType);
            }

            if (noteCount.HasValue)
            {
                andElemMatch.Add("NoteCount", noteCount.Value);
            }

            var pipelineStages = new List<BsonDocument>
            {
                new("$vectorSearch", new BsonDocument
                {
                    { "index", "chord_template_vector_index" },
                    { "path", "Embedding" },
                    { "queryVector", new BsonArray(queryEmbedding) },
                    { "numCandidates", numCandidates },
                    { "limit", limit * 2 }
                })
            };

            if (andElemMatch.ElementCount > 0)
            {
                pipelineStages.Add(new BsonDocument("$match", new BsonDocument
                {
                    { "Templates", new BsonDocument("$elemMatch", andElemMatch) }
                }));
            }

            pipelineStages.Add(new BsonDocument("$limit", limit));
            pipelineStages.Add(new BsonDocument("$project", new BsonDocument
            {
                { "PitchClassSet", 1 },
                { "Templates", 1 },
                { "score", new BsonDocument("$meta", "vectorSearchScore") }
            }));

            var results = await ChordTemplatesCollection
                .Aggregate<BsonDocument>(pipelineStages)
                .ToListAsync();

            return [.. results.Select(MapTemplateResult)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing hybrid template search for query: {Query}", query);
            throw;
        }
    }

    private static ChordTemplateSearchResult MapTemplateResult(BsonDocument doc)
    {
        var pcs = new List<int>();
        if (doc.Contains("PitchClassSet") && doc["PitchClassSet"].IsBsonArray)
        {
            pcs = [.. doc["PitchClassSet"].AsBsonArray.Select(v => v.AsInt32)];
        }

        var templates = new List<TemplateInfo>();
        if (doc.Contains("Templates") && doc["Templates"].IsBsonArray)
        {
            foreach (var t in doc["Templates"].AsBsonArray)
            {
                if (t.IsBsonDocument)
                {
                    var td = t.AsBsonDocument;
                    templates.Add(new TemplateInfo
                    {
                        Name = td.GetValue("Name", "").AsString,
                        Quality = td.GetValue("Quality", "").AsString,
                        Extension = td.GetValue("Extension", "").AsString,
                        StackingType = td.GetValue("StackingType", "").AsString,
                        NoteCount = td.GetValue("NoteCount", 0).AsInt32
                    });
                }
            }
        }

        return new ChordTemplateSearchResult
        {
            PitchClassSet = pcs,
            Templates = templates,
            Score = doc.GetValue("score", 0.0).AsDouble
        };
    }
}

public class ChordSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string StackingType { get; set; } = string.Empty;
    public int NoteCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class ChordTemplateSearchResult
{
    public List<int> PitchClassSet { get; set; } = [];
    public List<TemplateInfo> Templates { get; set; } = [];
    public double Score { get; set; }
}

public class TemplateInfo
{
    public string Name { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string StackingType { get; set; } = string.Empty;
    public int NoteCount { get; set; }
}
