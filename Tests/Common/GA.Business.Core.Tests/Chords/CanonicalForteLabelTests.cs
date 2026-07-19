namespace GA.Business.Core.Tests.Chords;

using System.Linq;
using Domain.Core.Primitives.Notes;
using Domain.Core.Theory.Atonal;
using Domain.Services.Chords;

/// <summary>
///     Regression tests for #544: the set-class fallback label must use Allen Forte's
///     canonical 1973 numbering (<see cref="CanonicalForteCatalog"/>), NOT the internal
///     Rahn-ordered <see cref="ProgrammaticForteCatalog"/>. The two disagree for some
///     set classes — {0,1,6,7} is canonical Forte 4-9 but Rahn 4-21 — and the label is
///     human-facing (it lands in OPTIC-K's <c>quality_inferred</c> and chatbot output).
/// </summary>
[TestFixture]
public class CanonicalForteLabelTests
{
    private static PitchClassSet Pcs(params int[] pcs) => new(pcs.Select(PitchClass.FromValue));

    private static string Label(PitchClassSet s)
    {
        Assert.That(CanonicalForteCatalog.TryGetForteLabel(s, out var l), Is.True, $"no canonical label for {s}");
        return l!;
    }

    [Test]
    public void TryGetForteLabel_UsesCanonical1973Numbering()
    {
        Assert.Multiple(() =>
        {
            // The #544 bug: {0,1,6,7} (and its transposition {4,5,10,11}) is Forte 4-9,
            // was mislabelled 4-21 by the Rahn catalog.
            Assert.That(Label(Pcs(0, 1, 6, 7)), Is.EqualTo("4-9"), "{0,1,6,7} is Forte 4-9");
            Assert.That(Label(Pcs(4, 5, 10, 11)), Is.EqualTo("4-9"), "#544 fixture (a transposition of 0167)");
            // 4-21 is genuinely the whole-tone tetrachord — guard against a mere label swap.
            Assert.That(Label(Pcs(0, 2, 4, 6)), Is.EqualTo("4-21"), "whole-tone tetrachord is 4-21");
            // Z marker preserved (ProgrammaticForteCatalog does not model it).
            Assert.That(Label(Pcs(0, 1, 3, 7)), Is.EqualTo("4-Z29"), "Z marker preserved");
        });
    }

    [Test]
    public void Identify_SetClassFallback_EmitsCanonicalForteLabel()
    {
        var result = CanonicalChordRecognizer.Identify(Pcs(4, 5, 10, 11));
        Assert.Multiple(() =>
        {
            Assert.That(result.CanonicalName, Does.Contain("Forte 4-9"), "fallback must emit the canonical label");
            Assert.That(result.CanonicalName, Does.Not.Contain("4-21"), "must not emit the Rahn ordinal");
        });
    }

    [Test]
    public void CanonicalLabels_PrimeFormRoundTrip()
    {
        // Every stored core label (cardinalities 0-6) resolves to a prime form whose own
        // label round-trips to the same string: pc-set → prime form → Forte label → itself.
        Assert.Multiple(() =>
        {
            foreach (var (label, primeForm) in CanonicalForteCatalog.CoreByLabel)
            {
                Assert.That(CanonicalForteCatalog.TryGetForteLabel(primeForm, out var back), Is.True,
                    $"{label}: prime form has no canonical label");
                Assert.That(back, Is.EqualTo(label), $"round-trip mismatch for {label}");
            }
        });
    }
}
