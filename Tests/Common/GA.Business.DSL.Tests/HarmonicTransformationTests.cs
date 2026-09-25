namespace GA.Business.DSL.Tests;

using Microsoft.FSharp.Collections;
using Services;

[TestFixture]
public class HarmonicTransformationTests
{
    private readonly HarmonicTransformationService _service = new();

    [Test]
    public void Test_Transpose_CMajor_To_DMajor()
    {
        // C Major: {0, 4, 7}
        var cMajor = new FSharpSet<int>(new[] { 0, 4, 7 });

        // Transpose +2 (D Major)
        var dMajor = _service.Transpose(2, cMajor);

        var expected = new[] { 2, 6, 9 };
        Assert.That(dMajor.OrderBy(x => x), Is.EquivalentTo(expected));
    }

    [Test]
    public void Test_NegativeHarmony_CMajor()
    {
        // C Major triad: {0, 4, 7}
        var cMajor = new FSharpSet<int>(new[] { 0, 4, 7 });

        // Negative harmony over C-G axis (sum = 7)
        // 0 -> 7 (G)
        // 4 -> 3 (Eb)
        // 7 -> 0 (C)
        // Result: {0, 3, 7} (C Minor)
        var result = _service.ApplyNegativeHarmony(7, cMajor);

        var expected = new[] { 0, 3, 7 };
        Assert.That(result.OrderBy(x => x), Is.EquivalentTo(expected));
    }
    // Rotations of equal span used to be resolved by list position, so the answer moved with the
    // transposition: {0,4,7,8} gave [0;4;7;8] but the same set transposed by 8 gave [0;3;4;8].
    [Test]
    public void GetNormalForm_TieOfSpans_IsResolvedByIntervals()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_service.GetNormalForm(new FSharpSet<int>(new[] { 0, 4, 7, 8 })).ToArray(), Is.EqualTo(new[] { 0, 3, 4, 8 }));
            Assert.That(_service.GetNormalForm(new FSharpSet<int>(new[] { 8, 0, 3, 4 })).ToArray(), Is.EqualTo(new[] { 0, 3, 4, 8 }));
        });
    }

    // Rahn and Forte break a tie of spans differently; {0,1,5,6,8} is a set where they disagree
    // (Forte packs from the left and gives [0;1;3;7;8]). The service follows Rahn.
    [Test]
    public void GetNormalForm_FollowsRahnTieBreak() =>
        Assert.That(_service.GetNormalForm(new FSharpSet<int>(new[] { 0, 1, 5, 6, 8 })).ToArray(), Is.EqualTo(new[] { 0, 1, 5, 6, 8 }));

    [Test]
    public void GetNormalForm_IsTranspositionInvariant_ForAll4096Sets()
    {
        var failures = new List<string>();
        for (var mask = 1; mask < 4096; mask++)
        {
            var set = new FSharpSet<int>(Enumerable.Range(0, 12).Where(pc => (mask & (1 << pc)) != 0));
            var normal = _service.GetNormalForm(set).ToArray();
            for (var t = 1; t < 12; t++)
            {
                var transposed = _service.GetNormalForm(_service.Transpose(t, set)).ToArray();
                if (!transposed.SequenceEqual(normal))
                {
                    failures.Add($"{{{string.Join(",", set)}}} T{t}: [{string.Join(";", transposed)}] vs [{string.Join(";", normal)}]");
                }
            }
        }

        Assert.That(failures, Is.Empty, $"{failures.Count} transpositions changed the normal form, e.g. {failures.FirstOrDefault()}");
    }
}
