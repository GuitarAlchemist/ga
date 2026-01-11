namespace GA.Business.Core.Fretboard.Voicings.Search;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Config.Configuration;

/// <summary>
/// Scans natural language queries for known symbolic tags to enhance search intent.
/// </summary>
public class SymbolicQueryParser
{
    private readonly SymbolicTagRegistry _registry;

    public SymbolicQueryParser(SymbolicTagRegistry? registry = null)
    {
        _registry = registry ?? SymbolicTagRegistry.Instance;
    }

    /// <summary>
    /// Extracts active bit indices from a query string based on registered tags.
    /// </summary>
    public List<int> ParseQueryTags(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var activeBitIndices = new HashSet<int>();
        var normalizedQuery = query.ToLowerInvariant();
        
        // 1. Check for multi-word tags (keeping existing logic for exact phrase matches)
        var knownTags = _registry.GetAllKnownTags();
        foreach (var tag in knownTags)
        {
            if (normalizedQuery.Contains(tag.Replace("-", " ")) || normalizedQuery.Contains(tag))
            {
                var bitIndex = _registry.GetBitIndex(tag);
                if (bitIndex.HasValue) activeBitIndices.Add(bitIndex.Value);
            }
        }

        // 2. Check individual tokens against registry (for partial matches like "beginner" -> "beginner-friendly")
        var tokens = normalizedQuery.Split(new[] { ' ', ',', '.', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var bitIndex = _registry.GetBitIndex(token);
            if (bitIndex.HasValue)
            {
                activeBitIndices.Add(bitIndex.Value);
            }
        }

        return activeBitIndices.ToList();
    }
}
