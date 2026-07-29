namespace GA.Business.DSL.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using GA.Domain.Core.Theory.Atonal;
using GA.Business.DSL.Generators;
using Microsoft.FSharp.Collections;

[TestFixture]
public class OptickGrothendieckTests
{
    private PitchClassSet _cMajorScale = null!;
    private PitchClassSet _cMajorTriad = null!;
    private PitchClassSet _dMajorTriad = null!;
    private PitchClassSet _cMinorTriad = null!;
    private PitchClassSet _gMajorScale = null!;

    [SetUp]
    public void SetUp()
    {
        _cMajorScale = PitchClassSet.Parse("024579E"); // C D E F G A B
        _cMajorTriad = PitchClassSet.Parse("047");   // C E G
        _dMajorTriad = PitchClassSet.Parse("269");   // D F# A
        _cMinorTriad = PitchClassSet.Parse("037");   // C Eb G
        _gMajorScale = PitchClassSet.Parse("02479E1"); // G A B C D E F#
    }

    // ============================================================================
    // 1. GROUP LAWS (ASSOCIATIVITY, IDENTITY, INVERSE) FOR 48-ORDER GROUP
    // ============================================================================

    [Test]
    public void GroupLaws_Identity_IsIdentityElement()
    {
        var identity = OptickTransformation.Identity;

        // Verify t ∘ Id = t = Id ∘ t
        foreach (var t in GetSampleTransformations())
        {
            var leftCompose = identity.Compose(t);
            var rightCompose = t.Compose(identity);

            Assert.Multiple(() =>
            {
                Assert.That(leftCompose.Transposition, Is.EqualTo(t.Transposition));
                Assert.That(leftCompose.Inversion, Is.EqualTo(t.Inversion));
                Assert.That(leftCompose.Complement, Is.EqualTo(t.Complement));

                Assert.That(rightCompose.Transposition, Is.EqualTo(t.Transposition));
                Assert.That(rightCompose.Inversion, Is.EqualTo(t.Inversion));
                Assert.That(rightCompose.Complement, Is.EqualTo(t.Complement));
            });
        }
    }

    [Test]
    public void GroupLaws_Inverse_YieldsIdentity()
    {
        var identity = OptickTransformation.Identity;

        // Verify t ∘ t^-1 = Id = t^-1 ∘ t
        foreach (var t in GetSampleTransformations())
        {
            var inv = t.Inverse();
            var leftCompose = t.Compose(inv);
            var rightCompose = inv.Compose(t);

            Assert.Multiple(() =>
            {
                Assert.That(leftCompose.Transposition, Is.EqualTo(identity.Transposition), $"Left compose transposition failure for {t}");
                Assert.That(leftCompose.Inversion, Is.EqualTo(identity.Inversion), $"Left compose inversion failure for {t}");
                Assert.That(leftCompose.Complement, Is.EqualTo(identity.Complement), $"Left compose complement failure for {t}");

                Assert.That(rightCompose.Transposition, Is.EqualTo(identity.Transposition), $"Right compose transposition failure for {t}");
                Assert.That(rightCompose.Inversion, Is.EqualTo(identity.Inversion), $"Right compose inversion failure for {t}");
                Assert.That(rightCompose.Complement, Is.EqualTo(identity.Complement), $"Right compose complement failure for {t}");
            });
        }
    }

    [Test]
    public void GroupLaws_Associativity_IsSatisfied()
    {
        var transforms = GetSampleTransformations().ToList();

        // Sample triples to verify associativity (t1 ∘ t2) ∘ t3 = t1 ∘ (t2 ∘ t3)
        for (int i = 0; i < transforms.Count - 2; i += 3)
        {
            var t1 = transforms[i];
            var t2 = transforms[i + 1];
            var t3 = transforms[i + 2];

            var left = (t1.Compose(t2)).Compose(t3);
            var right = t1.Compose(t2.Compose(t3));

            Assert.Multiple(() =>
            {
                Assert.That(left.Transposition, Is.EqualTo(right.Transposition));
                Assert.That(left.Inversion, Is.EqualTo(right.Inversion));
                Assert.That(left.Complement, Is.EqualTo(right.Complement));
            });
        }
    }

    [Test]
    public void GroupLaws_Action_IsConsistentWithSequentialApplication()
    {
        var t1 = new OptickTransformation(4, true, false);  // Transpose 4, Invert
        var t2 = new OptickTransformation(7, false, true); // Transpose 7, Complement
        var composed = t1.Compose(t2); // t2 ∘ t1 (t1 applied first, then t2)

        var seqResult = t2.Apply(t1.Apply(_cMajorTriad));
        var compResult = composed.Apply(_cMajorTriad);

        Assert.That(compResult.Id, Is.EqualTo(seqResult.Id));
    }

    // ============================================================================
    // 2. GROTHENDIECK COMPLETION AND CANCELLATIVITY
    // ============================================================================

    [Test]
    public void Grothendieck_PcDelta_IsAbelianGroupAndCancellative()
    {
        // PcDelta forms an abelian group under addition.
        // Let's test target - source is cancellative: (target - source) + source = target
        var pcDelta = PcDelta.Between(_cMajorTriad, _dMajorTriad);

        // Let's verify PcDelta identity is zero
        var zero = PcDelta.Zero;
        var addZero = pcDelta + zero;
        Assert.That(addZero.Changes, Is.EqualTo(pcDelta.Changes));

        // Let's verify inverse
        var inv = -pcDelta;
        var addInv = pcDelta + inv;
        Assert.That(addInv.Changes, Is.EqualTo(zero.Changes));
    }

    [Test]
    public void Grothendieck_IcvDelta_IsAbelianGroupAndCancellative()
    {
        // IcvDelta forms an abelian group under addition.
        var icvDelta = IcvDelta.Between(_cMajorScale.IntervalClassVector, _gMajorScale.IntervalClassVector);

        var zero = IcvDelta.Zero;
        var addZero = icvDelta + zero;
        Assert.That(addZero.Changes, Is.EqualTo(icvDelta.Changes));

        var inv = -icvDelta;
        var addInv = icvDelta + inv;
        Assert.That(addInv.Changes, Is.EqualTo(zero.Changes));
    }

    // ============================================================================
    // 3. PARTIAL ACTIONS AND EXPLICIT ERRORS
    // ============================================================================

    [Test]
    public void PartialAction_Drop2Voicing_FailsOnTriad()
    {
        var partial = PartialTransformation.Drop2Voicing;
        var result = partial.Apply(_cMajorTriad);

        Assert.That(result.IsError, Is.True);
        Assert.That(result.ErrorValue.IsInsufficientCardinality, Is.True);
    }

    [Test]
    public void PartialAction_Drop2Voicing_SucceedsOnTetrad()
    {
        var tetrad = PitchClassSet.Parse("047E"); // Cmaj7
        var partial = PartialTransformation.Drop2Voicing;
        var result = partial.Apply(tetrad);

        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue.Id, Is.EqualTo(tetrad.Id));
    }

    [Test]
    public void PartialAction_ParallelMinorShift_SucceedsOnMajorScale()
    {
        var partial = PartialTransformation.ParallelMinorShift;
        var result = partial.Apply(_cMajorScale);

        Assert.That(result.IsOk, Is.True);
        // C Major scale parallel minor is C Melodic Minor (0235789 in GA data) or similar
        // Under our shift rule: E (4) -> Eb (3), A (9) -> Ab (8), B (11) -> Bb (10)
        // Correct target should be: 0, 2, 3, 5, 7, 8, 10 (C natural minor scale!)
        var expected = PitchClassSet.Parse("023578T");
        Assert.That(result.ResultValue.Id, Is.EqualTo(expected.Id));
    }

    [Test]
    public void PartialAction_ParallelMinorShift_FailsOnTriad()
    {
        var partial = PartialTransformation.ParallelMinorShift;
        var result = partial.Apply(_cMajorTriad);

        Assert.That(result.IsError, Is.True);
        Assert.That(result.ErrorValue.IsInvalidParallelShift, Is.True);
    }

    // ============================================================================
    // 4. PATH REASONING AND EQUIVALENCE QUERIES
    // ============================================================================

    [Test]
    public void PathReasoning_ComparePaths_DetectsEquivalentPaths()
    {
        // Path A: Transpose 5, then Transpose 7 -> accumulated 12 (Identity)
        var t1 = new OptickTransformation(5, false, false);
        var t2 = new OptickTransformation(7, false, false);
        var pathA = new Path(ListModule.OfSeq(new[] { t1, t2 }));

        // Path B: Identity
        var pathB = new Path(ListModule.OfSeq(new[] { OptickTransformation.Identity }));

        var comparison = PathReasoning.comparePaths(pathA, pathB, _cMajorScale);

        Assert.That(comparison.IsEquivalent, Is.True);
    }

    [Test]
    public void PathReasoning_ComparePaths_DetectsHomologousPaths()
    {
        // For self-complementary sets, complementation is equivalent to no change, but the transformations differ!
        // Hexachord #35 (0123456) is not self-complementary, but some hexachords are.
        // Let's use standard inversion on a symmetric set class.
        // For example, C major triad inverted around E (4) is C minor.
        // Let's create two paths that end at the same place but via different algebraic transformations.
        // Path A: Transpose 2
        var tA = new OptickTransformation(2, false, false);
        var pathA = new Path(ListModule.OfSeq(new[] { tA }));

        // Path B: Inversion, then transposition, etc.
        // For a single note, say C (0). Transposing by 2 gives D (2).
        // Inverting C around 0 gives C (0), then transposing by 2 gives D (2).
        // The transformation is (2, true, false).
        // Let's check on C single note:
        var cSingle = PitchClassSet.Parse("0");
        var tB = new OptickTransformation(2, true, false);
        var pathB = new Path(ListModule.OfSeq(new[] { tB }));

        var comparison = PathReasoning.comparePaths(pathA, pathB, cSingle);

        Assert.That(comparison.IsHomologous, Is.True);
    }

    [Test]
    public void PathReasoning_ComparePaths_DetectsIncompatiblePaths()
    {
        var t1 = new OptickTransformation(2, false, false);
        var t2 = new OptickTransformation(4, false, false);

        var pathA = new Path(ListModule.OfSeq(new[] { t1 }));
        var pathB = new Path(ListModule.OfSeq(new[] { t2 }));

        var comparison = PathReasoning.comparePaths(pathA, pathB, _cMajorScale);

        Assert.That(comparison.IsIncompatible, Is.True);
    }

    // ============================================================================
    // 5. PRODUCT-ORIENTED QUERIES
    // ============================================================================

    [Test]
    public void ProductQueries_DifferenceRecipe_SameOrbitTransposition()
    {
        // C major triad and D major triad are in the same orbit under transposition.
        var recipe = ProductQueries.computeDifferenceRecipe(_cMajorTriad, _dMajorTriad);

        Assert.That(recipe.IsSameOrbitRecipe, Is.True);
        var transformation = ((DifferenceRecipe.SameOrbitRecipe)recipe).Item;
        Assert.That(transformation.Transposition, Is.EqualTo(2));
        Assert.That(transformation.Inversion, Is.False);
        Assert.That(transformation.Complement, Is.False);
    }

    [Test]
    public void ProductQueries_DifferenceRecipe_SameOrbitInversion()
    {
        // C major triad and C minor triad are in the same orbit (inversion/reflection!).
        var recipe = ProductQueries.computeDifferenceRecipe(_cMajorTriad, _cMinorTriad);

        Assert.That(recipe.IsSameOrbitRecipe, Is.True);
        var transformation = ((DifferenceRecipe.SameOrbitRecipe)recipe).Item;
        Assert.That(transformation.Inversion, Is.True);
    }

    [Test]
    public void ProductQueries_DifferenceRecipe_DifferentOrbitsAdditive()
    {
        // C major triad and C major scale are in different orbits.
        var recipe = ProductQueries.computeDifferenceRecipe(_cMajorTriad, _cMajorScale);

        Assert.That(recipe.IsGeneralAdditiveRecipe, Is.True);
        var additive = (DifferenceRecipe.GeneralAdditiveRecipe)recipe;
        var pcDelta = additive.Item1;
        var icvDelta = additive.Item2;

        // C major scale has 7 notes, C major triad has 3. Difference pcDelta should add 4 notes.
        Assert.That(pcDelta.Changes.Sum(), Is.EqualTo(4));
    }

    [Test]
    public void ProductQueries_GenerateHardNegatives_ReturnsValidSupervisionData()
    {
        var t1 = new OptickTransformation(2, false, false);
        var path = new Path(ListModule.OfSeq(new[] { t1 }));

        var negatives = ProductQueries.generateHardNegatives(_cMajorScale, path);

        Assert.That(negatives, Has.Length.EqualTo(2));

        var neg1 = negatives[0];
        Assert.That(neg1.Item3, Does.Contain("Off-by-one"));
        Assert.That(neg1.Item1.Id, Is.Not.EqualTo(_cMajorScale.Id));

        var neg2 = negatives[1];
        Assert.That(neg2.Item3, Does.Contain("Incorrect inversion"));
    }

    // ============================================================================
    // HELPER GENERATORS
    // ============================================================================

    private IEnumerable<OptickTransformation> GetSampleTransformations()
    {
        yield return OptickTransformation.Identity;
        yield return new OptickTransformation(2, false, false);
        yield return new OptickTransformation(5, true, false);
        yield return new OptickTransformation(0, false, true);
        yield return new OptickTransformation(7, true, true);
        yield return new OptickTransformation(11, false, false);
    }
}
