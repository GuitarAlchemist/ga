namespace GA.Business.Core.Atonal;

using JetBrains.Annotations;

/// <summary>
///     Display-only label formatter for set-class notation (Forte/Rahn).
///     Uses the programmatic ForteCatalog for complete coverage.
/// </summary>
[PublicAPI]
public static class SetClassLabelFormatter
{
    /// <summary>
    ///     Computes a display label for the given <see cref="SetClass"/> in the requested notation.
    ///     Both Forte and Rahn use the programmatic catalog (Rahn ordering).
    /// </summary>
    public static string ToLabel(SetClass setClass, SetClassNotation notation)
    {
        var n = setClass.Cardinality.Value;

        switch (notation)
        {
            case SetClassNotation.Forte:
            case SetClassNotation.Rahn:
            {
                // Use programmatic catalog for both Forte and Rahn
                // (Note: This uses Rahn ordering for both, which is mathematically consistent)
                if (ForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var forte))
                {
                    return forte.ToString();
                }
                // Fallback: use ICV-based heuristic
                return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
            }

            default:
                return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
        }
    }

    /// <summary>
    ///     Returns both Forte and Rahn labels for convenience (display-only).
    ///     Note: Both return the same value since we use a unified ordering.
    /// </summary>
    public static (string forte, string rahn) ToDualLabel(SetClass setClass)
    {
        var label = ToLabel(setClass, SetClassNotation.Forte);
        return (label, label);
    }
}
