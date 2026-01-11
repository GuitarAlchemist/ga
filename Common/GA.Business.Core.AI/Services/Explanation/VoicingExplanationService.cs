namespace GA.Business.Core.AI.Services.Explanation;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Config.Configuration;
using GA.Business.Core.AI.Embeddings;

/// <summary>
/// Provides human-readable explanations of voicing embeddings by reverse-mapping symbolic bits.
/// </summary>
public class VoicingExplanationService
{
    private readonly SymbolicTagRegistry _registry;

    public VoicingExplanationService()
    {
        _registry = SymbolicTagRegistry.Instance;
    }

    /// <summary>
    /// Explains the symbolic subspace of an embedding.
    /// </summary>
    public VoicingExplanation Explain(double[] fullEmbedding)
    {
        if (fullEmbedding.Length < EmbeddingSchema.SymbolicOffset + EmbeddingSchema.SymbolicDim)
        {
            return new VoicingExplanation("Invalid embedding dimension.");
        }

        var symbolic = fullEmbedding
            .Skip(EmbeddingSchema.SymbolicOffset)
            .Take(EmbeddingSchema.SymbolicDim)
            .ToArray();

        var activeTags = new List<string>();
        var knownTags = _registry.GetAllKnownTags().ToList(); // Fix multiple enumeration

        for (int i = 0; i < symbolic.Length; i++)
        {
            if (symbolic[i] >= 0.5) // Bit is active
            {
                // Find all tags that map to this bit
                var bitIndex = i; // Fix captured variable modification
                var tagsForBit = knownTags.Where(t => _registry.GetBitIndex(t) == bitIndex);
                activeTags.AddRange(tagsForBit);
            }
        }

        return GenerateSummary(activeTags);
    }

    private VoicingExplanation GenerateSummary(List<string> tags)
    {
        if (!tags.Any()) return new VoicingExplanation("No symbolic traits identified.");

        var explanation = new VoicingExplanation();
        explanation.Tags = tags.Distinct().ToList();

        // Categorize for better UX
        foreach (var tag in explanation.Tags)
        {
            var bit = _registry.GetBitIndex(tag);
            if (bit < 6) explanation.Techniques.Add(tag);
            else explanation.Styles.Add(tag);
        }

        explanation.Summary = $"This voicing incorporates {string.Join(", ", explanation.Techniques)} techniques " +
                             $"and reflects {string.Join(", ", explanation.Styles)} styles.";

        return explanation;
    }
}

public class VoicingExplanation
{
    public string Summary { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<string> Techniques { get; set; } = new();
    public List<string> Styles { get; set; } = new();

    public VoicingExplanation() { }
    public VoicingExplanation(string summary) => Summary = summary;
}
