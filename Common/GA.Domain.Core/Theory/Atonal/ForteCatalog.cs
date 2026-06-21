namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Maps pitch-class sets to their <b>canonical Allen Forte 1973 number</b>
///     (e.g. the major triad is 3-11, the all-interval tetrachord is 4-Z29),
///     backed by <see cref="CanonicalForteCatalog"/>.
/// </summary>
/// <remarks>
///     For GA's internal Rahn-ordered ordinal (lexicographic by ICV, then prime-form
///     id) use <see cref="ProgrammaticForteCatalog"/> directly. That ordinal is NOT
///     Forte's catalog number — it diverges for most set classes — so it must not be
///     surfaced to users or cross-joined with external Forte-numbered data.
/// </remarks>
public static class ForteCatalog
{
    /// <summary>
    ///     Gets the total number of set classes (224 for cardinalities 0-12).
    /// </summary>
    public static int TotalSetClasses => ProgrammaticForteCatalog.Count;

    /// <summary>
    ///     Attempts to get the canonical Forte number for a given prime form.
    /// </summary>
    public static bool TryGetForteNumber(PitchClassSet primeForm, out ForteNumber forte) =>
        CanonicalForteCatalog.TryGetForteNumber(primeForm, out forte);

    /// <summary>
    ///     Gets the canonical Forte number for a given pitch class set, or null if not found.
    /// </summary>
    public static ForteNumber? GetForteNumber(PitchClassSet set) =>
        CanonicalForteCatalog.TryGetForteNumber(set.PrimeForm ?? set, out var forte) ? forte : null;

    /// <summary>
    ///     Attempts to get the prime form for a given canonical Forte number.
    /// </summary>
    public static bool TryGetPrimeForm(ForteNumber forte, out PitchClassSet? primeForm)
    {
        if (CanonicalForteCatalog.TryGetPrimeForm(forte.ToString(), out var set))
        {
            primeForm = set;
            return true;
        }

        primeForm = null;
        return false;
    }
}
