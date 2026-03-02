namespace GA.Business.ML.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;

public class QdrantVectorIndex : IVectorIndex
{
    private readonly QdrantClient _client;
    private const string CollectionName = "voicings";
    private readonly ulong _dimension;

    public QdrantVectorIndex(string host = "localhost", int port = 6334, ulong dimension = 228)
    {
        _client = new QdrantClient(host, port, https: false);
        _dimension = dimension;
        InitializeCollectionAsync().Wait();
    }
    
    // IVectorIndex is currently synchronous or "List-based" in its interface definition.
    // Ideally IVectorIndex should be async. For now, we'll implement sync wrappers or properties.
    // NOTE: The current interface exposes IReadOnlyList<VoicingDocument> Documents.
    // This is problematic for a real DB. We might need to refactor IVectorIndex 
    // or just load everything into memory if we want to strictly adhere to the interface,
    // OR we change the interface.
    
    // Given the task is a SPIKE, let's look at the Interface again.
    // interface IVectorIndex { IReadOnlyList<VoicingDocument> Documents { get; } ... }
    
    // If we want to use Qdrant "for real", we can't load 10M docs into memory.
    // But for a "Small Corpus" validation (1k docs), we CAN load them.
    
    private List<ChordVoicingRagDocument> _localCache = [];
    
    public IReadOnlyList<ChordVoicingRagDocument> Documents => _localCache;

    public async Task InitializeCollectionAsync()
    {
        var collections = await _client.ListCollectionsAsync();
        if (!collections.Contains(CollectionName))
        {
            await _client.CreateCollectionAsync(CollectionName, new VectorParams { Size = _dimension, Distance = Distance.Cosine });
        }
        else
        {
            await LoadCacheFromQdrantAsync();
        }
    }

    private async Task LoadCacheFromQdrantAsync()
    {
        try
        {
            PointId? nextOffset = null;
            var allDocs = new List<ChordVoicingRagDocument>();
            do
            {
                var result = await _client.ScrollAsync(CollectionName, limit: 100, offset: nextOffset);
                
                foreach (var point in result.Result)
                {
                    var doc = MapToDocument(point);
                    if (doc != null) allDocs.Add(doc);
                }
                nextOffset = result.NextPageOffset;
            } while (nextOffset != null);
            
            _localCache = allDocs;
            Console.WriteLine($"Qdrant: Loaded {_localCache.Count} documents from cache.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Qdrant cache: {ex.Message}");
            // Don't crash
        }
    }

    private ChordVoicingRagDocument? MapToDocument(RetrievedPoint point)
    {
         if (point.Payload.TryGetValue("json", out var jsonVal))
         {
             var json = jsonVal.StringValue;
             if (!string.IsNullOrEmpty(json))
             {
                 try 
                 {
                    var doc = JsonSerializer.Deserialize<ChordVoicingRagDocument>(json);
                    if (doc != null)
                    {
                        if ((doc.Embedding == null || doc.Embedding.Length == 0) && point.Vectors != null)
                        {
#pragma warning disable CS0612 // Type or member is obsolete
                            var vecData = point.Vectors.Vector?.Data;
#pragma warning restore CS0612 // Type or member is obsolete
                            if (vecData != null)
                            {
                                doc = doc with { Embedding = [.. vecData] };
                            }
                        }
                        return doc;
                    }
                 }
                 catch {}
             }
         }
         
         // Fallback with required fields
         return new ChordVoicingRagDocument 
         { 
             Id = point.Id.ToString(),
             ChordName = "Unknown",
             Diagram = "x-x-x-x-x-x",
             SearchableText = "",
             PossibleKeys = [],
             SemanticTags = [],
             YamlAnalysis = "",
             MidiNotes = [],
             PitchClasses = [],
             PitchClassSet = "",
             IntervalClassVector = "",
             AnalysisEngine = "Qdrant",
             AnalysisVersion = "0.0",
             Jobs = [],
             TuningId = "Standard",
             PitchClassSetId = "",
         }; 
    }

    public async Task IndexAsync(IEnumerable<ChordVoicingRagDocument> docs)
    {
        // 1. Convert docs to Points
        var points = docs.Select(d => {
            var id = Guid.TryParse(d.Id, out var g) ? g : Guid.NewGuid();
            var point = new PointStruct
            {
                Id = id,
#pragma warning disable CS0612 // Type or member is obsolete
                Vectors = new Vectors { Vector = new Vector { Data = { d.Embedding! } } },
#pragma warning restore CS0612 // Type or member is obsolete
                Payload = {
                    ["json"] = JsonSerializer.Serialize(d)
                }
            };
            return point;
        }).ToList();

        if (!points.Any()) return;

        // 2. Upsert
        await _client.UpsertAsync(CollectionName, points);
        
        // 3. Update local cache for "Small Corpus" compliance
        _localCache.AddRange(docs);
    }

    public IEnumerable<(ChordVoicingRagDocument Doc, double Score)> Search(float[] queryVector, int topK = 10)
    {
        // Sync wrapper for Qdrant Search
        var searchTask = _client.SearchAsync(CollectionName, queryVector, limit: (ulong)topK);
        var searchResult = searchTask.Result;

        var results = new List<(ChordVoicingRagDocument, double)>();
        foreach (var scoredPoint in searchResult)
        {
            if (scoredPoint.Payload.TryGetValue("json", out var jsonValue))
            {
                 var doc = JsonSerializer.Deserialize<ChordVoicingRagDocument>(jsonValue.StringValue);
                 if (doc != null) results.Add((doc, scoredPoint.Score));
            }
        }
        return results;
    }

    public ChordVoicingRagDocument? FindByIdentity(string identity) =>
        // Naive local cache search for now, as Qdrant doesn't support "Find by Payload Value" efficiently without a Filter payload index.
        _localCache.FirstOrDefault(d => d.ChordName == identity || d.Id == identity);

    public Task<bool> IsStaleAsync(string currentSchemaVersion)
    {
        // If local cache is empty, we don't know yet, so not stale (or we should load)
        if (_localCache.Count == 0) return Task.FromResult(false);

        // Sampling check (first document) to avoid full collection scan
        var first = _localCache.First();
        return Task.FromResult(first.SchemaVersion != currentSchemaVersion || 
                               first.Embedding == null || 
                               first.Embedding.Length != (int)_dimension);
    }
}
