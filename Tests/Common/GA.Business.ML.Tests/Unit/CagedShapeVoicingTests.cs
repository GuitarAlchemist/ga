namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// "Where do I play a C major barre chord using the A shape?" gets the A-shape barre chord
/// (<c>x-3-5-5-5-3</c>). On 2026-10-03 it went to the OPTIC-K search with tags [the] and came back
/// with ten Cadd9 shapes. Every diagram here is decoded in standard tuning, independently of the
/// checks inside <see cref="CagedShapeVoicing.Build"/>.
/// </summary>
[TestFixture]
public class CagedShapeVoicingTests
{
    private const string QaQuestion = "where do I play a C major barre chord using the A shape?";
    private static readonly int[] StandardTuning = [4, 9, 2, 7, 11, 4]; // E A D G B E, low E first

    private static readonly string[] Roots =
        ["C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B", "Cb", "E#", "Fb", "B#"];

    private static IEnumerable<TestCaseData> AllShapes() =>
        from root in Roots
        from shape in "CAGED"
        from minor in new[] { false, true }
        select new TestCaseData(root, minor, shape).SetName($"{root}{(minor ? "m" : "")}_{shape}_shape");

    [TestCaseSource(nameof(AllShapes))]
    public void EveryShape_SoundsTheTriad_WithTheRootInTheBass(string root, bool minor, char shape)
    {
        var result = CagedShapeVoicing.Build(root, minor, shape);

        var frets = result.Diagram.Split('-').Select(f => f == "x" ? (int?)null : int.Parse(f)).ToArray();
        var sounding = frets.Select((f, s) => f is { } fret ? (StandardTuning[s] + fret) % 12 : (int?)null)
            .Where(pc => pc.HasValue).Select(pc => pc!.Value).ToList();
        var rootPc = PitchClass(root);
        var triad = new[] { rootPc, (rootPc + (minor ? 3 : 4)) % 12, (rootPc + 7) % 12 };

        Assert.Multiple(() =>
        {
            Assert.That(frets, Has.Length.EqualTo(6));
            Assert.That(frets.Where(f => f.HasValue), Has.All.InRange(0, 15));
            Assert.That(sounding.Distinct(), Is.EquivalentTo(triad), result.Diagram);
            Assert.That(sounding[0], Is.EqualTo(rootPc), $"{result.Diagram}: the bass must be the root");
            Assert.That(frets[result.RootString], Is.EqualTo(result.RootFret));
        });
    }

    // An explicit barre request never keeps an open string: the open shapes move up an octave.
    [TestCaseSource(nameof(AllShapes))]
    public void EveryBarreShape_SoundsTheTriad_WithoutAnOpenString(string root, bool minor, char shape)
    {
        var result = CagedShapeVoicing.Build(root, minor, shape, barre: true);

        var frets = result.Diagram.Split('-').Select(f => f == "x" ? (int?)null : int.Parse(f)).ToArray();
        var sounding = frets.Select((f, s) => f is { } fret ? (StandardTuning[s] + fret) % 12 : (int?)null)
            .Where(pc => pc.HasValue).Select(pc => pc!.Value).ToList();
        var rootPc = PitchClass(root);
        var triad = new[] { rootPc, (rootPc + (minor ? 3 : 4)) % 12, (rootPc + 7) % 12 };

        Assert.Multiple(() =>
        {
            Assert.That(frets.Where(f => f.HasValue), Has.All.InRange(1, 15), result.Diagram);
            Assert.That(sounding.Distinct(), Is.EquivalentTo(triad), result.Diagram);
            Assert.That(sounding[0], Is.EqualTo(rootPc), $"{result.Diagram}: the bass must be the root");
        });
    }

    [TestCase("C", false, 'A', "x-3-5-5-5-3")]
    [TestCase("C", false, 'E', "8-10-10-9-8-8")]
    [TestCase("C", false, 'C', "x-3-2-0-1-0")]
    [TestCase("C", false, 'G', "8-7-5-5-5-8")]
    [TestCase("C", false, 'D', "x-x-10-12-13-12")]
    [TestCase("G", false, 'E', "3-5-5-4-3-3")]
    [TestCase("E", false, 'E', "0-2-2-1-0-0")]
    [TestCase("D", false, 'C', "x-5-4-2-3-2")]
    [TestCase("Bb", false, 'A', "x-1-3-3-3-1")]
    [TestCase("A", true, 'E', "5-7-7-5-5-5")]
    [TestCase("A", true, 'A', "x-0-2-2-1-0")]
    [TestCase("C", true, 'A', "x-3-5-5-4-3")]
    [TestCase("A", true, 'G', "5-3-2-2-5-5")]
    public void KnownShapes(string root, bool minor, char shape, string diagram) =>
        Assert.That(CagedShapeVoicing.Build(root, minor, shape).Diagram, Is.EqualTo(diagram));

    [Test]
    public void TheQaQuestion_IsCMajorInTheAShape()
    {
        var result = CagedShapeVoicing.TryCreate(QaQuestion);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.ChordName, Is.EqualTo("C major"));
            Assert.That(result.Shape, Is.EqualTo('A'));
            Assert.That(result.Diagram, Is.EqualTo("x-3-5-5-5-3"));
            Assert.That(result.StringNotes, Is.EqualTo(new string?[] { null, "C", "G", "C", "E", "G" }));
        });
    }

    [TestCase("How do I play A minor using the E shape", "A minor", 'E')]
    [TestCase("Play G in the C shape", "G major", 'C')]
    [TestCase("A C major chord in the A-shape", "C major", 'A')]
    [TestCase("Ebm barre chord, D shape", "Eb minor", 'D')]
    [TestCase("F# major in the G shape please", "F# major", 'G')]
    [TestCase("B flat major in the A shape", "Bb major", 'A')]
    [TestCase("E-flat in the C shape", "Eb major", 'C')]
    public void Parses(string question, string chord, char shape)
    {
        var result = CagedShapeVoicing.TryCreate(question);

        Assert.That(result, Is.Not.Null, question);
        Assert.That((result!.ChordName, result.Shape), Is.EqualTo((chord, shape)));
    }

    // "barre" asks for the closed form: the open E and A chords move to the 12th fret.
    [TestCase("E major barre chord using the E shape", "12-14-14-13-12-12")]
    [TestCase("A major barre chord using the A shape", "x-12-14-14-14-12")]
    [TestCase("Am barre chord in the A shape", "x-12-14-14-13-12")]
    [TestCase("D major as a bar chord in the D shape", "x-x-12-14-15-14")]
    [TestCase("E major in the E shape", "0-2-2-1-0-0")]
    [TestCase(QaQuestion, "x-3-5-5-5-3")]
    public void ExplicitBarre_SelectsTheClosedForm(string question, string diagram) =>
        Assert.That(CagedShapeVoicing.TryCreate(question)?.Diagram, Is.EqualTo(diagram));

    [TestCase("Cmaj7 in the A shape")]
    [TestCase("C major 7 in the A shape")]
    [TestCase("C minor seventh in the E shape")]
    [TestCase("C maj7 in the A shape")]
    [TestCase("C sus4 in the A shape")]
    [TestCase("C dominant 7 in the E shape")]
    [TestCase("C/G in the A shape")]
    [TestCase("Show me the E shape barre chord")]
    [TestCase("Cmaj7 drop2 jazz voicings")]
    [TestCase("what does a shape mean in CAGED")]
    public void NotATriadInANamedShape_ReturnsNull(string question) =>
        Assert.That(CagedShapeVoicing.TryCreate(question), Is.Null);

    [Test]
    public async Task VoicingAgent_AnswersFromTheTemplate_WithoutSearching()
    {
        // Strict mock: any call to the extractor (the search path's first step) fails the test.
        var extractor = new Mock<IMusicalQueryExtractor>(MockBehavior.Strict);
        var agent = new VoicingAgent(new Mock<IChatClient>(MockBehavior.Strict).Object, NullLogger<VoicingAgent>.Instance,
            null!, extractor.Object, null!);

        var response = await agent.ProcessAsync(new AgentRequest { Query = QaQuestion });

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.StartWith("**C major, A shape**: `x-3-5-5-5-3` (low E to high E), root on the 5th string, 3rd fret."));
            Assert.That(response.Result, Does.Contain("```vextab"));
            Assert.That(response.Result, Does.Contain("Notes, low to high: C G C E G."));
            Assert.That(response.Result, Does.Not.Contain("Cadd9"));
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
        });
    }

    private static int PitchClass(string name)
    {
        var pc = "C D EF G A B".IndexOf(name[0]);
        foreach (var c in name.Skip(1)) pc += c == '#' ? 1 : -1;
        return (pc % 12 + 12) % 12;
    }
}
