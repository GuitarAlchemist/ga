namespace GA.Domain.Core.Tests.Theory.Atonal;

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using GA.Domain.Core.Theory.Atonal;
using NUnit.Framework;

/// <summary>
///     <see cref="AtonalExtensions.ToIntervalClassVector{T}" /> stores the id it computes for an
///     <see cref="ImmutableSortedSet{T}" /> of pitch classes, which is what
///     <see cref="PitchClassSet.IntervalClassVector" /> passes. These tests compare the stored path with the
///     general one, which a <see cref="System.Collections.Generic.List{T}" /> still takes, on all 4096 sets.
/// </summary>
[TestFixture]
public class IntervalClassVectorCacheTests
{
    private static PitchClassSet Set(int id) => PitchClassSet.FromId(PitchClassSetId.FromValue(id));

    [Test]
    public void StoredId_MatchesTheGeneralComputation_ForEverySet()
    {
        var mismatches = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var id = 0; id < 4096; id++)
            {
                var set = Set(id);
                var general = set.ToList().ToIntervalClassVector();
                var stored = set.IntervalClassVector;
                var sortedSet = ImmutableSortedSet.CreateRange(set).ToIntervalClassVector();
                if (stored.Id != general.Id || sortedSet.Id != general.Id || stored.ToString() != general.ToString()) mismatches++;
            }
        }

        Assert.That(mismatches, Is.Zero);
    }

    [Test]
    public void ConcurrentReads_ReturnTheGeneralComputation()
    {
        var expected = Enumerable.Range(0, 4096).Select(id => Set(id).ToList().ToIntervalClassVector().Id.Value).ToArray();
        var actual = new int[4096];
        Parallel.For(0, 4096, id => actual[id] = Set(id).IntervalClassVector.Id.Value);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void EachRead_ReturnsANewInstance()
    {
        var set = Set(2741);

        Assert.That(set.IntervalClassVector, Is.Not.SameAs(set.IntervalClassVector));
        Assert.That(set.IntervalClassVector.ToString(), Is.EqualTo("<2 5 4 3 6 1>"));
    }
}
