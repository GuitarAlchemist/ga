namespace GA.Business.DSL.Tests;

using NUnit.Framework;
using GA.Business.DSL.Services;
using GA.Business.DSL.Types;
using Microsoft.FSharp.Core;

[TestFixture]
public class ChordDslTests
{
    private readonly ChordDslService _service = new();

    [TestCase("C", "C")]
    [TestCase("Am7", "Am7")]
    [TestCase("G7#9", "G7#9")]
    [TestCase("Fmaj7/A", "Fmaj7/A")]
    [TestCase("Cmi7", "Cm7")]
    [TestCase("D-7", "Dm7")]
    [TestCase("EbΔ9", "Ebmaj9")]
    [TestCase("Cmin7b5", "Cm7b5")]
    public void Test_Normalization(string input, string expected)
    {
        var result = _service.Normalize(input);
        if (result.IsOk)
        {
            Assert.That(result.ResultValue, Is.EqualTo(expected));
        }
        else
        {
            Assert.Fail($"Failed to parse {input}: {result.ErrorValue}");
        }
    }

    [Test]
    public void Test_Complex_Alterations()
    {
        var input = "C13#11b9";
        var result = _service.Parse(input);
        
        Assert.That(result.IsOk, Is.True, result.IsError ? result.ErrorValue : "");
        var ast = result.ResultValue;
        
        Assert.That(ast.Root, Is.EqualTo("C"));
        Assert.That(ast.Components.Length, Is.EqualTo(3));
    }
}
