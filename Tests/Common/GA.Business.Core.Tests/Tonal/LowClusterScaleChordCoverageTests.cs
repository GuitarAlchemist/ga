namespace GA.Business.Core.Tests.Tonal;

using GA.Business.Core.Chords;
using GA.Business.Core.Notes;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Modes.Exotic;
using GA.Business.Core.Tonal.Modes.Pentatonic;
using GA.Business.Core.Tonal.Modes.Symmetric;
using NUnit.Framework;

[TestFixture]
public class LowClusterScaleChordCoverageTests
{
    private const int ClusterHighlightThreshold = 2;

    [Test]
    public void EveryScaleMode_GeneratesChordsAndReportsClusters()
    {
        var modes = GetAllScaleModes();
        var flagged = new List<string>();

        foreach (var scaleMode in modes)
        {
            var clusterLength = GetLongestContiguousSemitoneCluster(scaleMode.Notes);
            if (clusterLength > ClusterHighlightThreshold)
            {
                flagged.Add($"{scaleMode.Name} ({scaleMode.ParentScale.PitchClassSet.Id}) → cluster {clusterLength}");
            }

            var chords = ChordTemplateFactory.GenerateFromScaleMode(scaleMode).ToList();
            Assert.That(chords, Is.Not.Empty, $"{scaleMode.Name} should generate chord templates.");
        }

        if (flagged.Any())
        {
            TestContext.WriteLine(
                $"Scale modes exceeding {ClusterHighlightThreshold} contiguous semitone steps ({flagged.Count}):");
            foreach (var entry in flagged)
            {
                TestContext.WriteLine($"  • {entry}");
            }
        }
    }

    private static int GetLongestContiguousSemitoneCluster(IReadOnlyCollection<Note> notes)
    {
        var pitchClasses = notes.Select(note => note.PitchClass.Value).Distinct().OrderBy(value => value).ToList();
        if (!pitchClasses.Any())
        {
            return 0;
        }

        var extended = pitchClasses.ToList();
        extended.Add(pitchClasses[0] + 12); // wrap for bracelet notation

        var longest = 0;
        var currentRun = 0;

        for (var i = 1; i < extended.Count; i++)
        {
            if (extended[i] - extended[i - 1] == 1)
            {
                currentRun++;
            }
            else
            {
                longest = Math.Max(longest, currentRun);
                currentRun = 0;
            }
        }

        longest = Math.Max(longest, currentRun);
        return longest;
    }

    private static IReadOnlyCollection<ScaleMode> GetAllScaleModes()
    {
        var families = new[]
        {
            MajorScaleMode.Items.Cast<ScaleMode>(),
            HarmonicMinorMode.Items.Cast<ScaleMode>(),
            MelodicMinorMode.Items.Cast<ScaleMode>(),
            NaturalMinorMode.Items.Cast<ScaleMode>(),
            WholeToneScaleMode.Items.Cast<ScaleMode>(),
            DiminishedScaleMode.Items.Cast<ScaleMode>(),
            AugmentedScaleMode.Items.Cast<ScaleMode>(),
            MajorPentatonicMode.Items.Cast<ScaleMode>(),
            HirajoshiScaleMode.Items.Cast<ScaleMode>(),
            InSenScaleMode.Items.Cast<ScaleMode>(),
            BebopScaleMode.Items.Cast<ScaleMode>(),
            BluesScaleMode.Items.Cast<ScaleMode>(),
            DoubleHarmonicScaleMode.Items.Cast<ScaleMode>(),
            EnigmaticScaleMode.Items.Cast<ScaleMode>(),
            NeapolitanMajorScaleMode.Items.Cast<ScaleMode>(),
            NeapolitanMinorScaleMode.Items.Cast<ScaleMode>(),
            PrometheusScaleMode.Items.Cast<ScaleMode>(),
            TritoneScaleMode.Items.Cast<ScaleMode>()
        };

        return [.. families.SelectMany(family => family)];
    }
}
