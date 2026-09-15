namespace GaMcpServer.Tests;

using System.Text.Json;
using System.Threading.Tasks;
using GaMcpServer.Tools;
using NUnit.Framework;

[TestFixture]
public sealed class GuitaristProblemToolsTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Force F# closure module initializers to run before MCP tools are called.
        GA.Business.DSL.GaClosureBootstrap.init();
    }

    [Test]
    public void GaKeyFromProgression_TextbookCadences_ReturnsExpectedBestGuess()
    {
        var testCases = new[]
        {
            (Chords: new[] { "Dm7", "G7" },         ExpectedKey: "C major"),
            (Chords: new[] { "Dm7", "G7", "Cmaj7" }, ExpectedKey: "C major"),
            (Chords: new[] { "Am7", "D7", "Gmaj7" }, ExpectedKey: "G major"),
            (Chords: new[] { "Em7", "A7", "Dmaj7" }, ExpectedKey: "D major"),
            (Chords: new[] { "Dm", "G" },           ExpectedKey: "C major"),
            (Chords: new[] { "G7", "C" },           ExpectedKey: "C major"),
            (Chords: new[] { "F", "G", "C" },       ExpectedKey: "C major"),
            (Chords: new[] { "C", "Am", "F" },       ExpectedKey: "C major")
        };

        foreach (var tc in testCases)
        {
            var json = GaKeyFromProgressionTool.GaKeyFromProgression(tc.Chords);
            using var doc = JsonDocument.Parse(json);
            var bestGuess = doc.RootElement.GetProperty("bestGuess").GetString();

            Assert.That(bestGuess, Is.EqualTo(tc.ExpectedKey),
                $"Chords [{string.Join(", ", tc.Chords)}] should have resolved to best guess key '{tc.ExpectedKey}' but got '{bestGuess}'");
        }
    }

    [Test]
    public async Task GaAnalyzeProgression_TextbookCadences_ReturnsExpectedKey()
    {
        var testCases = new[]
        {
            (Progression: "Dm7 G7",         ExpectedKey: "C major"),
            (Progression: "Dm7 G7 Cmaj7",   ExpectedKey: "C major"),
            (Progression: "Am7 D7 Gmaj7",   ExpectedKey: "G major"),
            (Progression: "Em7 A7 Dmaj7",   ExpectedKey: "D major"),
            (Progression: "Dm G",           ExpectedKey: "C major"),
            (Progression: "G7 C",           ExpectedKey: "C major"),
            (Progression: "F G C",           ExpectedKey: "C major"),
            (Progression: "C Am F",         ExpectedKey: "C major")
        };

        foreach (var tc in testCases)
        {
            var analysis = await GaDslTool.GaAnalyzeProgression(tc.Progression);

            Assert.That(analysis, Does.Contain($"Key: {tc.ExpectedKey}"),
                $"Progression '{tc.Progression}' should contain 'Key: {tc.ExpectedKey}' in analysis. Analysis:\n{analysis}");
        }
    }

    [Test]
    public async Task GaAnalyzeProgression_G7_C_LabelsG7AsV_And_C_AsI()
    {
        var analysis = await GaDslTool.GaAnalyzeProgression("G7 C");
        var lines = analysis.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chordLineIndex = Array.FindIndex(lines, line => line.StartsWith("G7", StringComparison.Ordinal));
        Assert.That(chordLineIndex, Is.GreaterThanOrEqualTo(0), "Should contain a G7/C chord line");

        var chords = lines[chordLineIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var functions = lines[chordLineIndex + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mappings = chords.Zip(functions).Select(pair => $"{pair.First}->{pair.Second}");

        Assert.Multiple(() =>
        {
            Assert.That(analysis, Does.Contain("Key: C major"), "Should identify C major");
            Assert.That(mappings, Is.EqualTo(new[] { "G7->V", "C->I" }));
        });
    }
}
