namespace GA.Domain.Core.Tests.Theory.Harmony;

using System.Collections.Generic;
using System.Linq;
using GA.Domain.Core.Theory.Harmony;
using NUnit.Framework;

/// <summary>
///     <see cref="ChordIntervalPattern.TryMatch" /> counts missing, extra and shared intervals with
///     12-bit masks. These tests compare it with the set arithmetic it replaced, for every pattern of
///     the catalog and every one of the 4096 interval sets, so the rewrite cannot drift.
/// </summary>
[TestFixture]
public class ChordIntervalPatternMatchTests
{
    // The previous implementation, kept here as the reference
    private static MatchResult? Reference(ChordIntervalPattern pattern, IReadOnlyCollection<int> intervals, int maxMissing, int maxExtra)
    {
        var patternSet = new HashSet<int>(pattern.Intervals);
        var voicingSet = new HashSet<int>(intervals);
        var missing = patternSet.Except(voicingSet).Count();
        var extra = voicingSet.Except(patternSet).Count();
        if (missing > maxMissing || extra > maxExtra) return null;
        return new MatchResult(pattern, patternSet.Intersect(voicingSet).Count(), missing, extra);
    }

    private static int[] Intervals(int mask) => [.. Enumerable.Range(0, 12).Where(i => (mask & (1 << i)) != 0)];

    [Test]
    public void TryMatch_AgreesWithSetArithmetic_ForEveryPatternAndEveryIntervalSet()
    {
        var mismatches = 0;
        foreach (var pattern in CanonicalChordPatternCatalog.All)
        {
            for (var mask = 0; mask < 4096; mask++)
            {
                var array = Intervals(mask);
                IReadOnlyCollection<int>[] shapes = [array, new HashSet<int>(array), array.ToList()];
                for (var maxMissing = 0; maxMissing <= 2; maxMissing++)
                for (var maxExtra = 0; maxExtra <= 2; maxExtra++)
                {
                    var expected = Reference(pattern, array, maxMissing, maxExtra);
                    foreach (var shape in shapes)
                    {
                        if (!Equals(pattern.TryMatch(shape, maxMissing, maxExtra), expected)) mismatches++;
                    }
                }
            }
        }

        Assert.That(mismatches, Is.Zero);
    }

    [Test]
    public void TryMatch_KeepsSetSemantics_ForDuplicatesAndOutOfRangeValues()
    {
        var major = CanonicalChordPatternCatalog.All.First(p => p.Name == "major-triad");
        int[][] inputs = [[0, 4, 7, 7, 0], [0, 4, 7, 12], [-1, 0, 4], [0, 4, 7, 19, 24]];

        Assert.Multiple(() =>
        {
            foreach (var input in inputs)
            {
                Assert.That(major.TryMatch(input, 2, 2), Is.EqualTo(Reference(major, input, 2, 2)), string.Join(",", input));
            }
        });
    }
}
