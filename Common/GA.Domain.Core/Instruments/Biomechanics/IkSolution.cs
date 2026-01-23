namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Inverse kinematics solution
/// </summary>
public sealed record IkSolution
{
    /// <summary>
    ///     The solved hand pose
    /// </summary>
    public required HandPose Pose { get; init; }

    /// <summary>
    ///     Number of iterations to converge
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    ///     Time taken to solve
    /// </summary>
    public TimeSpan SolveTime { get; init; }

    /// <summary>
    ///     Final error value
    /// </summary>
    public double FinalError { get; init; }

    /// <summary>
    ///     Whether the solution converged
    /// </summary>
    public bool Converged { get; init; }

    /// <summary>
    ///     Get convergence rate
    /// </summary>
    public double GetConvergenceRate()
    {
        return Iterations > 0 ? 1.0 / Iterations : 1.0;
    }
}