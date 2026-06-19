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
}
