namespace GA.Business.Core.Tests.Atonal;

using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Proves the hand-transcribed canonical Forte 1973 catalog
///     (<see cref="CanonicalForteCatalog"/>) against GA's own atonal engine, so
///     a transcription error can't ship silently. Four independent constraints:
///     (1) every stored prime form is a real GA set class, (2) per-cardinality
///     counts match GA exactly, (3) the catalog is a bijection onto GA's set
///     classes for cardinalities 1–11, and (4) the Z marker on a label agrees
///     with GA's computed <see cref="PitchClassSet.IsZRelated"/>.
/// </summary>
[TestFixture]
public class CanonicalForteCatalogTests
{
    // GA set classes keyed by canonical (GA) prime-form id, per cardinality.
    private static Dictionary<int, HashSet<int>> GaSetClassIdsByCardinality()
    {
        var byCard = new Dictionary<int, HashSet<int>>();
        foreach (var sc in SetClass.Items)
        {
            var card = sc.PrimeForm.Cardinality.Value;
            (byCard.TryGetValue(card, out var bucket) ? bucket : byCard[card] = []).Add(sc.PrimeForm.Id.Value);
        }
        return byCard;
    }

    private static int GaPrimeId(PitchClassSet set) => (set.PrimeForm ?? set).Id.Value;

    [Test]
    public void Core_Counts_Match_GA_Engine_Per_Cardinality()
    {
        var ga = GaSetClassIdsByCardinality();
        var byCard = CanonicalForteCatalog.CoreByLabel
            .GroupBy(kv => (kv.Value.PrimeForm ?? kv.Value).Cardinality.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var card in new[] { 1, 2, 3, 4, 5, 6 })
        {
            Assert.That(byCard.GetValueOrDefault(card), Is.EqualTo(ga[card].Count),
                $"cardinality {card}: canonical catalog has {byCard.GetValueOrDefault(card)} labels but GA has {ga[card].Count} set classes");
        }
    }

    [Test]
    public void Catalog_Is_Bijection_Onto_GA_SetClasses_Cardinalities_1_To_11()
    {
        var ga = GaSetClassIdsByCardinality();

        // Build the full canonical id-set: stored cards 1–6 + derived 7–11.
        var canonical = new Dictionary<int, HashSet<int>>();
        void Add(int card, int primeId)
        {
            var ok = (canonical.TryGetValue(card, out var b) ? b : canonical[card] = []).Add(primeId);
            Assert.That(ok, Is.True, $"duplicate canonical set class in cardinality {card} (id {primeId})");
        }

        foreach (var (_, set) in CanonicalForteCatalog.CoreByLabel)
            Add((set.PrimeForm ?? set).Cardinality.Value, GaPrimeId(set));

        // Derived cardinalities 7–11 via the complement labels.
        foreach (var (label, set) in CanonicalForteCatalog.CoreByLabel)
        {
            var card = (set.PrimeForm ?? set).Cardinality.Value;
            if (card is < 1 or > 5) continue; // complement lands in 7..11
            var dash = label.IndexOf('-');
            var derivedLabel = $"{12 - card}-{label[(dash + 1)..]}";
            Assert.That(CanonicalForteCatalog.TryGetPrimeForm(derivedLabel, out var derived), Is.True,
                $"derived label {derivedLabel} failed to resolve");
            Add((derived.PrimeForm ?? derived).Cardinality.Value, GaPrimeId(derived));
        }

        foreach (var card in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 })
        {
            Assert.That(canonical.GetValueOrDefault(card) ?? [], Is.EquivalentTo(ga[card]),
                $"cardinality {card}: canonical set-class ids do not match GA's set classes exactly");
        }
    }

    [Test]
    public void ZMarker_Agrees_With_GA_IsZRelated()
    {
        foreach (var (label, set) in CanonicalForteCatalog.CoreByLabel)
        {
            var labelSaysZ = label.Contains('Z', StringComparison.OrdinalIgnoreCase);
            Assert.That(set.IsZRelated, Is.EqualTo(labelSaysZ),
                $"{label}: label Z-marker={labelSaysZ} but GA IsZRelated={set.IsZRelated}");
        }
    }

    [TestCase("4-Z29", "[0,1,3,7]")]
    [TestCase("4-Z15", "[0,1,4,6]")]
    [TestCase("3-11", "[0,3,7]")]
    [TestCase("5-Z12", "[0,1,3,5,6]")]
    public void ReverseLookup_Returns_Expected_PrimeForm(string label, string expected)
    {
        Assert.That(CanonicalForteCatalog.TryGetPrimeForm(label, out var set), Is.True);
        var prime = set.PrimeForm ?? set;
        var formatted = "[" + string.Join(",", prime.OrderBy(pc => pc.Value).Select(pc => pc.Value)) + "]";
        Assert.That(formatted, Is.EqualTo(expected));
    }

    [Test]
    public void ReverseLookup_Derives_Complement_Cardinality_8()
    {
        // 8-Z15 is the complement of 4-Z15 = [0,1,4,6]; it must resolve and be
        // an 8-note set that is itself Z-related.
        Assert.That(CanonicalForteCatalog.TryGetPrimeForm("8-Z15", out var set), Is.True);
        Assert.That((set.PrimeForm ?? set).Cardinality.Value, Is.EqualTo(8));
        Assert.That(set.IsZRelated, Is.True);
    }

    // ── Forward lookup (prime form → canonical Forte label) ─────────────────────

    [TestCase("047", "3-11")]    // major triad
    [TestCase("037", "3-11")]    // minor triad — same set class
    [TestCase("07", "2-5")]      // perfect-fifth dyad (the {0,7} → 2-5 case GA got wrong)
    [TestCase("048", "3-12")]    // augmented triad
    [TestCase("0137", "4-Z29")]  // all-interval tetrachord (Z marker preserved)
    [TestCase("0146", "4-Z15")]  // all-interval tetrachord
    [TestCase("0158", "4-20")]   // major-seventh chord set class
    public void ForwardLookup_Returns_Canonical_Forte(string pcs, string expected)
    {
        Assert.That(PitchClassSet.TryParse(pcs, null, out var set), Is.True);
        var prime = set.PrimeForm ?? set;

        Assert.That(CanonicalForteCatalog.TryGetForteLabel(prime, out var label), Is.True);
        Assert.That(label, Is.EqualTo(expected));

        // The public facade must agree (this is what every consumer calls).
        Assert.That(ForteCatalog.GetForteNumber(set)?.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void ForwardLookup_Derives_Complement_Cardinality_7()
    {
        // 7-35 (the diatonic collection) is the complement of 5-35; exercises the 7–11 path.
        Assert.That(CanonicalForteCatalog.TryGetPrimeForm("7-35", out var diatonic), Is.True);
        Assert.That(CanonicalForteCatalog.TryGetForteLabel(diatonic.PrimeForm ?? diatonic, out var label), Is.True);
        Assert.That(label, Is.EqualTo("7-35"));
    }

    [Test]
    public void Forward_Then_Reverse_RoundTrips_For_Every_GA_SetClass()
    {
        // The strong invariant: for every set class GA knows (cardinalities 1–11),
        // prime-form → canonical label → prime-form returns the same prime form.
        foreach (var sc in SetClass.Items)
        {
            var prime = sc.PrimeForm;
            var card = prime.Cardinality.Value;
            if (card is 0 or 12) continue; // trivial classes use synthetic "0-1"/"12-1" labels

            Assert.That(CanonicalForteCatalog.TryGetForteLabel(prime, out var label), Is.True,
                $"no canonical label for prime form {prime} (card {card})");
            Assert.That(CanonicalForteCatalog.TryGetPrimeForm(label, out var back), Is.True,
                $"canonical label {label} did not resolve back to a prime form");
            Assert.That((back.PrimeForm ?? back).Id, Is.EqualTo(prime.Id),
                $"round-trip mismatch: {prime} → {label} → {back}");
        }
    }
}
