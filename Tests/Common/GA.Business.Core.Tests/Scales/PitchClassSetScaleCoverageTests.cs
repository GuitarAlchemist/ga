namespace GA.Business.Core.Tests.Scales;

using System.Linq;
using System.Numerics;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Chords;
using GA.Business.Core.Scales;
using NUnit.Framework;

[TestFixture]
public class PitchClassSetScaleCoverageTests
{
    [Test]
    public void AllScalePitchClassSets_CreateAnalyticalChord()
    {
        var ids = PitchClassSetId.Items
            .Where(id => id.IsScale && id.Value != 0 && BitOperations.PopCount((uint)id.Value) >= 3)
            .ToList();

        Assert.That(ids.Count, Is.GreaterThan(0), "Expect at least one scale pitch class set.");

        foreach (var id in ids)
        {
            var pitchClassSet = id.ToPitchClassSet();
            var scale = Scale.FromPitchClassSetId(id);
            var chord = ChordTemplateFactory.FromPitchClassSet(pitchClassSet, $"Scale {id}");

            Assert.That(chord, Is.Not.Null, $"Chord templates should be creatable for scale {id}");
            Assert.That(chord.PitchClassSet.Cardinality, Is.EqualTo(scale.PitchClassSet.Cardinality),
                $"Scale {id} should preserve cardinality in chord template.");

            // Spot-check that we can analyze the pitch-class set via modal families if it's modal.
            if (scale.IsModal && scale.ModalFamily is { } modalFamily)
            {
                Assert.That(modalFamily.ModeIds.Contains(id), Is.True,
                    $"Modal family {modalFamily} should contain scale ID {id}");
            }
        }
    }

    [Test]
    public void PrimeScaleCount_IsReasonable()
    {
        var scaleCount = PitchClassSetId.Items.Count(id => id.IsScale && id.Value != 0);
        TestContext.WriteLine($"Total scale-form pitch class sets: {scaleCount}");
        Assert.That(scaleCount, Is.GreaterThan(500));
    }
}
