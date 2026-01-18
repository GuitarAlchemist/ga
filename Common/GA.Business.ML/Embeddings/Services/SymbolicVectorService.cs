namespace GA.Business.ML.Embeddings.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Config.Configuration;

/// <summary>
/// Generates the SYMBOLIC partition of the musical embedding (Practice, technique, lineage).
/// Corresponds to dimensions 66-77 of the standard musical vector.
/// Implements OPTIC-K Schema v1.3.1 (Indices 66-77 unchanged since v1.2).
/// </summary>
public class SymbolicVectorService
{
    public const int Dimension = 12;

    public double[] ComputeEmbedding(IEnumerable<string> tags)
    {
        var v = new double[Dimension];

        if (tags == null) return v;

        var registry = SymbolicTagRegistry.Instance;
        foreach (var tag in tags)
        {
            var bitIndex = registry.GetBitIndex(tag);
            if (bitIndex.HasValue && bitIndex.Value >= 0 && bitIndex.Value < Dimension)
            {
                v[bitIndex.Value] = 1.0;
            }
        }

        return v;
    }
}
