namespace GA.Business.ML.Tests;

using System;
using System.Linq;
using GA.Business.ML.Musical.Enrichment;
using NUnit.Framework;

[TestFixture]
public class ModalDebugTests
{
    [Test]
    public void DumpIntervals()
    {
        var service = ModalCharacteristicIntervalService.Instance;
        var names = service.GetAllModeNames().OrderBy(n => n).ToList();

        Console.WriteLine($"Total Modes: {names.Count()}");
        foreach(var name in names)
        {
            var intervals = service.GetCharacteristicSemitones(name);
            var str = string.Join(",", intervals.OrderBy(x => x));
            Console.WriteLine($"{name}: [{str}]");
        }
    }
}
