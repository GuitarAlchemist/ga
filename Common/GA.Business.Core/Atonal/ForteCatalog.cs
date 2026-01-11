namespace GA.Business.Core.Atonal;

/// <summary>
///     Forte catalog for mapping pitch-class sets to Forte numbers.
///     Uses the fully programmatic ProgrammaticForteCatalog for complete coverage.
/// </summary>
/// <remarks>
///     Uses Rahn ordering (lexicographic by ICV, then by prime form ID) which is
///     mathematically consistent and complete for all 224 set classes.
/// </remarks>
public static class ForteCatalog
{
    /// <summary>
    /// Attempts to get the Forte number for a given pitch class set.
    /// </summary>
    public static bool TryGetForteNumber(PitchClassSet primeForm, out ForteNumber forte)
    {
        return ProgrammaticForteCatalog.TryGetForteNumber(primeForm, out forte);
    }

    /// <summary>
    /// Gets the Forte number for a given pitch class set, or null if not found.
    /// </summary>
    public static ForteNumber? GetForteNumber(PitchClassSet set)
    {
        return ProgrammaticForteCatalog.GetForteNumber(set);
    }

    /// <summary>
    /// Attempts to get the prime form for a given Forte number.
    /// </summary>
    public static bool TryGetPrimeForm(ForteNumber forte, out PitchClassSet? primeForm)
    {
        return ProgrammaticForteCatalog.TryGetPrimeForm(forte, out primeForm);
    }

    /// <summary>
    /// Gets the total number of set classes (224 for cardinalities 0-12).
    /// </summary>
    public static int TotalSetClasses => ProgrammaticForteCatalog.Count;
}
