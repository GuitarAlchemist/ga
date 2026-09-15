namespace GA.Business.ML.Tests.Notation;

using GA.Business.ML.Notation;

[TestFixture]
public sealed class PlayableNotationFormatterTests
{
    [TestCase("x-3-2-0-1-0", "5/3 4/2 3/0 2/1 1/0")]
    [TestCase("x32010", "5/3 4/2 3/0 2/1 1/0")]
    [TestCase("0-2-2-1-0-0", "6/0 5/2 4/2 3/1 2/0 1/0")]
    [TestCase("x-x-0-2-3-2", "4/0 3/2 2/3 1/2")]
    public void TryFormatChordDiagramAsVexTab_ConvertsSixStringDiagrams(
        string diagram,
        string expected)
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(diagram);

        Assert.That(actual, Is.EqualTo(expected));
    }

    // Voicing.Diagram / OPTK index order: string 1 (high E) first.
    // 3-0-x-2-3-x is Cmaj7 (A3 = C, D2 = E, B0 = B, e3 = G); 0-1-0-2-3-x is open C.
    [TestCase("3-0-x-2-3-x", "5/3 4/2 2/0 1/3")]
    [TestCase("0-1-0-2-3-x", "5/3 4/2 3/0 2/1 1/0")]
    [TestCase("10-13-10-x-x-x", "3/10 2/13 1/10")]
    public void TryFormatChordDiagramAsVexTab_HighToLowOrder_NumbersStringsFromHighE(
        string diagram,
        string expected)
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(diagram, DiagramStringOrder.HighToLow);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void VoicingAgentResultLine_RendersIndexDiagramInTheRightStringOrder()
    {
        // Course finding: the chatbot rendered the Cmaj7 result `3-0-x-2-3-x` as
        // "6/3 5/0 3/2 2/3" (G A A D) because the host read the index diagram low-E-first.
        var sb = new System.Text.StringBuilder();
        GA.Business.ML.Agents.VoicingAgent.AppendResultLine(sb, "Cmaj7", "3-0-x-2-3-x", null, 0.369);

        var augmented = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(sb.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(augmented.Text, Does.Contain("5/3 4/2 2/0 1/3"));
            Assert.That(augmented.Text, Does.Not.Contain("6/3 5/0 3/2 2/3"));
            Assert.That(augmented.AddedFenceCount, Is.EqualTo(0));
            Assert.That(augmented.Text.Split("```vextab").Length - 1, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryFormatChordDiagramAsMarkdownFence_UsesVexTabFence()
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsMarkdownFence("x-3-2-0-1-0");

        Assert.That(actual, Does.StartWith("```vextab"));
        Assert.That(actual, Does.Contain("5/3 4/2 3/0 2/1 1/0"));
        Assert.That(actual, Does.EndWith("```"));
    }

    [Test]
    public void AugmentMarkdownWithVexTabFences_AddsFenceAfterVoicingLines()
    {
        const string markdown =
            """
            Found 1 voicing:

            - **Dm7(shell)** `10-13-10-x-x-x` (guitar, score 0.594)
            """;

        var actual = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(markdown);

        Assert.Multiple(() =>
        {
            Assert.That(actual.DiagramCount, Is.EqualTo(1));
            Assert.That(actual.AddedFenceCount, Is.EqualTo(1));
            Assert.That(actual.Text, Does.Contain("```vextab"));
            Assert.That(actual.Text, Does.Contain("6/10 5/13 4/10"));
        });
    }

    [Test]
    public void AugmentMarkdownWithVexTabFences_DoesNotDuplicateExistingFence()
    {
        const string markdown =
            """
            - **C** `x-3-2-0-1-0`
            ```vextab
            5/3 4/2 3/0 2/1 1/0
            ```
            """;

        var actual = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(markdown);

        Assert.Multiple(() =>
        {
            Assert.That(actual.DiagramCount, Is.EqualTo(1));
            Assert.That(actual.AddedFenceCount, Is.EqualTo(0));
            Assert.That(actual.Text.Split("```vextab").Length - 1, Is.EqualTo(1));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not a diagram")]
    [TestCase("x-3-2-0-1")]
    [TestCase("x-3-2-0-1-99")]
    public void TryFormatChordDiagramAsVexTab_RejectsUnknownShapes(string? diagram)
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(diagram);

        Assert.That(actual, Is.Null);
    }
}
