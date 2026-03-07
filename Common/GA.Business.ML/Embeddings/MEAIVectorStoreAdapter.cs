namespace GA.Business.ML.Embeddings;

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

/// <summary>
/// Adapts IVectorIndex to Microsoft.Extensions.VectorData.VectorStore patterns
/// </summary>
public class VectorStoreAdapter(IVectorIndex index) : VectorStore
{

    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(name == "voicings");

    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException("Deletion not supported in adapter.");

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TRecord) == typeof(ChordVoicingRagDocument) && typeof(TKey) == typeof(string))
        {
            return (VectorStoreCollection<TKey, TRecord>)(object)new VectorStoreCollectionAdapter(index, name);
        }
        throw new NotSupportedException($"Unsupported collection type mapping: {typeof(TKey).Name}, {typeof(TRecord).Name}.");
    }

#pragma warning disable CS8609  // Nullability of reference types in return type doesn't match overridden member.
#pragma warning disable CS1066  // Default value on optional parameter differs from base method.
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, VectorStoreCollectionDefinition? definition = null) => throw new NotSupportedException("Dynamic collections not supported.");
#pragma warning restore CS1066
#pragma warning restore CS8609

    public override object? GetService(Type serviceType, object? serviceKey = null) => null;

    public override async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "voicings";
        await Task.CompletedTask;
    }
}

public class VectorStoreCollectionAdapter(IVectorIndex index, string name) : VectorStoreCollection<string, ChordVoicingRagDocument>
{
    private readonly IVectorIndex _index = index;

    public override string Name => name;

    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public override Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Deletion not supported via MEAI adapter currently.");

    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Deletion not supported via MEAI adapter currently.");

    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

#pragma warning disable CS8609
#pragma warning disable CS8669
    public override Task<ChordVoicingRagDocument?> GetAsync(string key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(_index.FindByIdentity(key));

    public override IAsyncEnumerable<ChordVoicingRagDocument> GetAsync(Expression<Func<ChordVoicingRagDocument, bool>> filter, int count, FilteredRecordRetrievalOptions<ChordVoicingRagDocument>? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Filtered iteration not supported.");

    public override object? GetService(Type serviceType, object? serviceKey = null) => null;

    public override async IAsyncEnumerable<VectorSearchResult<ChordVoicingRagDocument>> SearchAsync<TInput>(TInput vector, int topK, VectorSearchOptions<ChordVoicingRagDocument>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (vector is not ReadOnlyMemory<float> fp32Vector)
            throw new NotSupportedException("Only ReadOnlyMemory<float> input vectors are supported.");
            
        float[] queryArray = fp32Vector.ToArray();
        var results = _index.Search(queryArray, topK);
        
        foreach (var r in results)
        {
            yield return new VectorSearchResult<ChordVoicingRagDocument>(r.Doc, r.Score);
        }
        
        await Task.CompletedTask;
    }
#pragma warning restore CS8669
#pragma warning restore CS8609

    public override Task UpsertAsync(ChordVoicingRagDocument record, CancellationToken cancellationToken = default) => _index.IndexAsync(new[] { record });

    public override Task UpsertAsync(IEnumerable<ChordVoicingRagDocument> records, CancellationToken cancellationToken = default) => _index.IndexAsync(records);
}
