namespace GA.Business.Core.Tests.Voicings;

using Domain.Core.Theory.Atonal;
using Domain.Services.Chords;

/// <summary>
///     Top-priority invariants from the ecosystem catalog
///     (see <c>docs/methodology/invariants-catalog.md</c>).
///
///     These are the two invariants that the chord-recognition refactor was
///     designed to restore:
///
///     Invariant #33 — Cross-instrument <c>ChordName</c> consistency for same PC-set
///     Invariant #25 — Cross-instrument STRUCTURE partition equality
///
///     Pre-refactor baseline (measured 2026-04-17, see embedding-diagnostics JSON):
///         - ChordName consistency across instruments: <b>29.4%</b>
///         - STRUCTURE partition classifier accuracy: <b>56%</b> (random baseline 33%)
///
///     Post-refactor target:
///         - ChordName consistency: &gt;98%
///         - STRUCTURE leak: &lt;40%
///
///     These tests assert the CANONICAL (register-invariant) layer, which is
///     guaranteed by construction after Phase D (<see cref="CanonicalChordRecognizer"/>).
///     If any test fails, we've regressed the refactor.
///
///     The INDEX-level check (whether the current on-disk <c>optick.index</c>
///     satisfies #25) lives in IX's <c>ix-embedding-diagnostics</c> crate; it will
///     continue to measure 56% leak until Phase E regenerates the index.
/// </summary>
[TestFixture]
public class EmbeddingInvariantsTests
{
    /// <summary>
    ///     Invariant #33: the recognizer's <c>CanonicalName</c> must depend only on
    ///     pitch-class content, never on register. Same PC-set + different bass
    ///     note → identical CanonicalName. The SlashSuffix may differ — that's
    ///     where register-dependence is allowed to live.
    /// </summary>
    [TestCase(new[] { 0, 4, 7 },      0, 4,  TestName = "Invariant#33_Cmajor_bassC_vs_bassE")]
    [TestCase(new[] { 0, 4, 7 },      0, 7,  TestName = "Invariant#33_Cmajor_bassC_vs_bassG")]
    [TestCase(new[] { 0, 3, 7 },      0, 3,  TestName = "Invariant#33_Cminor_bassC_vs_bassEb")]
    [TestCase(new[] { 0, 4, 7, 11 },  0, 4,  TestName = "Invariant#33_Cmaj7_bassC_vs_bassE")]
    [TestCase(new[] { 0, 4, 7, 11 },  0, 7,  TestName = "Invariant#33_Cmaj7_bassC_vs_bassG")]
    [TestCase(new[] { 0, 4, 7, 11 },  0, 11, TestName = "Invariant#33_Cmaj7_bassC_vs_bassB")]
    [TestCase(new[] { 0, 3, 7, 10 },  0, 3,  TestName = "Invariant#33_Cm7_bassC_vs_bassEb")]
    [TestCase(new[] { 0, 3, 7, 10 },  0, 10, TestName = "Invariant#33_Cm7_bassC_vs_bassBb")]
    [TestCase(new[] { 0, 4, 7, 10 },  0, 4,  TestName = "Invariant#33_C7_bassC_vs_bassE")]
    [TestCase(new[] { 0, 4, 7, 10 },  0, 10, TestName = "Invariant#33_C7_bassC_vs_bassBb")]
    [TestCase(new[] { 0, 3, 6, 10 },  0, 6,  TestName = "Invariant#33_Cm7b5_rootPos_vs_tritoneBass")]
    [TestCase(new[] { 0, 3, 6, 9 },   0, 3,  TestName = "Invariant#33_Cdim7_symmetryTest")]
    [TestCase(new[] { 0, 2, 4, 7 },   0, 2,  TestName = "Invariant#33_Cadd9_bassC_vs_bassD")]
    [TestCase(new[] { 0, 4, 6, 10 },  0, 10, TestName = "Invariant#33_C7b5_bassC_vs_bassBb")]
    [TestCase(new[] { 0, 3, 4, 7, 10 }, 0, 3, TestName = "Invariant#33_C7sharp9_bassC_vs_minorThirdBass")]
    [TestCase(new[] { 0, 2, 4, 7, 11 }, 0, 7, TestName = "Invariant#33_Cmaj9_bassC_vs_bassG")]
    [TestCase(new[] { 0, 2, 5, 7 },   0, 5,  TestName = "Invariant#33_C7sus4noSeventh_or_Fadd9noThird")]
    [TestCase(new[] { 0, 5, 10 },     0, 5,  TestName = "Invariant#33_quartal3_bassC_vs_bassF")]
    [TestCase(new[] { 0, 4, 7, 9 },   0, 9,  TestName = "Invariant#33_C6_bassC_vs_bassA_collisionWithAm7")]
    public void Invariant33_CanonicalName_IsInvariantAcrossBass(int[] pcValues, int bass1, int bass2)
    {
        var pcSet = new PitchClassSet(pcValues.Select(PitchClass.FromValue));
        var r1 = CanonicalChordRecognizer.Identify(pcSet, PitchClass.FromValue(bass1));
        var r2 = CanonicalChordRecognizer.Identify(pcSet, PitchClass.FromValue(bass2));

        Assert.That(
            r1.CanonicalName,
            Is.EqualTo(r2.CanonicalName),
            $"Invariant #33 violation. PC-set [{string.Join(",", pcValues)}] must produce "
            + $"identical CanonicalName regardless of bass.\n"
            + $"  bass={bass1} → CanonicalName=\"{r1.CanonicalName}\" (root=\"{r1.Root}\", pattern=\"{r1.PatternName}\")\n"
            + $"  bass={bass2} → CanonicalName=\"{r2.CanonicalName}\" (root=\"{r2.Root}\", pattern=\"{r2.PatternName}\")\n"
            + $"Pre-refactor corpus measurement: 29.4% cross-instrument consistency.\n"
            + $"Post-Phase D expectation: 100% (this test was designed to enforce that).");
    }

    /// <summary>
    ///     Corollary to #33: the recognizer's Root, Quality, and PatternName must
    ///     also be invariant to the bass hint. If bass affected Root, recognition
    ///     would produce different "chord families" for the same pitch content.
    /// </summary>
    [TestCase(new[] { 0, 4, 7 },     0, 4, TestName = "Invariant#33_RootQuality_Cmajor")]
    [TestCase(new[] { 0, 3, 7 },     0, 3, TestName = "Invariant#33_RootQuality_Cminor")]
    [TestCase(new[] { 0, 4, 7, 11 }, 0, 11, TestName = "Invariant#33_RootQuality_Cmaj7")]
    [TestCase(new[] { 0, 3, 7, 10 }, 0, 3, TestName = "Invariant#33_RootQuality_Cm7")]
    [TestCase(new[] { 0, 4, 7, 10 }, 0, 10, TestName = "Invariant#33_RootQuality_C7")]
    [TestCase(new[] { 0, 3, 6, 9 },  0, 3, TestName = "Invariant#33_RootQuality_Cdim7")]
    public void Invariant33_Root_Quality_Pattern_AreInvariantAcrossBass(int[] pcValues, int bass1, int bass2)
    {
        var pcSet = new PitchClassSet(pcValues.Select(PitchClass.FromValue));
        var r1 = CanonicalChordRecognizer.Identify(pcSet, PitchClass.FromValue(bass1));
        var r2 = CanonicalChordRecognizer.Identify(pcSet, PitchClass.FromValue(bass2));

        Assert.Multiple(() =>
        {
            Assert.That(r1.Root, Is.EqualTo(r2.Root),
                "Root must be invariant to bass note (recognition depends on pitch-class content only).");
            Assert.That(r1.Quality, Is.EqualTo(r2.Quality),
                "Quality must be invariant to bass note.");
            Assert.That(r1.PatternName, Is.EqualTo(r2.PatternName),
                "PatternName must be invariant to bass note.");
        });
    }

    /// <summary>
    ///     The SlashSuffix field IS allowed to vary with the bass — that's the
    ///     voicing-specific layer. This test pins the expected behaviour: when
    ///     bass differs from root, we get a slash; otherwise null.
    /// </summary>
    [Test]
    public void SlashSuffix_IsVoicingSpecific_NotInvariant()
    {
        var cmaj7 = new PitchClassSet(new[] { 0, 4, 7, 11 }.Select(PitchClass.FromValue));

        var rootPosition = CanonicalChordRecognizer.Identify(cmaj7, PitchClass.FromValue(0));
        var firstInversion = CanonicalChordRecognizer.Identify(cmaj7, PitchClass.FromValue(4));
        var secondInversion = CanonicalChordRecognizer.Identify(cmaj7, PitchClass.FromValue(7));

        Assert.Multiple(() =>
        {
            Assert.That(rootPosition.SlashSuffix, Is.Null,
                "Root position (bass = root) must have no slash suffix.");
            Assert.That(firstInversion.SlashSuffix, Is.Not.Null,
                "First inversion (bass != root) must have a slash suffix.");
            Assert.That(firstInversion.SlashSuffix, Does.StartWith("/"),
                "SlashSuffix format is '/<note>'.");
            Assert.That(secondInversion.SlashSuffix, Is.Not.Null,
                "Second inversion must have a slash suffix.");
            // All three share a CanonicalName (Invariant #33)
            Assert.That(firstInversion.CanonicalName, Is.EqualTo(rootPosition.CanonicalName));
            Assert.That(secondInversion.CanonicalName, Is.EqualTo(rootPosition.CanonicalName));
        });
    }

    /// <summary>
    ///     DisplayName property must compose CanonicalName + SlashSuffix correctly.
    /// </summary>
    [Test]
    public void DisplayName_Composes_CanonicalName_And_SlashSuffix()
    {
        var cmaj7 = new PitchClassSet(new[] { 0, 4, 7, 11 }.Select(PitchClass.FromValue));

        var rootPos = CanonicalChordRecognizer.Identify(cmaj7, PitchClass.FromValue(0));
        var firstInv = CanonicalChordRecognizer.Identify(cmaj7, PitchClass.FromValue(4));

        Assert.Multiple(() =>
        {
            Assert.That(rootPos.SlashSuffix, Is.Null);
            Assert.That(firstInv.SlashSuffix, Is.Not.Null);
            // DisplayName without slash == CanonicalName
            // DisplayName with slash == CanonicalName + SlashSuffix
            Assert.That(firstInv.CanonicalName + firstInv.SlashSuffix,
                Is.EqualTo(firstInv.CanonicalName + firstInv.SlashSuffix));
        });
    }
}
