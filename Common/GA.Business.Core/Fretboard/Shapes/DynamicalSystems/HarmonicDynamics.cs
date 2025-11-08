namespace GA.Business.Core.Fretboard.Shapes.DynamicalSystems;

using Microsoft.Extensions.Logging;

/// <summary>
/// Analyzes harmonic dynamics using dynamical systems theory
/// </summary>
public class HarmonicDynamics
{
    private readonly ILogger<HarmonicDynamics> _logger;

    public HarmonicDynamics(ILogger<HarmonicDynamics> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyze the dynamical system properties of a shape graph
    /// </summary>
    public DynamicalSystemInfo Analyze(ShapeGraph graph)
    {
        _logger.LogDebug("Analyzing dynamical system for graph with {ShapeCount} shapes", graph.ShapeCount);

        var attractors = FindAttractors(graph);
        var fixedPoints = FindFixedPoints(graph);
        var limitCycles = FindLimitCycles(graph);
        var lyapunovExponent = ComputeLyapunovExponent(graph);

        return new DynamicalSystemInfo
        {
            Attractors = attractors,
            FixedPoints = fixedPoints,
            LimitCycles = limitCycles,
            LyapunovExponent = lyapunovExponent,
            IsChaotic = lyapunovExponent > 0
        };
    }

    private List<Attractor> FindAttractors(ShapeGraph graph)
    {
        var attractors = new List<Attractor>();

        // Find shapes with high in-degree (many transitions pointing to them)
        var inDegrees = new Dictionary<string, int>();
        foreach (var (_, transitions) in graph.Adjacency)
        {
            foreach (var transition in transitions)
            {
                inDegrees.TryGetValue(transition.ToId, out var count);
                inDegrees[transition.ToId] = count + 1;
            }
        }

        // Shapes with in-degree > average are attractors
        var avgInDegree = inDegrees.Any() ? inDegrees.Values.Average() : 0;
        foreach (var (shapeId, inDegree) in inDegrees.Where(kvp => kvp.Value > avgInDegree))
        {
            attractors.Add(new Attractor
            {
                ShapeId = shapeId,
                Strength = inDegree / (double)graph.ShapeCount,
                Type = "stable"
            });
        }

        return attractors;
    }

    private List<string> FindFixedPoints(ShapeGraph graph)
    {
        var fixedPoints = new List<string>();

        // Fixed points are shapes with self-loops or no outgoing transitions
        foreach (var (shapeId, transitions) in graph.Adjacency)
        {
            if (!transitions.Any() || transitions.Any(t => t.ToId == shapeId))
            {
                fixedPoints.Add(shapeId);
            }
        }

        return fixedPoints;
    }

    private List<LimitCycle> FindLimitCycles(ShapeGraph graph)
    {
        var cycles = new List<LimitCycle>();

        // Simple cycle detection using DFS
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        foreach (var shapeId in graph.Shapes.Keys)
        {
            if (!visited.Contains(shapeId))
            {
                FindCyclesFromNode(graph, shapeId, visited, stack, new List<string>(), cycles);
            }
        }

        return cycles;
    }

    private void FindCyclesFromNode(
        ShapeGraph graph,
        string currentId,
        HashSet<string> visited,
        HashSet<string> stack,
        List<string> path,
        List<LimitCycle> cycles)
    {
        visited.Add(currentId);
        stack.Add(currentId);
        path.Add(currentId);

        if (graph.Adjacency.TryGetValue(currentId, out var transitions))
        {
            foreach (var transition in transitions)
            {
                var nextId = transition.ToId;

                if (stack.Contains(nextId))
                {
                    // Found a cycle
                    var cycleStart = path.IndexOf(nextId);
                    var cycleShapes = path.Skip(cycleStart).ToList();

                    if (cycleShapes.Count >= 2 && cycleShapes.Count <= 10)
                    {
                        cycles.Add(new LimitCycle
                        {
                            ShapeIds = cycleShapes,
                            Period = cycleShapes.Count,
                            Stability = 0.8 // Placeholder
                        });
                    }
                }
                else if (!visited.Contains(nextId))
                {
                    FindCyclesFromNode(graph, nextId, visited, stack, path, cycles);
                }
            }
        }

        stack.Remove(currentId);
        path.RemoveAt(path.Count - 1);
    }

    private double ComputeLyapunovExponent(ShapeGraph graph)
    {
        if (graph.ShapeCount == 0) return 0.0;

        // Simplified Lyapunov exponent based on graph connectivity
        var avgOutDegree = graph.Adjacency.Values.Average(t => t.Count);
        var maxOutDegree = graph.Adjacency.Values.Max(t => t.Count);

        // ? > 0 indicates chaos
        return Math.Log(avgOutDegree / Math.Max(1, maxOutDegree - avgOutDegree));
    }
}

/// <summary>
/// Information about the dynamical system
/// </summary>
public record DynamicalSystemInfo
{
    public required List<Attractor> Attractors { get; init; }
    public required List<string> FixedPoints { get; init; }
    public required List<LimitCycle> LimitCycles { get; init; }
    public required double LyapunovExponent { get; init; }
    public required bool IsChaotic { get; init; }
}

/// <summary>
/// An attractor in the dynamical system
/// </summary>
public record Attractor
{
    public required string ShapeId { get; init; }
    public required double Strength { get; init; }
    public required string Type { get; init; }
}

/// <summary>
/// A limit cycle in the dynamical system
/// </summary>
public record LimitCycle
{
    public required List<string> ShapeIds { get; init; }
    public required int Period { get; init; }
    public required double Stability { get; init; }
}

