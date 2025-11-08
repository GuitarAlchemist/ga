namespace GA.Business.Core.Atonal.Grothendieck;

using Fretboard.Shapes;
using Microsoft.Extensions.Logging;

/// <summary>
/// Markov walker for navigating shape graphs
/// </summary>
public class MarkovWalker
{
    private readonly ILogger<MarkovWalker> _logger;
    private readonly Random _random = new();

    public MarkovWalker(ILogger<MarkovWalker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate a random walk through the shape graph
    /// </summary>
    public List<FretboardShape> GenerateWalk(
        ShapeGraph graph,
        FretboardShape startShape,
        WalkOptions options)
    {
        _logger.LogDebug("Generating walk from {ShapeId} with {Steps} steps", startShape.Id, options.Steps);

        var path = new List<FretboardShape> { startShape };
        var currentShape = startShape;

        for (var i = 0; i < options.Steps; i++)
        {
            var nextShape = SelectNextShape(graph, currentShape, options);
            if (nextShape == null) break;

            path.Add(nextShape);
            currentShape = nextShape;
        }

        _logger.LogDebug("Generated path with {Count} shapes", path.Count);
        return path;
    }

    /// <summary>
    /// Generate a practice path with specific constraints
    /// </summary>
    public List<FretboardShape> GeneratePracticePath(
        ShapeGraph graph,
        FretboardShape startShape,
        WalkOptions options)
    {
        return GenerateWalk(graph, startShape, options);
    }

    /// <summary>
    /// Generate a heat map showing transition probabilities
    /// </summary>
    public Dictionary<string, double> GenerateHeatMap(
        ShapeGraph graph,
        FretboardShape currentShape,
        WalkOptions options)
    {
        var heatMap = new Dictionary<string, double>();

        if (!graph.Adjacency.TryGetValue(currentShape.Id, out var transitions))
        {
            return heatMap;
        }

        var probabilities = ComputeTransitionProbabilities(graph, transitions, options);

        foreach (var (transition, probability) in probabilities)
        {
            heatMap[transition.ToId] = probability;
        }

        return heatMap;
    }

    private FretboardShape? SelectNextShape(
        ShapeGraph graph,
        FretboardShape currentShape,
        WalkOptions options)
    {
        if (!graph.Adjacency.TryGetValue(currentShape.Id, out var transitions))
        {
            return null;
        }

        if (!transitions.Any())
        {
            return null;
        }

        // Filter transitions based on options
        var validTransitions = transitions
            .Where(t => graph.Shapes.TryGetValue(t.ToId, out var shape) &&
                       shape.Span <= options.MaxSpan &&
                       t.PhysicalCost <= options.MaxShift)
            .ToList();

        if (!validTransitions.Any())
        {
            return null;
        }

        // Apply box preference
        if (options.BoxPreference)
        {
            var boxTransitions = validTransitions
                .Where(t => graph.Shapes.TryGetValue(t.ToId, out var shape) && shape.Diagness < 0.5)
                .ToList();

            if (boxTransitions.Any())
            {
                validTransitions = boxTransitions;
            }
        }

        // Compute probabilities using temperature
        var probabilities = ComputeTransitionProbabilities(graph, validTransitions, options);

        // Select using weighted random
        var totalProb = probabilities.Sum(p => p.probability);
        var randomValue = _random.NextDouble() * totalProb;

        var cumulative = 0.0;
        foreach (var (transition, probability) in probabilities)
        {
            cumulative += probability;
            if (randomValue <= cumulative)
            {
                return graph.Shapes[transition.ToId];
            }
        }

        // Fallback: return first transition
        return graph.Shapes[validTransitions.First().ToId];
    }

    private List<(ShapeTransition transition, double probability)> ComputeTransitionProbabilities(
        ShapeGraph graph,
        IEnumerable<ShapeTransition> transitions,
        WalkOptions options)
    {
        var transitionsList = transitions.ToList();
        if (!transitionsList.Any())
        {
            return new List<(ShapeTransition, double)>();
        }

        // Compute weights using Boltzmann distribution
        var weights = transitionsList
            .Select(t => Math.Exp(-t.Score / options.Temperature))
            .ToList();

        var totalWeight = weights.Sum();

        return transitionsList
            .Zip(weights, (t, w) => (t, w / totalWeight))
            .ToList();
    }
}

