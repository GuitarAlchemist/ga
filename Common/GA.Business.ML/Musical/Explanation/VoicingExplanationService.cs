namespace GA.Business.ML.Musical.Explanation;

using Domain.Services;
using Enrichment;

/// <summary>
///     Provides human-readable explanations of voicing embeddings by reverse-mapping symbolic bits.
/// </summary>
public class VoicingExplanationService(ModalFlavorService modalFlavorService)
{
    private readonly SymbolicTagRegistry _registry = SymbolicTagRegistry.Instance;

    /// <summary>
    ///     Explains the voicing using both explicit tags and embedded symbolic bits.
    /// </summary>
    public VoicingExplanation Explain(ChordVoicingRagDocument doc)
    {
        // Use HashSet to avoid duplicates and allow easy enrichment
        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Get embedding-based tags (if embedding is present)
        if (doc.Embedding != null &&
            doc.Embedding.Length >= EmbeddingSchema.SymbolicOffset + EmbeddingSchema.SymbolicDim)
        {
            var embExplanation = Explain(doc.Embedding);
            foreach (var t in embExplanation.Tags)
            {
                tagSet.Add(t);
            }
        }

        // 2. Add explicit semantic tags from document (Phases 12 and 13)
        if (doc.SemanticTags != null)
        {
            foreach (var t in doc.SemanticTags)
            {
                tagSet.Add(t);
            }
        }

        // 3. Enrich with Modal Flavor (Phase 13 Integration)
        modalFlavorService.Enrich(doc, tagSet);

        var result = GenerateSummary([.. tagSet]);

        // 4. Capture Spectral Position (if embedding present)
        if (doc.Embedding != null && doc.Embedding.Length > EmbeddingSchema.FourierPhaseK5)
        {
            // Map [0, 1] normalized phase back to radians [-PI, PI] or just provide factual number.
            // Radian mapping: (val * 2PI) - PI
            var phase = doc.Embedding[EmbeddingSchema.FourierPhaseK5];
            result.SpectralCentroid = (double)phase * 2.0 * Math.PI - Math.PI;
        }

        return result;
    }

    /// <summary>
    ///     Explains the symbolic subspace of an embedding.
    /// </summary>
    public VoicingExplanation Explain(float[] fullEmbedding)
    {
        if (fullEmbedding.Length < EmbeddingSchema.SymbolicOffset + EmbeddingSchema.SymbolicDim)
        {
            return new("Invalid embedding dimension.");
        }

        var symbolic = fullEmbedding
            .Skip(EmbeddingSchema.SymbolicOffset)
            .Take(EmbeddingSchema.SymbolicDim)
            .ToArray();

        var activeTags = new List<string>();
        var knownTags = _registry.GetAllKnownTags().ToList();

        for (var i = 0; i < symbolic.Length; i++)
        {
            if (symbolic[i] >= 0.5) // Bit is active
            {
                // Find all tags that map to this bit
                var bitIndex = i;
                var tagsForBit = knownTags.Where(t => _registry.GetBitIndex(t) == bitIndex);
                activeTags.AddRange(tagsForBit);
            }
        }

        return GenerateSummary(activeTags);
    }

    private VoicingExplanation GenerateSummary(List<string> tags)
    {
        if (!tags.Any())
        {
            return new("No symbolic traits identified.");
        }

        var explanation = new VoicingExplanation
        {
            Tags = [.. tags.Distinct(StringComparer.OrdinalIgnoreCase)]
        };

        // Categorize for better UX
        foreach (var tag in explanation.Tags)
        {
            if (tag.StartsWith("Flavor:", StringComparison.OrdinalIgnoreCase))
            {
                // Phase 13: Flavor tags
                explanation.Styles.Add($"{tag.Substring(7)} flavor");
                continue;
            }

            // Try registry lookup
            try
            {
                var bitIndex = _registry.GetBitIndex(tag);
                if (bitIndex.HasValue)
                {
                    var bit = bitIndex.Value;
                    if (bit < 6)
                    {
                        explanation.Techniques.Add(tag);
                    }
                    else
                    {
                        explanation.Styles.Add(tag);
                    }
                }
                else
                {
                    // Fallback for unknown tags (e.g. from AutoTaggingService that aren't in registry yet)
                    // If it ends with "Technique" or "Style", we could guess, otherwise put in Techniques for now
                    explanation.Styles.Add(tag);
                }
            }
            catch
            {
                explanation.Styles.Add(tag);
            }
        }

        var summary = "This voicing";
        if (explanation.Techniques.Any())
        {
            summary += $" incorporates {string.Join(", ", explanation.Techniques)} techniques";
        }

        if (explanation.Styles.Any())
        {
            if (explanation.Techniques.Any())
            {
                summary += " and";
            }

            summary += $" reflects {string.Join(", ", explanation.Styles)} characteristics";
        }

        explanation.Summary = summary + ".";
        return explanation;
    }
}

public class VoicingExplanation
{
    public VoicingExplanation()
    {
    }

    public VoicingExplanation(string summary) => Summary = summary;
    public string Summary { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public List<string> Techniques { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public double? SpectralCentroid { get; set; }
}
