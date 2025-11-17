namespace GA.Business.Core.Tests.Chords;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Chords;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Modes.Exotic;
using GA.Business.Core.Tonal.Modes.Pentatonic;
using GA.Business.Core.Tonal.Modes.Symmetric;
using NUnit.Framework;

[TestFixture]
public class ChordTemplateScaleCoverageTests
{
    [Test]
    public void MajorAndMinorModes_AllProduceChords()
    {
        var groups = new Dictionary<string, IReadOnlyCollection<ScaleMode>>
        {
            ["Major (Diatonic)"] = [.. MajorScaleMode.Items.Cast<ScaleMode>()],
            ["HarmonicMinor"] = [.. HarmonicMinorMode.Items.Cast<ScaleMode>()],
            ["MelodicMinor"] = [.. MelodicMinorMode.Items.Cast<ScaleMode>()],
            ["Symmetrical"] =
            [
                .. WholeToneScaleMode.Items.Cast<ScaleMode>()
,
                .. DiminishedScaleMode.Items.Cast<ScaleMode>(),
                .. AugmentedScaleMode.Items.Cast<ScaleMode>(),
            ],
            ["Pentatonic"] =
            [
                .. MajorPentatonicMode.Items.Cast<ScaleMode>()
,
                .. HirajoshiScaleMode.Items.Cast<ScaleMode>(),
                .. InSenScaleMode.Items.Cast<ScaleMode>(),
            ],
            ["Exotic"] =

            [
                .. BebopScaleMode.Items.Cast<ScaleMode>()
,
                .. BluesScaleMode.Items.Cast<ScaleMode>(),
                .. DoubleHarmonicScaleMode.Items.Cast<ScaleMode>(),
                .. EnigmaticScaleMode.Items.Cast<ScaleMode>(),
                .. NeapolitanMajorScaleMode.Items.Cast<ScaleMode>(),
                .. NeapolitanMinorScaleMode.Items.Cast<ScaleMode>(),
                .. PrometheusScaleMode.Items.Cast<ScaleMode>(),
                .. TritoneScaleMode.Items.Cast<ScaleMode>(),
            ]
        };

        foreach (var (groupName, modes) in groups)
        {
            var total = modes.Sum(mode => ChordTemplateFactory.GenerateFromScaleMode(mode).Count());
            Assert.That(total, Is.GreaterThan(0), $"{groupName} should generate at least one chord template.");
        }
    }

    [Test]
    public void GenerateAllPossibleChords_ProducesThousands()
    {
        var chords = ChordTemplateFactory.GenerateAllPossibleChords().ToList();
        Assert.That(chords.Count, Is.GreaterThan(1000), "Full generation should produce over a thousand templates.");
    }
}
