namespace GA.Business.ML.Tests;

using System;
using System.Linq;
using GA.Business.Core.Atonal;
using NUnit.Framework;

[TestFixture]
public class ForteDebugTests
{
    [Test]
    public void DebugForteNumbers()
    {
        // C Major: 0, 2, 4, 5, 7, 9, 11
        var pcs = new PitchClassSet(new[] { 0, 2, 4, 5, 7, 9, 11 }.Select(i => PitchClass.FromValue(i)));
        Console.WriteLine($"C Major PCS: {pcs}");
        Console.WriteLine($"Prime Form: {pcs.PrimeForm}");
        
        if (ForteCatalog.TryGetForteNumber(pcs.PrimeForm, out var forte))
        {
            Console.WriteLine($"Forte: {forte}");
        }
        else 
        {
            Console.WriteLine("Forte not found");
        }

        // Whole Tone: 0, 2, 4, 6, 8, 10
        var wt = new PitchClassSet(new[] { 0, 2, 4, 6, 8, 10 }.Select(i => PitchClass.FromValue(i)));
        Console.WriteLine($"Whole Tone PCS: {wt}");
        Console.WriteLine($"Prime Form: {wt.PrimeForm}");
        
        if (ForteCatalog.TryGetForteNumber(wt.PrimeForm, out var f2))
        {
            Console.WriteLine($"Forte: {f2}");
        }
        
        // Vienna: 0, 1, 6
        var v = new PitchClassSet(new[] { 0, 1, 6 }.Select(i => PitchClass.FromValue(i)));
        Console.WriteLine($"Vienna PCS: {v}");
        Console.WriteLine($"Prime Form: {v.PrimeForm}");
        
        if (ForteCatalog.TryGetForteNumber(v.PrimeForm, out var f3))
        {
            Console.WriteLine($"Forte: {f3}");
        }
    }
}
