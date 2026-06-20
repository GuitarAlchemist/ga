namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Allen Forte's canonical 1973 set-class catalog (<c>The Structure of
///     Atonal Music</c>) — label → prime form. This is the STANDARD numbering a
///     user means when they type "4-Z29", and it differs from GA's internal
///     <see cref="ProgrammaticForteCatalog"/> (which uses Rahn ordering and does
///     not model the Z marker). Use this for human-facing Forte-label lookups;
///     use the programmatic catalog for GA's internal Rahn-consistent work.
/// </summary>
/// <remarks>
///     Only cardinalities 0–6 are stored. Cardinalities 7–12 are derived at
///     lookup time via Forte's complement rule: for n ≠ 6, the set class
///     <c>n-k</c> and its complement <c>(12−n)-k</c> share the same index and
///     Z-status, so e.g. "8-Z15" resolves to the complement of "4-Z15". The
///     stored prime forms are the standard Forte-column values from the
///     canonical catalog; <c>CanonicalForteCatalogTests</c> verifies every entry
///     against GA's own engine (bijection with <see cref="SetClass.Items"/>,
///     per-cardinality counts, and Z-marker ⇔ <see cref="PitchClassSet.IsZRelated"/>).
/// </remarks>
public static class CanonicalForteCatalog
{
    // Pitch classes: 0-9, T=10, E=11. One "label primeForm" pair per line,
    // cardinalities 0-6 only. Z marker is part of the label.
    private const string CoreData = """
        1-1 0
        2-1 01
        2-2 02
        2-3 03
        2-4 04
        2-5 05
        2-6 06
        3-1 012
        3-2 013
        3-3 014
        3-4 015
        3-5 016
        3-6 024
        3-7 025
        3-8 026
        3-9 027
        3-10 036
        3-11 037
        3-12 048
        4-1 0123
        4-2 0124
        4-3 0134
        4-4 0125
        4-5 0126
        4-6 0127
        4-7 0145
        4-8 0156
        4-9 0167
        4-10 0235
        4-11 0135
        4-12 0236
        4-13 0136
        4-14 0237
        4-Z15 0146
        4-16 0157
        4-17 0347
        4-18 0147
        4-19 0148
        4-20 0158
        4-21 0246
        4-22 0247
        4-23 0257
        4-24 0248
        4-25 0268
        4-26 0358
        4-27 0258
        4-28 0369
        4-Z29 0137
        5-1 01234
        5-2 01235
        5-3 01245
        5-4 01236
        5-5 01237
        5-6 01256
        5-7 01267
        5-8 02346
        5-9 01246
        5-10 01346
        5-11 02347
        5-Z12 01356
        5-13 01248
        5-14 01257
        5-15 01268
        5-16 01347
        5-Z17 01348
        5-Z18 01457
        5-19 01367
        5-20 01568
        5-21 01458
        5-22 01478
        5-23 02357
        5-24 01357
        5-25 02358
        5-26 02458
        5-27 01358
        5-28 02368
        5-29 01368
        5-30 01468
        5-31 01369
        5-32 01469
        5-33 02468
        5-34 02469
        5-35 02479
        5-Z36 01247
        5-Z37 03458
        5-Z38 01258
        6-1 012345
        6-2 012346
        6-Z3 012356
        6-Z4 012456
        6-5 012367
        6-Z6 012567
        6-7 012678
        6-8 023457
        6-9 012357
        6-Z10 013457
        6-Z11 012457
        6-Z12 012467
        6-Z13 013467
        6-14 013458
        6-15 012458
        6-16 014568
        6-Z17 012478
        6-18 012578
        6-Z19 013478
        6-20 014589
        6-21 023468
        6-22 012468
        6-Z23 023568
        6-Z24 013468
        6-Z25 013568
        6-Z26 013578
        6-27 013469
        6-Z28 013569
        6-Z29 023679
        6-30 013679
        6-31 014579
        6-32 024579
        6-33 023579
        6-34 013579
        6-35 02468T
        6-Z36 012347
        6-Z37 012348
        6-Z38 012378
        6-Z39 023458
        6-Z40 012358
        6-Z41 012368
        6-Z42 012369
        6-Z43 012568
        6-Z44 012569
        6-Z45 023469
        6-Z46 012469
        6-Z47 012479
        6-Z48 012579
        6-Z49 013479
        6-Z50 014679
        """;

    private static readonly Lazy<IReadOnlyDictionary<string, PitchClassSet>> _coreByLabel =
        new(BuildCore, LazyThreadSafetyMode.ExecutionAndPublication);

    private static IReadOnlyDictionary<string, PitchClassSet> BuildCore()
    {
        var map = new Dictionary<string, PitchClassSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in CoreData.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;
            if (PitchClassSet.TryParse(parts[1], null, out var set))
            {
                map[parts[0]] = set;
            }
        }
        return map;
    }

    /// <summary>All stored (cardinality 0–6) canonical Forte labels.</summary>
    public static IReadOnlyDictionary<string, PitchClassSet> CoreByLabel => _coreByLabel.Value;

    /// <summary>
    ///     Resolves a canonical Forte label (e.g. "4-Z29", "8-Z15", "3-11") to
    ///     its prime form. Cardinalities 7–12 are derived from the stored
    ///     complement. Returns false for malformed labels or labels not in the
    ///     canonical catalog.
    /// </summary>
    public static bool TryGetPrimeForm(string? forteLabel, out PitchClassSet primeForm)
    {
        primeForm = default!;
        if (string.IsNullOrWhiteSpace(forteLabel)) return false;

        var label = forteLabel.Trim().ToUpperInvariant();
        var dash = label.IndexOf('-');
        if (dash <= 0 || dash == label.Length - 1) return false;
        if (!int.TryParse(label[..dash], out var cardinality) || cardinality is < 0 or > 12) return false;

        // index part (may carry a leading Z), e.g. "Z29" or "11".
        var indexPart = label[(dash + 1)..];

        if (cardinality <= 6)
        {
            return CoreByLabel.TryGetValue(label, out primeForm!);
        }

        // Derive cardinality 7–12 from the complement (12−n)-k (same index + Z).
        var complementLabel = $"{12 - cardinality}-{indexPart}";
        if (!CoreByLabel.TryGetValue(complementLabel, out var complementSet)) return false;

        primeForm = complementSet.Complement.PrimeForm ?? complementSet.Complement;
        return true;
    }

    private static readonly Lazy<IReadOnlyDictionary<PitchClassSetId, string>> _labelByPrimeId =
        new(BuildReverse, LazyThreadSafetyMode.ExecutionAndPublication);

    private static IReadOnlyDictionary<PitchClassSetId, string> BuildReverse()
    {
        var map = new Dictionary<PitchClassSetId, string>();
        foreach (var (label, set) in CoreByLabel)
        {
            // Key on GA's own prime-form id so callers passing any orbit member resolve.
            map[(set.PrimeForm ?? set).Id] = label;
        }
        return map;
    }

    /// <summary>
    ///     Inverse of <see cref="TryGetPrimeForm"/>: resolves a prime form to its
    ///     canonical Forte label — e.g. {0,3,7} → "3-11", {0,1,3,7} → "4-Z29".
    ///     Cardinalities 7–11 are derived via the complement rule (same index + Z as
    ///     the complement); the empty set and chromatic aggregate map to "0-1" / "12-1".
    ///     Returns false for any input that is not a recognized set class.
    /// </summary>
    public static bool TryGetForteLabel(PitchClassSet primeForm, out string? label)
    {
        label = null;
        if (primeForm is null) return false;

        var n = primeForm.Count;
        if (n == 0) { label = "0-1"; return true; }
        if (n == 12) { label = "12-1"; return true; }

        var pf = primeForm.PrimeForm ?? primeForm;
        if (n <= 6)
        {
            return _labelByPrimeId.Value.TryGetValue(pf.Id, out label);
        }

        // Cardinalities 7–11: the set class n-k is the complement of (12−n)-k and
        // shares its index and Z-status, so look up the complement's label and swap
        // the cardinality prefix.
        var complement = pf.Complement;
        var complementPrime = complement.PrimeForm ?? complement;
        if (!_labelByPrimeId.Value.TryGetValue(complementPrime.Id, out var complementLabel))
        {
            return false;
        }

        var dash = complementLabel!.IndexOf('-');
        label = $"{n}-{complementLabel[(dash + 1)..]}";
        return true;
    }

    /// <summary>
    ///     Inverse lookup returning a <see cref="ForteNumber"/> (with its
    ///     <see cref="ForteNumber.IsZRelated"/> flag set) rather than a string.
    ///     See <see cref="TryGetForteLabel"/>.
    /// </summary>
    public static bool TryGetForteNumber(PitchClassSet primeForm, out ForteNumber forte)
    {
        forte = default;
        return TryGetForteLabel(primeForm, out var label)
               && ForteNumber.TryParse(label, null, out forte);
    }
}
