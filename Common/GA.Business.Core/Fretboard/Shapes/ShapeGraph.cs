namespace GA.Business.Core.Fretboard.Shapes;

using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// Directed graph of fretboard shapes with weighted transitions
/// </summary>
public record ShapeGraph
{
    /// <summary>
    /// Tuning ID this graph was built for
    /// </summary>
    public required string TuningId { get; init; }

    /// <summary>
    /// All shapes in the graph, indexed by shape ID
    /// </summary>
    public required ImmutableDictionary<string, FretboardShape> Shapes { get; init; }

    /// <summary>
    /// Adjacency list: shape ID -> list of transitions
    /// </summary>
    public required ImmutableDictionary<string, ImmutableList<ShapeTransition>> Adjacency { get; init; }

    /// <summary>
    /// Total number of shapes in the graph
    /// </summary>
    public int ShapeCount => Shapes.Count;

    /// <summary>
    /// Total number of transitions in the graph
    /// </summary>
    public int TransitionCount => Adjacency.Values.Sum(list => list.Count);
}

