namespace GA.Business.Core.Tests.Atonal;

using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Primitives;

/// <summary>
/// Tests for the ProgrammaticForteCatalog which generates Forte numbers algorithmically.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ProgrammaticForteCatalogTests
{
    [Test]
    public void Catalog_Contains_All_224_SetClasses()
    {
        // The complete Forte catalog has 224 set classes (cardinalities 0-12)
        Assert.That(ProgrammaticForteCatalog.Count, Is.EqualTo(224));
    }

    [Test]
    public void Catalog_Cardinality_Counts_Are_Correct()
    {
        // Known cardinality counts from Forte catalog
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(0), Is.EqualTo(1));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(1), Is.EqualTo(1));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(2), Is.EqualTo(6));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(3), Is.EqualTo(12));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(4), Is.EqualTo(29));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(5), Is.EqualTo(38));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(6), Is.EqualTo(50));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(7), Is.EqualTo(38));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(8), Is.EqualTo(29));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(9), Is.EqualTo(12));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(10), Is.EqualTo(6));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(11), Is.EqualTo(1));
        Assert.That(ProgrammaticForteCatalog.GetCountForCardinality(12), Is.EqualTo(1));
    }

    [Test]
    public void TryGetForteNumber_MajorScale_ReturnsValidNumber()
    {
        var majorScale = new PitchClassSet([
            PitchClass.C, 
            PitchClass.FromValue(2), // D
            PitchClass.FromValue(4), // E
            PitchClass.FromValue(5), // F
            PitchClass.FromValue(7), // G
            PitchClass.FromValue(9), // A
            PitchClass.FromValue(11) // B
        ]);

        var success = ProgrammaticForteCatalog.TryGetForteNumber(majorScale, out var forte);

        Assert.That(success, Is.True);
        Assert.That(forte.Cardinality.Value, Is.EqualTo(7));
        Assert.That(forte.Index, Is.GreaterThan(0).And.LessThanOrEqualTo(38));
    }

    [Test]
    public void TryGetForteNumber_WholeTone_ReturnsValidNumber()
    {
        var wholeTone = new PitchClassSet([
            PitchClass.C, 
            PitchClass.FromValue(2), 
            PitchClass.FromValue(4), 
            PitchClass.FromValue(6), 
            PitchClass.FromValue(8), 
            PitchClass.FromValue(10)
        ]);

        var success = ProgrammaticForteCatalog.TryGetForteNumber(wholeTone, out var forte);

        Assert.That(success, Is.True);
        Assert.That(forte.Cardinality.Value, Is.EqualTo(6));
    }

    [Test]
    public void TryGetPrimeForm_RoundTrip_Succeeds()
    {
        // Get Forte number for Major triad
        var majorTriad = new PitchClassSet([
            PitchClass.C, 
            PitchClass.FromValue(4), 
            PitchClass.FromValue(7)
        ]);

        var success1 = ProgrammaticForteCatalog.TryGetForteNumber(majorTriad, out var forte);
        Assert.That(success1, Is.True);

        // Round-trip: get prime form back
        var success2 = ProgrammaticForteCatalog.TryGetPrimeForm(forte, out var primeForm);
        Assert.That(success2, Is.True);
        Assert.That(primeForm, Is.Not.Null);
        Assert.That(primeForm!.Count, Is.EqualTo(3));
    }

    [Test]
    public void ForteByPrimeFormId_IsBidirectionalWithPrimeFormByForte()
    {
        // Verify bidirectionality: every entry in ForteByPrimeFormId should be in PrimeFormByForte
        foreach (var kvp in ProgrammaticForteCatalog.ForteByPrimeFormId)
        {
            var forte = kvp.Value;
            Assert.That(ProgrammaticForteCatalog.PrimeFormByForte.ContainsKey(forte), Is.True,
                $"Forte {forte} not found in reverse catalog");
        }
    }

    [Test]
    public void Catalog_AllForteNumbers_AreUnique()
    {
        var forteNumbers = ProgrammaticForteCatalog.ForteByPrimeFormId.Values.ToList();
        var distinct = forteNumbers.Distinct().ToList();

        Assert.That(forteNumbers.Count, Is.EqualTo(distinct.Count),
            "All Forte numbers should be unique");
    }

    [Test]
    public void Catalog_ForteNumbers_AreContiguousWithinCardinality()
    {
        // For each cardinality, indices should be 1, 2, 3, ... n
        for (var card = 0; card <= 12; card++)
        {
            var forteNumbers = ProgrammaticForteCatalog.ForteByPrimeFormId.Values
                .Where(f => f.Cardinality.Value == card)
                .OrderBy(f => f.Index)
                .ToList();

            if (forteNumbers.Count == 0) continue;

            for (var i = 0; i < forteNumbers.Count; i++)
            {
                Assert.That(forteNumbers[i].Index, Is.EqualTo(i + 1),
                    $"Cardinality {card}: expected index {i + 1}, got {forteNumbers[i].Index}");
            }
        }
    }
}
