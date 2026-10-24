namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Instruments.Primitives;
using Domain.Services.Fretboard.Voicings.Analysis;
using Domain.Services.Fretboard.Voicings.Generation;

/// <summary>
///     CAGED shapes of voicings as GA generates them. A voicing stores string 1 (high E) first, so its
///     diagram "1-1-2-3-3-1" is the chart guitarists write low E first, "133211". The analyzer used to
///     read the first entry as string 6: it never recognised a real E shape, tagged 92 other voicings
///     "E-Shape", and could not reach its A- and C-shape checks at all.
/// </summary>
[TestFixture]
[Category("Unit")]
public class VoicingCagedShapeTests
{
    private static readonly Dictionary<string, Voicing> _byDiagram = [];

    private static readonly string[] _diagrams =
    [
        "0-0-1-2-2-0", "1-1-2-3-3-1", "0-2-2-2-0-x", "1-3-3-3-1-x", "0-1-0-2-3-x", "0-2-2-1-x-x"
    ];

    [OneTimeSetUp]
    public async Task GenerateTheVoicings()
    {
        // The shapes sit in the first frets, and the generator emits its windows from the nut up
        await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(Fretboard.Default, parallel: false))
        {
            if (_diagrams.Contains(voicing.Diagram)) _byDiagram[voicing.Diagram] = voicing;
            if (_byDiagram.Count == _diagrams.Length) break;
        }
    }

    [Test]
    public void GeneratedVoicing_StoresStringOneFirst()
    {
        // The premise of the cases below: open E's first position is string 1, high E (MIDI 64)
        var first = (Position.Played)_byDiagram["0-0-1-2-2-0"].Positions[0];

        Assert.Multiple(() =>
        {
            Assert.That(first.Location.Str.Value, Is.EqualTo(1));
            Assert.That(first.MidiNote.Value, Is.EqualTo(64));
        });
    }

    [TestCase("0-0-1-2-2-0", "E-Shape", TestName = "Open E (022100) is the E shape")]
    [TestCase("1-1-2-3-3-1", "E-Shape", TestName = "F barre (133211) is the E shape")]
    [TestCase("0-2-2-2-0-x", "A-Shape", TestName = "Open A (x02220) is the A shape")]
    [TestCase("1-3-3-3-1-x", "A-Shape", TestName = "Bb barre (x13331) is the A shape")]
    [TestCase("0-1-0-2-3-x", "C-Shape", TestName = "Open C (x32010) is the C shape")]
    [TestCase("0-2-2-1-x-x", null, TestName = "xx1220 is no CAGED shape")]
    public void CagedShape_ReadsTheStringsInTheOrderTheVoicingStoresThem(string diagram, string? expected)
    {
        Assert.That(_byDiagram, Contains.Key(diagram), "the generator should produce this voicing");

        var layout = VoicingPhysicalAnalyzer.ExtractPhysicalLayout(_byDiagram[diagram]);

        Assert.That(VoicingPhysicalAnalyzer.CalculatePlayability(layout).CagedShape, Is.EqualTo(expected), diagram);
    }
}
