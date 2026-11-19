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
/// re-embedded). Only missing texts are embedded. Callers that arrive while a fill is in flight
/// await that same fill and share its outcome, failure included, so a wedged embedder costs
/// every waiting request one timeout rather than one timeout each in turn. A failed fill stores
/// nothing, so the next request after it retries.
/// </remarks>
public sealed class AgentEmbeddingCache
{
    private readonly ConditionalWeakTable<IEmbeddingGenerator<string, Embedding<float>>, Entry> _byGenerator = [];

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

        var entry = _byGenerator.GetValue(generator, static _ => new Entry());
        while (true)
        {
            if (TryCollect(entry.Vectors, descriptions) is { } cached) return new(cached, 0);

            Task<int> fill;
            bool started;
            lock (entry.Gate)
            {
                if (entry.Fill is { IsCompleted: false } running)
                {
                    fill = running;
                    started = false;
                }
                else
                {
                    var missing = descriptions.Distinct(StringComparer.Ordinal).Where(d => !entry.Vectors.ContainsKey(d)).ToArray();
                    // The fill is shared, so one caller's cancellation must not fail it for the others;
                    // each caller still stops waiting on its own token below.
                    fill = entry.Fill = FillAsync(generator, entry.Vectors, missing);
                    started = true;
                }
            }

            var generated = await fill.WaitAsync(cancellationToken);
            if (started) return new(TryCollect(entry.Vectors, descriptions)!, generated);
            // Joined another caller's fill: its texts may differ from ours, so look again.
        }
    }

    private static async Task<int> FillAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        ConcurrentDictionary<string, float[]> vectors,
        string[] missing)
    {
        if (missing.Length == 0) return 0;

        // Store only after the whole batch succeeded, so a failure caches nothing.
        var embeddings = await generator.GenerateAsync(missing, cancellationToken: CancellationToken.None);
        for (var i = 0; i < missing.Length; i++)
        {
            vectors[missing[i]] = embeddings[i].Vector.ToArray();
        }

        return missing.Length;
    }

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

    private sealed class Entry
    {
        public ConcurrentDictionary<string, float[]> Vectors { get; } = new(StringComparer.Ordinal);
        public Lock Gate { get; } = new();
        public Task<int>? Fill { get; set; }
    }
}

/// <summary>Agent-description vectors in request order, and how many had to be embedded.</summary>
public sealed record AgentEmbeddings(IReadOnlyList<float[]> Vectors, int GeneratedCount);
