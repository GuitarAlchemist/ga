namespace GaMcpServer.Tests;

using System.Text.Json;
using GaMcpServer.Tools;
using NUnit.Framework;

[TestFixture]
public sealed class GuitaristProblemToolsTests
{
    private class SuggestionResult
    {
        public string Key { get; set; } = "";
        public List<SuggestionItem> Suggestions { get; set; } = new();
    }

    private class SuggestionItem
    {
        public string Chord { get; set; } = "";
        public string ScaleDegree { get; set; } = "";
        public string Arpeggio { get; set; } = "";
        public string Mode { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    [Test]
    public async Task GaArpeggioSuggestions_DiatonicProgression_ReturnsCorrectArpeggiosAndModes()
    {
        // 1. Amm7 — root + full-suffix concatenation bug must be fixed.
        // For Am, it must return "Am", not "Amm7" or similar.
        var chords = new[] { "Am", "F", "C", "G" };
        var json = await GaArpeggioSuggestionsTool.GaArpeggioSuggestions(chords, "C major");

        var result = JsonSerializer.Deserialize<SuggestionResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Key, Is.EqualTo("C major"));
        Assert.That(result.Suggestions, Has.Count.EqualTo(4));

        var amItem = result.Suggestions[0];
        Assert.That(amItem.Chord, Is.EqualTo("Am"));
        Assert.That(amItem.ScaleDegree, Is.EqualTo("vi"));
        Assert.That(amItem.Arpeggio, Is.EqualTo("Am"));
        Assert.That(amItem.Mode, Is.EqualTo("Aeolian (minor)"));

        var fItem = result.Suggestions[1];
        Assert.That(fItem.Chord, Is.EqualTo("F"));
        Assert.That(fItem.ScaleDegree, Is.EqualTo("IV"));
        Assert.That(fItem.Arpeggio, Is.EqualTo("F"));
        Assert.That(fItem.Mode, Is.EqualTo("Lydian"));
    }

    [Test]
    public async Task GaArpeggioSuggestions_BorrowedChords_ClassifiedByWrittenQuality()
    {
        // 2. Key-blind degree mapping — wrong for borrowed / secondary chords must be fixed.
        // Feed an A major chord in C major and it should NOT report Aeolian (with m3, putting a natural C against C#),
        // it should report Ionian or Mixolydian (secondary dominant) and suggest A / A7 with M3.
        var chords = new[] { "C", "A", "Dm", "G" };
        var json = await GaArpeggioSuggestionsTool.GaArpeggioSuggestions(chords, "C major");

        var result = JsonSerializer.Deserialize<SuggestionResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(result, Is.Not.Null);
        var aItem = result.Suggestions[1];
        Assert.That(aItem.Chord, Is.EqualTo("A"));
        Assert.That(aItem.ScaleDegree, Is.EqualTo("vi"));
        Assert.That(aItem.Arpeggio, Is.EqualTo("A"));
        Assert.That(aItem.Mode, Is.EqualTo("Ionian (major)"));
        Assert.That(aItem.Notes, Does.Contain("M3")); // Major 3rd (C# relative to A), NOT minor 3rd (m3)!
        Assert.That(aItem.Notes, Does.Not.Contain("m3"));
    }

    [Test]
    public async Task GaArpeggioSuggestions_SecondaryDominant_ClassifiedByWrittenQuality()
    {
        var chords = new[] { "C", "A7", "Dm", "G7" };
        var json = await GaArpeggioSuggestionsTool.GaArpeggioSuggestions(chords, "C major");

        var result = JsonSerializer.Deserialize<SuggestionResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(result, Is.Not.Null);
        var a7Item = result.Suggestions[1];
        Assert.That(a7Item.Chord, Is.EqualTo("A7"));
        Assert.That(a7Item.ScaleDegree, Is.EqualTo("vi"));
        Assert.That(a7Item.Arpeggio, Is.EqualTo("A7"));
        Assert.That(a7Item.Mode, Is.EqualTo("Mixolydian"));
        Assert.That(a7Item.Notes, Does.Contain("M3")); // Major 3rd (C# relative to A), NOT minor 3rd (m3)!
        Assert.That(a7Item.Notes, Does.Not.Contain("m3"));
    }
}
