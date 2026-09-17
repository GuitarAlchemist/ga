namespace GA.Domain.Core.Tests.Theory.Atonal;

using System.Linq;
using GA.Domain.Core.Theory.Atonal;
using NUnit.Framework;

/// <summary>
///     <see cref="IntervalClassVectorId" /> packs the six counts as base-12 digits. The chromatic
///     aggregate is the only set with a count of 12, which overflows its digit: its id must keep its
///     value (ids are used as keys and labels) and still decode to &lt;12 12 12 12 12 6&gt;.
/// </summary>
[TestFixture]
public class IntervalClassVectorIdTests
{
    private static IntervalClassVector IcvOf(int count) =>
        new PitchClassSet(Enumerable.Range(0, count).Select(PitchClass.FromValue)).IntervalClassVector;

    [Test]
    public void ChromaticAggregate_DecodesToTwelves() =>
        Assert.Multiple(() =>
        {
            Assert.That(IcvOf(12).Vector.Values, Is.EqualTo(new[] { 12, 12, 12, 12, 12, 6 }));
            Assert.That(IcvOf(12).ToString(), Is.EqualTo("<12 12 12 12 12 6>"));
            Assert.That(IntervalClassVector.Parse("<12 12 12 12 12 6>"), Is.EqualTo(IcvOf(12)));
        });

    [Test]
    public void Ids_KeepTheirBase12Values() =>
        Assert.Multiple(() =>
        {
            // 12·12⁵ + 12·12⁴ + 12·12³ + 12·12² + 12·12 + 6
            Assert.That(IcvOf(12).Id.Value, Is.EqualTo(3257430));
            // 10·12⁵ + 10·12⁴ + 10·12³ + 10·12² + 10·12 + 5
            Assert.That(IcvOf(11).Id.Value, Is.EqualTo(2714525));
            Assert.That(IcvOf(11).ToString(), Is.EqualTo("<10 10 10 10 10 5>"));
        });

    [Test]
    public void EveryPitchClassSet_DecodesToItsCountedIntervals()
    {
        static int[] Counted(PitchClassSet set)
        {
            var values = set.Select(pc => pc.Value).ToArray();
            var counts = new int[6];
            for (var i = 0; i < values.Length; i++)
            {
                for (var j = i + 1; j < values.Length; j++)
                {
                    var semitones = System.Math.Abs(values[i] - values[j]);
                    counts[System.Math.Min(semitones, 12 - semitones) - 1]++;
                }
            }

            return counts;
        }

        var mismatches = PitchClassSet.Items
            .Where(set => !set.IntervalClassVector.Vector.Values.SequenceEqual(Counted(set)))
            .Select(set => set.ToString())
            .ToArray();

        Assert.That(mismatches, Is.Empty);
    }
}
