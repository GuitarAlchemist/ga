namespace GA.Business.Core.Tests.Atonal.Geometry;

using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard.Shapes.Geometry;
using NUnit.Framework;

[TestFixture]
public class VoiceLeadingSpaceOpticTests
{
    [Test]
    public void Opt_Invariance_Triad_OPT_ShouldBeZero()
    {
        // C major triad vs. its transposition by 11 semitones (B): [0,4,7] vs [11,3,6]
        var space = new VoiceLeadingSpace(
            voices: 3,
            octaveEquivalence: true,
            permutationEquivalence: true,
            transpositionEquivalence: true,
            inversionEquivalence: false);

        var a = new[] { 0.0, 4.0, 7.0 };
        var b = new[] { 11.0, 3.0, 6.0 };
        var d = space.Distance(a, b);
        Assert.That(d, Is.EqualTo(0.0).Within(1e-9));
    }

    [Test]
    public void Permutation_Disabled_ShouldAffectDistance()
    {
        var space = new VoiceLeadingSpace(
            voices: 3,
            octaveEquivalence: true,
            permutationEquivalence: false, // critical
            transpositionEquivalence: false,
            inversionEquivalence: false);

        var a = new[] { 0.0, 4.0, 7.0 };
        var bPermuted = new[] { 7.0, 4.0, 0.0 };
        var d = space.Distance(a, bPermuted);

        // With P off, voices must align index-wise; expect nonzero unless identical ordering
        Assert.That(d, Is.GreaterThan(0.0));
    }

    [Test]
    public void Inversion_Equivalence_Toggle_Behavior()
    {
        var a = new[] { 0.0, 4.0, 7.0 };
        var invA = new[] { 0.0, -4.0, -7.0 }; // raw inversion around 0; space handles mod if enabled

        var spaceNoI = new VoiceLeadingSpace(3, true, true, true, false);
        var dNoI = spaceNoI.Distance(a, invA);
        Assert.That(dNoI, Is.GreaterThanOrEqualTo(0.0)); // usually > 0 unless symmetric set

        var spaceI = new VoiceLeadingSpace(3, true, true, true, true);
        var dI = spaceI.Distance(a, invA);
        Assert.That(dI, Is.LessThanOrEqualTo(dNoI));
    }

    [Test]
    public void EdgeCases_Empty_Singleton_Chromatic()
    {
        // Empty
        var space0 = new VoiceLeadingSpace(0, true, true, true, false);
        var empty = System.Array.Empty<double>();
        Assert.That(space0.Distance(empty, empty), Is.EqualTo(0.0));

        // Singleton under T
        var space1 = new VoiceLeadingSpace(1, true, true, true, false);
        var x = new[] { 3.0 };
        var y = new[] { 7.0 };
        Assert.That(space1.Distance(x, y), Is.EqualTo(0.0).Within(1e-9));
    }

    [Test]
    public void CardinalityEmbedding_Triad_Vs_Seventh_IsFinite()
    {
        // C major triad vs. Cmaj7: expect finite non-negative distance under embedding
        var triad = new SetClass(PitchClassSet.Parse("047"));
        var maj7 = new SetClass(PitchClassSet.Parse("047B")); // B = 11

        var d = SetClassOpticIndex.Distance(triad, maj7, VoiceLeadingOptions.Default);
        Assert.That(d, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(double.IsFinite(d), Is.True);
    }

    [Test]
    public void ChromaticSet_OPTI_Invariance_ShouldBeZero()
    {
        // 12-tone chromatic set vs. its transposition by 1
        var chromaA = new SetClass(PitchClassSet.Parse("0123456789TE"));
        var chromaB = new SetClass(PitchClassSet.Parse("123456789TE0"));

        var options = new VoiceLeadingOptions
        {
            OctaveEquivalence = true,
            PermutationEquivalence = true,
            TranspositionEquivalence = true,
            InversionEquivalence = true // OPTI
        };

        var d = SetClassOpticIndex.Distance(chromaA, chromaB, options);
        Assert.That(d, Is.EqualTo(0.0).Within(1e-9));
    }

    [Test]
    public void WholeToneSets_OPT_Invariance_ShouldBeZero()
    {
        // Two distinct whole-tone collections (T apart): {0,2,4,6,8,10} vs {1,3,5,7,9,11}
        var wtA = new SetClass(PitchClassSet.Parse("02468T"));
        var wtB = new SetClass(PitchClassSet.Parse("13579E"));

        var options = new VoiceLeadingOptions
        {
            OctaveEquivalence = true,
            PermutationEquivalence = true,
            TranspositionEquivalence = true,
            InversionEquivalence = false // OPT
        };

        var d = SetClassOpticIndex.Distance(wtA, wtB, options);
        Assert.That(d, Is.EqualTo(0.0).Within(1e-9));
    }

    [Test]
    public void KNN_Neighbors_ShouldBeSortedByDistance()
    {
        var triad = new SetClass(PitchClassSet.Parse("047"));
        var options = VoiceLeadingOptions.Default; // OPT by default
        var neighbors = SetClassOpticIndex.GetNearestByOptic(triad, k: 12, options);

        // Distances should be non-decreasing
        for (int i = 1; i < neighbors.Count; i++)
        {
            Assert.That(neighbors[i].distance, Is.GreaterThanOrEqualTo(neighbors[i - 1].distance));
        }
    }
}
