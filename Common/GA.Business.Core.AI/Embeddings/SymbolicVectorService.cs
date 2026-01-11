namespace GA.Business.Core.AI.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Config.Configuration;

/// <summary>
/// Generates embeddings for Symbolic Knowledge (v1.2).
/// Encodes musical practice, technique, and lineage using a dynamic registry.
/// Corresponds to dimensions 66-77 of the standard musical vector.
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
