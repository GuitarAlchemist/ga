namespace GA.Business.ML.Tests;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Tests for the shared <see cref="FretDiagram"/> parser — the seam both FretSpanMcpTools and
///     FretSpanSkill now cross (/improve-codebase-architecture candidate #3). The headline cases are
///     the compact diagrams whose leading character is not <c>x</c>: the skill's old regex rejected
///     them while the MCP tool accepted them, so the two adapters disagreed.
/// </summary>
[TestFixture]
public class FretDiagramTests
{
    // Common voicings the skill's old \b[xX]\d{5}\b regex wrongly rejected.
    [TestCase("032010", new[] { 0, 3, 2, 0, 1, 0 }, TestName = "GMajor_032010")]
    [TestCase("133211", new[] { 1, 3, 3, 2, 1, 1 }, TestName = "FBarre_133211")]
    [TestCase("355463", new[] { 3, 5, 5, 4, 6, 3 }, TestName = "AbBarre_355463")]
    [TestCase("x32010", new[] { -1, 3, 2, 0, 1, 0 }, TestName = "CMajor_x32010")]
    public void TryParseFrets_CompactForm_Parses(string diagram, int[] expected)
    {
        Assert.That(FretDiagram.TryParseFrets(diagram), Is.EqualTo(expected));
    }

    [Test]
    public void TryParseFrets_DashForm_ParsesMutesAndTwoDigitFrets()
    {
        Assert.That(FretDiagram.TryParseFrets("x-10-12-12-11-x"),
            Is.EqualTo(new[] { -1, 10, 12, 12, 11, -1 }));
    }

    [Test]
    public void TryParseFrets_NotADiagram_ReturnsNull()
    {
        Assert.That(FretDiagram.TryParseFrets("how do I play a barre chord"), Is.Null);
    }

    [TestCase("032010")]
    [TestCase("what's the span of 355463")]
    public void Contains_DetectsLeadingNonXCompactDiagrams(string message)
    {
        Assert.That(FretDiagram.Contains(message), Is.True);
    }

    // Regression: the skill used to reject leading-non-x compact diagrams in CanHandle.
    [Test]
    public void FretSpanSkill_NowHandles_LeadingNonXCompactDiagram()
    {
        var skill = new FretSpanSkill(NullLogger<FretSpanSkill>.Instance);
        Assert.That(skill.CanHandle("what is the fret span of 032010?"), Is.True);
    }
}
