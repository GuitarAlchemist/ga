namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Display-only label formatter for set-class notation. <see cref="SetClassNotation.Forte"/>
///     yields the canonical Allen Forte 1973 number (via <see cref="ForteCatalog"/> /
///     <see cref="CanonicalForteCatalog"/>); <see cref="SetClassNotation.Rahn"/> yields GA's
///     internal Rahn-ordered ordinal (via <see cref="ProgrammaticForteCatalog"/>).
/// </summary>
[PublicAPI]
public static class SetClassLabelFormatter
{
    /// <summary>
    ///     Computes a display label for the given <see cref="SetClass"/> in the requested notation.
    ///     Forte → canonical 1973 catalog (e.g. major triad "3-11"); Rahn → GA's Rahn ordinal.
    /// </summary>
    public static string ToLabel(SetClass setClass, SetClassNotation notation)
    {
        var n = setClass.Cardinality.Value;

        switch (notation)
        {
            case SetClassNotation.Forte:
            {
                // Canonical Allen Forte 1973 number, e.g. the major triad is "3-11".
                if (ForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var forte))
                {
                    return forte.ToString();
                }
                return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
            }

            case SetClassNotation.Rahn:
            {
                // GA's internal Rahn ordering (lexicographic by ICV, then prime-form id).
                if (ProgrammaticForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var rahn))
                {
                    return rahn.ToString();
                }
                return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
            }

            default:
                return $"{n}-{setClass.IntervalClassVector.Id.Value % 100}";
        }
    }

    /// <summary>
    ///     Returns both the canonical Forte label and the Rahn-ordinal label (display-only).
    ///     These now differ for most set classes (only the Forte label is the standard catalog number).
    /// </summary>
    public static (string forte, string rahn) ToDualLabel(SetClass setClass)
        => (ToLabel(setClass, SetClassNotation.Forte), ToLabel(setClass, SetClassNotation.Rahn));
}
