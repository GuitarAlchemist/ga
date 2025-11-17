namespace GA.Business.Core.Atonal;

/// <summary>
///     Minimal, display-only label formatter for set-class notation.
///     Forte remains the default; Rahn returns mapped values when available,
///     otherwise a placeholder.
/// </summary>
[PublicAPI]
public static class SetClassLabelFormatter
{
    // Data-driven mapping: loaded lazily from embedded JSON (see Atonal/Data/SetClassNotationMap.json)
    // Key is the PrimeForm string representation (e.g., "[0,4,7]").
    private static IReadOnlyDictionary<string, int> RahnIndexByPrimeForm => SetClassNotationMap.RahnIndexByPrimeForm;
    private static IReadOnlyDictionary<string, int> ForteIndexByPrimeForm => SetClassNotationMap.ForteIndexByPrimeForm;

    /// <summary>
    ///     Computes a display label for the given <see cref="SetClass"/> in the requested notation.
    ///     Forte is computed using current heuristic used in UI (cardinality + ICV-based index).
    ///     Rahn returns a mapped index when available; otherwise returns "n-?".
    /// </summary>
    public static string ToLabel(SetClass setClass, SetClassNotation notation)
    {
        var n = setClass.Cardinality.Value;

        switch (notation)
        {
            case SetClassNotation.Forte:
                // Prefer mapped Forte index when available; otherwise fallback to heuristic
                {
                    var key = setClass.PrimeForm.ToString();
                    if (ForteIndexByPrimeForm.TryGetValue(key, out var forteIndex) && forteIndex > 0)
                    {
                        return $"{n}-{forteIndex}";
                    }
                    return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
                }

            case SetClassNotation.Rahn:
            {
                var key = setClass.PrimeForm.ToString();
                if (RahnIndexByPrimeForm.TryGetValue(key, out var index) && index > 0)
                {
                    return $"{n}-{index}";
                }

                // Unknown (not yet mapped) -> placeholder
                return $"{n}-?";
            }

            default:
                return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
        }
    }

    /// <summary>
    ///     Returns both Forte and Rahn labels for convenience (display-only).
    /// </summary>
    public static (string forte, string rahn) ToDualLabel(SetClass setClass)
    {
        var forte = ToLabel(setClass, SetClassNotation.Forte);
        var rahn = ToLabel(setClass, SetClassNotation.Rahn);
        return (forte, rahn);
    }
}
