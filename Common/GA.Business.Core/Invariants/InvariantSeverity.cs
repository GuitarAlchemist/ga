namespace GA.Business.Core.Invariants;

/// <summary>
///     Severity levels for invariant violations
/// </summary>
public enum InvariantSeverity
{
    /// <summary>
    ///     Informational - does not prevent operation but provides useful information
    /// </summary>
    Info = 0,

    /// <summary>
    ///     Warning - indicates potential issues but allows operation to continue
    /// </summary>
    Warning = 1,

    /// <summary>
    ///     Error - indicates serious issues that should prevent operation
    /// </summary>
    Error = 2,

    /// <summary>
    ///     Critical - indicates critical issues that must be addressed immediately
    /// </summary>
    Critical = 3
}
