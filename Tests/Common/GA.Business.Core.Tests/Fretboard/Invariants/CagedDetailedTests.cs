namespace GA.Business.Core.Tests.Fretboard.Invariants;

using System.Diagnostics;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Invariants;

[TestFixture]
public class CagedDetailedTests
{
    [Test]
    public void IdentifyCagedShape_Major_Open_Shapes()
    {
        // Arrange
        var openC = new[] { -1, 3, 2, 0, 1, 0 };
        var openA = new[] { 0, 0, 2, 2, 2, 0 };
        var openG = new[] { 3, 2, 0, 0, 0, 3 };
        var openE = new[] { 0, 2, 2, 1, 0, 0 };
        var openD = new[] { -1, -1, 0, 2, 3, 2 };

        // Act
        var c = CagedSystemIntegration.IdentifyCagedShape(openC);
        var a = CagedSystemIntegration.IdentifyCagedShape(openA);
        var g = CagedSystemIntegration.IdentifyCagedShape(openG);
        var e = CagedSystemIntegration.IdentifyCagedShape(openE);
        var d = CagedSystemIntegration.IdentifyCagedShape(openD);

        // Assert
        Assert.That(c, Is.EqualTo(CagedSystemIntegration.CagedShape.C));
        Assert.That(a, Is.EqualTo(CagedSystemIntegration.CagedShape.A));
        Assert.That(g, Is.EqualTo(CagedSystemIntegration.CagedShape.G));
        Assert.That(e, Is.EqualTo(CagedSystemIntegration.CagedShape.E));
        Assert.That(d, Is.EqualTo(CagedSystemIntegration.CagedShape.D));
    }

    [Test]
    public void TryIdentifyDetailed_Exact_Matches_For_Key_Qualities()
    {
        // Arrange
        var e7 = new[] { 0, 2, 0, 1, 0, 0 };
        var am = new[] { 0, 0, 2, 2, 1, 0 };
        var dm = new[] { -1, -1, 0, 2, 3, 1 };

        // Act
        var ok1 = CagedSystemIntegration.TryIdentifyDetailed(e7, Tuning.Default, null, out var i1);
        var ok2 = CagedSystemIntegration.TryIdentifyDetailed(am, Tuning.Default, null, out var i2);
        var ok3 = CagedSystemIntegration.TryIdentifyDetailed(dm, Tuning.Default, null, out var i3);

        // Assert
        Assert.That(ok1, Is.True);
        Assert.That(i1.Shape, Is.EqualTo(CagedSystemIntegration.CagedShape.E));
        Assert.That(i1.Quality, Is.EqualTo(CagedSystemIntegration.CagedQuality.Dominant7));
        Assert.That(i1.Confidence, Is.GreaterThanOrEqualTo(0.8));

        Assert.That(ok2, Is.True);
        Assert.That(i2.Shape, Is.EqualTo(CagedSystemIntegration.CagedShape.A));
        Assert.That(i2.Quality, Is.EqualTo(CagedSystemIntegration.CagedQuality.Minor));
        Assert.That(i2.Confidence, Is.GreaterThanOrEqualTo(0.8));

        Assert.That(ok3, Is.True);
        Assert.That(i3.Shape, Is.EqualTo(CagedSystemIntegration.CagedShape.D));
        Assert.That(i3.Quality, Is.EqualTo(CagedSystemIntegration.CagedQuality.Minor));
        Assert.That(i3.Confidence, Is.GreaterThanOrEqualTo(0.8));
    }

    [Test]
    public void TryIdentifyDetailed_Relaxed_EdgeString_Variant()
    {
        // Arrange: mute the top E of open E major
        var eOpen = new[] { 0, 2, 2, 1, 0, 0 };
        var eHighMuted = new[] { 0, 2, 2, 1, 0, -1 };

        // Act
        var okExact = CagedSystemIntegration.TryIdentifyDetailed(eOpen, Tuning.Default, null, out var exact);
        var okRelax = CagedSystemIntegration.TryIdentifyDetailed(eHighMuted, Tuning.Default, null, out var relax);

        // Assert
        Assert.That(okExact, Is.True);
        Assert.That(okRelax, Is.True);
        Assert.That(exact.Shape, Is.EqualTo(CagedSystemIntegration.CagedShape.E));
        Assert.That(relax.Shape, Is.EqualTo(CagedSystemIntegration.CagedShape.E));
        Assert.That(relax.Confidence, Is.LessThanOrEqualTo(exact.Confidence));
        Assert.That(relax.Confidence, Is.GreaterThanOrEqualTo(0.5));
    }

    [Test]
    public void GetCagedTranspositions_ShouldYield_Major_E_Family()
    {
        // Arrange
        const int maxFret = 5;

        // Act
        var family = CagedSystemIntegration
            .GetCagedTranspositions(CagedSystemIntegration.CagedShape.E, CagedSystemIntegration.CagedQuality.Major, maxFret)
            .ToList();

        // Assert
        Assert.That(family, Is.Not.Empty);
        Assert.That(family.First().baseFret, Is.EqualTo(0));
        Assert.That(family.First().frets, Is.EqualTo(new[] { 0, 2, 2, 1, 0, 0 }));
        Assert.That(family.Last().baseFret, Is.EqualTo(maxFret));
    }

    [Test]
    public void SuggestOtherPositions_ShouldReturn_Translated_Frets()
    {
        // Arrange: G major barre (E-shape at 3rd fret)
        var inv = ChordInvariant.FromFrets([3, 5, 5, 4, 3, 3], Tuning.Default);

        // Act
        var suggestions = CagedSystemIntegration.SuggestOtherPositions(inv, radius: 2).ToList();

        // Assert
        Assert.That(suggestions.Count, Is.GreaterThan(0));
        // All suggestions should normalize back to the same pattern
        var norm = inv.PatternId.ToPattern();
        Assert.That(suggestions.All(s => PatternId.FromPattern(s).ToPattern().SequenceEqual(norm)), Is.True);
    }

    [Test]
    public void Identification_Performance_Sanity()
    {
        // Build 1000 patterns by transposing a canonical form
        var canonical = new[] { 0, 2, 2, 1, 0, 0 };
        var patterns = Enumerable.Range(0, 1000)
            .Select(k => canonical.Select(v => v < 0 ? -1 : v + (k % 12)).ToArray())
            .ToList();

        var sw = Stopwatch.StartNew();
        var ok = 0;
        foreach (var p in patterns)
        {
            if (CagedSystemIntegration.TryIdentifyDetailed(p, Tuning.Default, null, out _)) ok++;
        }
        sw.Stop();

        Assert.That(ok, Is.GreaterThan(800));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1500));
    }
}
