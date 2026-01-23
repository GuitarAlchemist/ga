namespace GA.Domain.Core.Instruments.Shapes.Applications;

/// <summary>
/// Topology information
/// </summary>
public record TopologyInfo
{
    public int ConnectedComponents { get; init; }
    public int EulerCharacteristic { get; init; }
    public int[] BettiNumbers { get; init; } = [];

    public List<PersistenceInterval> GetIntervals(int dimension)
    {
        return []; // Placeholder
    }
}