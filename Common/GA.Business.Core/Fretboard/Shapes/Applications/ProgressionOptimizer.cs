namespace GA.Business.Core.Fretboard.Shapes.Applications;

using Microsoft.Extensions.Logging;

/// <summary>
/// Optimizes chord progressions for practice
/// </summary>
public class ProgressionOptimizer
{
    private readonly ILogger<ProgressionOptimizer> _logger;

    public ProgressionOptimizer(ILogger<ProgressionOptimizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate an optimal practice progression
    /// </summary>
    public OptimizedProgression GeneratePracticeProgression(
        ShapeGraph graph,
        ProgressionConstraints constraints)
    {
        _logger.LogDebug("Generating practice progression with length {Length}", constraints.TargetLength);

        // Start from a random high-ergonomics shape
        var startShape = graph.Shapes.Values
            .OrderByDescending(s => s.Ergonomics)
            .FirstOrDefault();

        if (startShape == null)
        {
            return new OptimizedProgression
            {
                Shapes = new List<FretboardShape>(),
                Score = 0.0,
                Quality = 0.0
            };
        }

        var progression = new List<FretboardShape> { startShape };
        var currentShape = startShape;
        var Score = 0.0;

        for (var i = 1; i < constraints.TargetLength; i++)
        {
            var nextShape = SelectNextShape(graph, currentShape, progression, constraints);
            if (nextShape == null) break;

            // Find transition cost
            if (graph.Adjacency.TryGetValue(currentShape.Id, out var transitions))
            {
                var transition = transitions.FirstOrDefault(t => t.ToId == nextShape.Id);
                if (transition != null)
                {
                    Score += transition.Score;
                }
            }

            progression.Add(nextShape);
            currentShape = nextShape;
        }

        var quality = ComputeQuality(progression, Score);

        return new OptimizedProgression
        {
            Shapes = progression,
            Score = Score,
            Quality = quality
        };
    }

    private FretboardShape? SelectNextShape(
        ShapeGraph graph,
        FretboardShape currentShape,
        List<FretboardShape> progression,
        ProgressionConstraints constraints)
    {
        if (!graph.Adjacency.TryGetValue(currentShape.Id, out var transitions))
        {
            return null;
        }

        // Filter and rank transitions based on strategy
        var candidates = transitions
            .Select(t => (transition: t, shape: graph.Shapes[t.ToId]))
            .Where(c => c.shape.Ergonomics >= constraints.MinErgonomics)
            .ToList();

        if (!candidates.Any()) return null;

        return constraints.Strategy switch
        {
            OptimizationStrategy.MinimizeVoiceLeading => candidates
                .OrderBy(c => c.transition.PhysicalCost)
                .First().shape,

            OptimizationStrategy.MaximizeVariety => candidates
                .OrderBy(c => progression.Count(p => p.Id == c.shape.Id))
                .ThenByDescending(c => c.shape.Ergonomics)
                .First().shape,

            OptimizationStrategy.BalancedPractice => candidates
                .OrderBy(c => c.transition.Score)
                .ThenByDescending(c => c.shape.Ergonomics)
                .First().shape,

            _ => candidates.First().shape
        };
    }

    private double ComputeQuality(List<FretboardShape> progression, double Score)
    {
        if (!progression.Any()) return 0.0;

        var avgErgonomics = progression.Average(s => s.Ergonomics);
        var variety = progression.Select(s => s.Id).Distinct().Count() / (double)progression.Count;
        var costPenalty = Math.Min(1.0, Score / (progression.Count * 10.0));

        return (avgErgonomics * 0.4 + variety * 0.4 + (1.0 - costPenalty) * 0.2);
    }
}

/// <summary>
/// Constraints for progression optimization
/// </summary>
public record ProgressionConstraints
{
    public int TargetLength { get; init; } = 8;
    public double MinErgonomics { get; init; } = 0.3;
    public OptimizationStrategy Strategy { get; init; } = OptimizationStrategy.BalancedPractice;
}

/// <summary>
/// Optimization strategy
/// </summary>
public enum OptimizationStrategy
{
    MinimizeVoiceLeading,
    MaximizeVariety,
    BalancedPractice
}

/// <summary>
/// Result of progression optimization
/// </summary>
public record OptimizedProgression
{
    public required List<FretboardShape> Shapes { get; init; }
    public required double Score { get; init; }
    public required double Quality { get; init; }
}

