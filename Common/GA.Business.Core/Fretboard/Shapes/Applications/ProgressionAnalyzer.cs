namespace GA.Business.Core.Fretboard.Shapes.Applications;

using Microsoft.Extensions.Logging;

/// <summary>
/// Analyzes chord progressions using information theory
/// </summary>
public class ProgressionAnalyzer
{
    private readonly ILogger<ProgressionAnalyzer> _logger;

    public ProgressionAnalyzer(ILogger<ProgressionAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyze a progression using information theory
    /// </summary>
    public ProgressionInfo AnalyzeProgression(ShapeGraph graph, List<FretboardShape> progression)
    {
        _logger.LogDebug("Analyzing progression with {Count} shapes", progression.Count);

        var entropy = ComputeEntropy(progression);
        var perplexity = Math.Pow(2, entropy);
        var complexity = ComputeComplexity(graph, progression);
        var predictability = 1.0 - (entropy / Math.Log2(graph.ShapeCount));

        return new ProgressionInfo
        {
            Entropy = entropy,
            Perplexity = perplexity,
            Complexity = complexity,
            Predictability = predictability
        };
    }

    /// <summary>
    /// Suggest next shapes that maximize information gain
    /// </summary>
    public List<FretboardShape> SuggestNextShapes(
        ShapeGraph graph,
        List<FretboardShape> progression,
        int topK)
    {
        if (!progression.Any()) return new List<FretboardShape>();

        var currentShape = progression.Last();
        if (!graph.Adjacency.TryGetValue(currentShape.Id, out var transitions))
        {
            return new List<FretboardShape>();
        }

        // Rank by information gain (prefer less common transitions)
        var suggestions = transitions
            .Select(t => graph.Shapes[t.ToId])
            .OrderBy(s => progression.Count(p => p.Id == s.Id)) // Prefer novel shapes
            .ThenByDescending(s => s.Ergonomics)
            .Take(topK)
            .ToList();

        return suggestions;
    }

    private double ComputeEntropy(List<FretboardShape> progression)
    {
        if (!progression.Any()) return 0.0;

        // Compute entropy based on shape frequency
        var frequencies = progression
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.Count() / (double)progression.Count);

        var entropy = 0.0;
        foreach (var (_, prob) in frequencies)
        {
            if (prob > 0)
            {
                entropy -= prob * Math.Log2(prob);
            }
        }

        return entropy;
    }

    private double ComputeComplexity(ShapeGraph graph, List<FretboardShape> progression)
    {
        if (progression.Count < 2) return 0.0;

        // Complexity based on transition costs
        var Score = 0.0;
        for (var i = 0; i < progression.Count - 1; i++)
        {
            var fromId = progression[i].Id;
            var toId = progression[i + 1].Id;

            if (graph.Adjacency.TryGetValue(fromId, out var transitions))
            {
                var transition = transitions.FirstOrDefault(t => t.ToId == toId);
                if (transition != null)
                {
                    Score += transition.Score;
                }
            }
        }

        return Score / (progression.Count - 1);
    }
}

/// <summary>
/// Information about a progression
/// </summary>
public record ProgressionInfo
{
    public required double Entropy { get; init; }
    public required double Perplexity { get; init; }
    public required double Complexity { get; init; }
    public required double Predictability { get; init; }
}

