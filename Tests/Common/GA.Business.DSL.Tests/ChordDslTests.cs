namespace GA.Business.DSL.Tests;

using NUnit.Framework;
using Services;

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
    // The whole symbol must be understood: a parser that stops early returns a different chord
    // ("C7sus4" read as C7, "Cm(maj7)" as Cm) instead of an error.
    [TestCase("C7sus4")]
    [TestCase("Cm(maj7)")]
    [TestCase("Cø7")]
    [TestCase("G7 foo")]
    public void Parse_RejectsUnparsedTrailingInput(string input)
    {
        var result = _service.Parse(input);
        Assert.That(result.IsError, Is.True, result.IsOk ? $"Parsed as {_service.Render(result.ResultValue)}" : "");
    }

    [Test]
    public void Parse_AllowsTrailingWhitespace()
    {
        var result = _service.Parse("Am7 ");
        Assert.That(result.IsOk, Is.True, result.IsError ? result.ErrorValue : "");
        Assert.That(_service.Render(result.ResultValue), Is.EqualTo("Am7"));
    }
}
