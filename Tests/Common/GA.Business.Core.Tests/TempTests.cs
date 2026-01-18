using NUnit.Framework;
using GA.Business.Core.Atonal;
using System.Linq;
using System;

namespace GA.Business.Core.Tests.Temp;

[TestFixture]
public class TempTests
{
    [Test]
    public void PrintICVs()
    {
        var major = new PitchClassSet(new[] { 0, 2, 4, 5, 7, 9, 11 }.Select(PitchClass.FromValue));
        var harmonicMinor = new PitchClassSet(new[] { 0, 2, 3, 5, 7, 8, 11 }.Select(PitchClass.FromValue));
        var melodicMinor = new PitchClassSet(new[] { 0, 2, 3, 5, 7, 9, 11 }.Select(PitchClass.FromValue));

        Console.WriteLine($"[DEBUG_LOG] Major ICV: {major.IntervalClassVector}");
        Console.WriteLine($"[DEBUG_LOG] Harmonic Minor ICV: {harmonicMinor.IntervalClassVector}");
        Console.WriteLine($"[DEBUG_LOG] Melodic Minor ICV: {melodicMinor.IntervalClassVector}");
    }
}
