namespace GaMcpServer.Tests;

using System.Text.Json;
using GaMcpServer.Tools;
using ModelContextProtocol;

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

    private static string[] Notes(string json) =>
        [.. JsonDocument.Parse(json).RootElement.EnumerateArray().Select(e => e.GetProperty("note").GetString()!)];

    [TestCase("F major", "F G A Bb C D E")]
    [TestCase("Bb major", "Bb C D Eb F G A")]
    [TestCase("C# major", "C# D# E# F# G# A# B#")]
    [TestCase("Eb minor", "Eb F Gb Ab Bb Cb Db")]
    [TestCase("A minor", "A B C D E F G")]
    [TestCase("D dorian", "D E F G A B C")]
    [TestCase("E phrygian", "E F G A B C D")]
    [TestCase("F lydian", "F G A B C D E")]
    [TestCase("G mixolydian", "G A B C D E F")]
    [TestCase("A aeolian", "A B C D E F G")]
    [TestCase("B locrian", "B C D E F G A")]
    [TestCase("C natural minor", "C D Eb F G Ab Bb")]
    public void GetScaleNotes_SpellsOneLetterPerDegree(string key, string expected)
    {
        Assert.That(string.Join(" ", Notes(ScaleTool.GetScaleNotes(key))), Is.EqualTo(expected));
    }

    [Test]
    public void GetScaleNotes_PitchClassesMatchTheSpelling()
    {
        var json = JsonDocument.Parse(ScaleTool.GetScaleNotes("Bb major")).RootElement;
        int[] pcs = [.. json.EnumerateArray().Select(e => e.GetProperty("pitchClass").GetInt32())];
        Assert.That(pcs, Is.EqualTo(new[] { 10, 0, 2, 3, 5, 7, 9 }));
    }

    [Test]
    public void GetScaleNotes_AgreesWithGetKeyNotesForEveryKey()
    {
        foreach (var keyName in KeyTool.GetAllKeys())
        {
            // "Key of Bb" -> "Bb major", "Key of F#m" -> "F# minor"
            var symbol = keyName["Key of ".Length..];
            var key = symbol.EndsWith('m') ? $"{symbol[..^1]} minor" : $"{symbol} major";
            Assert.That(Notes(ScaleTool.GetScaleNotes(key)), Is.EqualTo(KeyTool.GetKeyNotes(keyName).ToArray()), key);
        }
    }

    [TestCase("H major")]
    [TestCase("C")]
    [TestCase("C minor pentatonic")]
    [TestCase("C blues")]
    public void GetScaleNotes_RejectsWhatItCannotSpell(string key)
    {
        Assert.Throws<McpException>(() => ScaleTool.GetScaleNotes(key));
    }

    [Test]
    public async Task GetScaleNotes_ErrorsReachTheClientWithIsError()
    {
        var (isError, text) = await McpToolInvoker.CallAsync(
            typeof(ScaleTool).GetMethod(nameof(ScaleTool.GetScaleNotes))!, null, ("key", "H major"));

        Assert.That(isError, Is.True);
        Assert.That(text, Contains.Substring("Unknown root note 'H'"));
    }
}
