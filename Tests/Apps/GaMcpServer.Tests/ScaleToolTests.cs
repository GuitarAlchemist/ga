namespace GaMcpServer.Tests;

using GaMcpServer.Tools;

[TestFixture]
public sealed class ScaleToolTests
{
    [Test]
    public void GaScaleById_PrintsOptionalFieldsWithoutFSharpOptionWrappers()
    {
        var result = ScaleTool.GaScaleById(2741);

        Assert.That(result, Does.Not.Contain("Some("));
        Assert.That(result, Contains.Substring("Name: Major"));
        Assert.That(result, Contains.Substring("Category: Western"));
        // Scales.yaml has no ForteNumber for Major: computed from the pitch-class set.
        Assert.That(result, Contains.Substring("Forte Number: 7-35"));
    }

    [TestCase(1365, "6-35")] // whole tone
    [TestCase(1755, "8-28")] // half-whole diminished
    [TestCase(1193, "5-35")] // minor pentatonic
    public void GaScaleById_ComputesTheForteNumber(int id, string forte)
    {
        Assert.That(ScaleTool.GaScaleById(id), Contains.Substring($"Forte Number: {forte}"));
    }

    [Test]
    public void GaScaleById_Description_QuotesIdsThatExist()
    {
        var description = typeof(ScaleTool).GetMethod(nameof(ScaleTool.GaScaleById))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single().Description;

        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(description, @"([A-Za-z -]+)=(\d+)"))
        {
            var result = ScaleTool.GaScaleById(int.Parse(m.Groups[2].Value));
            Assert.That(result, Does.Not.StartWith("No scale found"), m.Value);
        }
    }

    [Test]
    public void GaScaleByName_PrintsOptionalFieldsWithoutFSharpOptionWrappers()
    {
        var result = ScaleTool.GaScaleByName("Ionian");

        Assert.That(result, Does.Not.Contain("Some("));
        Assert.That(result, Contains.Substring("Name: Major"));
    }

    [TestCase("Dorian", "C D Eb F G A Bb", 1709)]
    [TestCase("lydian", "C D E F# G A B", 2773)]
    public void GaScaleByName_FindsModes(string name, string notes, int id)
    {
        var result = ScaleTool.GaScaleByName(name);

        Assert.That(result, Does.Not.StartWith("No scale found"));
        Assert.That(result, Contains.Substring($"Notes: {notes}"));
        Assert.That(result, Contains.Substring($"Binary Scale ID: {id}"));
        Assert.That(result, Contains.Substring("Forte Number: 7-35"));
        Assert.That(result, Does.Not.Contain("Some("));
    }

    [Test]
    public void GaScaleByName_UnknownName_StillReportsNotFound()
    {
        Assert.That(ScaleTool.GaScaleByName("Nonexistent"), Does.StartWith("No scale found"));
    }
}
