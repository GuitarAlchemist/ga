namespace GA.Business.DSL.Tests;

using NUnit.Framework;
using GA.Business.DSL.Services;
using Microsoft.FSharp.Collections;
using System.Linq;

[TestFixture]
public class HarmonicTransformationTests
{
    private readonly HarmonicTransformationService _service = new();

    [Test]
    public void Test_Transpose_CMajor_To_DMajor()
    {
        // C Major: {0, 4, 7}
        var cMajor = new FSharpSet<int>(new[] { 0, 4, 7 });
        
        // Transpose +2 (D Major)
        var dMajor = _service.Transpose(2, cMajor);
        
        var expected = new[] { 2, 6, 9 };
        Assert.That(dMajor.OrderBy(x => x), Is.EquivalentTo(expected));
    }

    [Test]
    public void Test_NegativeHarmony_CMajor()
    {
        // C Major triad: {0, 4, 7}
        var cMajor = new FSharpSet<int>(new[] { 0, 4, 7 });
        
        // Negative harmony over C-G axis (sum = 7)
        // 0 -> 7 (G)
        // 4 -> 3 (Eb)
        // 7 -> 0 (C)
        // Result: {0, 3, 7} (C Minor)
        var result = _service.ApplyNegativeHarmony(7, cMajor);
        
        var expected = new[] { 0, 3, 7 };
        Assert.That(result.OrderBy(x => x), Is.EquivalentTo(expected));
    }
}
