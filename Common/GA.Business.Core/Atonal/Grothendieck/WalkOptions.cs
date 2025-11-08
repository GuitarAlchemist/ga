namespace GA.Business.Core.Atonal.Grothendieck;

/// <summary>
/// Options for Markov walk generation
/// </summary>
public record WalkOptions
{
    /// <summary>
    /// Number of steps to take (default: 10)
    /// </summary>
    public int Steps { get; init; } = 10;

    /// <summary>
    /// Temperature for probabilistic selection (default: 1.0)
    /// 1.0 = balanced, &lt; 1.0 = greedy, &gt; 1.0 = exploratory
    /// </summary>
    public double Temperature { get; init; } = 1.0;

    /// <summary>
    /// Prefer box shapes (diagness &lt; 0.5) (default: false)
    /// </summary>
    public bool BoxPreference { get; init; } = false;

    /// <summary>
    /// Maximum fret span (default: 5)
    /// </summary>
    public int MaxSpan { get; init; } = 5;

    /// <summary>
    /// Maximum position shift cost (default: 5.0)
    /// </summary>
    public double MaxShift { get; init; } = 5.0;
}

