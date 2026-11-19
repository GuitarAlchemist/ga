namespace GA.Business.ML.Agents;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

/// <summary>
/// Process-wide cache of agent-description embeddings for <see cref="SemanticRouter"/>.
/// </summary>
/// <remarks>
/// <see cref="SemanticRouter"/> is scoped because its agents may depend on scoped services,
/// so an instance-level cache was rebuilt on every request: one remote embedding call per
/// chat request (up to several seconds on a cold Ollama). Register this type as a singleton.
/// Entries are keyed by the generator instance (held weakly, so a different embedder never
/// reuses another's vectors) and by the exact description text (so an edited description is
/// re-embedded). Only missing texts are embedded, one fill at a time; a failed call stores
/// nothing, so the next request retries.
/// </remarks>
public sealed class AgentEmbeddingCache : IDisposable
{
    private readonly ConditionalWeakTable<IEmbeddingGenerator<string, Embedding<float>>, ConcurrentDictionary<string, float[]>> _byGenerator = [];
    private readonly SemaphoreSlim _fillLock = new(1, 1);

    /// <summary>
    /// Returns one vector per description, in order, embedding only the texts not cached yet.
    /// </summary>
    /// <returns>The vectors and how many texts this call had to embed.</returns>
    public async Task<AgentEmbeddings> GetOrCreateAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IReadOnlyList<string> descriptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(descriptions);

        var vectors = _byGenerator.GetValue(generator, static _ => new ConcurrentDictionary<string, float[]>(StringComparer.Ordinal));
        if (TryCollect(vectors, descriptions) is { } cached) return new(cached, 0);

        await _fillLock.WaitAsync(cancellationToken);
        try
        {
            var missing = descriptions.Distinct(StringComparer.Ordinal).Where(d => !vectors.ContainsKey(d)).ToArray();
            if (missing.Length > 0)
            {
                // Store only after the whole batch succeeded, so a failure caches nothing.
                var embeddings = await generator.GenerateAsync(missing, cancellationToken: cancellationToken);
                for (var i = 0; i < missing.Length; i++)
                {
                    vectors[missing[i]] = embeddings[i].Vector.ToArray();
                }
            }

            return new(TryCollect(vectors, descriptions)!, missing.Length);
        }
        finally
        {
            _fillLock.Release();
        }
    }

    public void Dispose() => _fillLock.Dispose();

    private static IReadOnlyList<float[]>? TryCollect(ConcurrentDictionary<string, float[]> vectors, IReadOnlyList<string> descriptions)
    {
        var result = new float[descriptions.Count][];
        for (var i = 0; i < descriptions.Count; i++)
        {
            if (!vectors.TryGetValue(descriptions[i], out var vector)) return null;
            result[i] = vector;
        }

        return result;
    }
}

/// <summary>Agent-description vectors in request order, and how many had to be embedded.</summary>
public sealed record AgentEmbeddings(IReadOnlyList<float[]> Vectors, int GeneratedCount);
