namespace GA.Business.ML.Tests.TestInfrastructure;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Fretboard.Analysis;
using NUnit.Framework;

public static class MusicalAssertions
{
    public enum AssertionSeverity { Info, Warning, Error, Critical }

    /// <summary>
    /// Asserts that a realization is "Physically Playable" according to a cost threshold.
    /// Supports "Soft Failures" (Warnings) for borderline cases.
    /// </summary>
    public static void IsPlayable(
        List<FretboardPosition> shape, 
        PhysicalCostService costService, 
        double maxCost = 5.0,
        double warningThreshold = 3.5)
    {
        var cost = costService.CalculateStaticCost(shape);
        var breakdownStr = string.Join(", ", cost.Breakdown.Select(kv => $"{kv.Key}:{kv.Value:F2}"));

        if (cost.TotalCost > maxCost)
        {
            Assert.Fail($"[CRITICAL] Shape is unplayable (Cost: {cost.TotalCost:F2}). Breakdown: {breakdownStr}");
        }
        else if (cost.TotalCost > warningThreshold)
        {
            TestContext.WriteLine($"[WARNING] Shape is difficult but acceptable (Cost: {cost.TotalCost:F2}). Breakdown: {breakdownStr}");
        }
        else
        {
            TestContext.WriteLine($"[INFO] Shape is ergonomic (Cost: {cost.TotalCost:F2}).");
        }
    }

    /// <summary>
    /// Asserts that two realizations are harmonically equivalent.
    /// </summary>
    public static void AreHarmonicallyEquivalent(List<FretboardPosition> expected, List<FretboardPosition> actual)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count), "Note count mismatch");

        var expectedPitches = expected.Select(p => p.Pitch.MidiNote.Value).OrderBy(v => v).ToList();
        var actualPitches = actual.Select(p => p.Pitch.MidiNote.Value).OrderBy(v => v).ToList();

        for (int i = 0; i < expectedPitches.Count; i++)
        {
            Assert.That(actualPitches[i], Is.EqualTo(expectedPitches[i]), 
                $"Pitch mismatch at index {i}. Expected MIDI {expectedPitches[i]}, got {actualPitches[i]}");
        }
    }

    /// <summary>
    /// Asserts that a realization is "Physically Playable" according to a cost threshold.
    /// </summary>
    public static void IsPlayable(List<FretboardPosition> shape, PhysicalCostService costService, double maxCost = 5.0)
    {
        var cost = costService.CalculateStaticCost(shape);
        Assert.That(cost.TotalCost, Is.LessThan(maxCost), 
            $"Shape is too difficult to play (Cost: {cost.TotalCost:F2}). Breakdown: {string.Join(", ", cost.Breakdown.Select(kv => $"{kv.Key}:{kv.Value:F2}"))}");
    }

    /// <summary>
    /// Asserts that a sequence of realizations is "Smooth" (low transition costs).
    /// </summary>
    public static void IsSmoothSequence(List<List<FretboardPosition>> sequence, PhysicalCostService costService, double maxAvgTransition = 6.0)
    {
        if (sequence.Count < 2) return;

        double totalTrans = 0;
        for (int i = 0; i < sequence.Count - 1; i++)
        {
            totalTrans += costService.CalculateTransitionCost(sequence[i], sequence[i + 1]);
        }

        double avg = totalTrans / (sequence.Count - 1);
        Assert.That(avg, Is.LessThan(maxAvgTransition), 
            $"Sequence is too jumpy (Avg Transition Cost: {avg:F2})");
    }
}
