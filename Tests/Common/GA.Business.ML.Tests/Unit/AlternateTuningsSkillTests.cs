namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class AlternateTuningsSkillTests
{
    private static AlternateTuningsSkill MakeSkill() =>
        new(NullLogger<AlternateTuningsSkill>.Instance);

    private static async Task<string> AnswerAsync(string message)
    {
        var response = await MakeSkill().ExecuteAsync(message);
        return response.Result;
    }

    [Test]
    public void Metadata_ListsDropC()
    {
        var skill = MakeSkill();
        Assert.Multiple(() =>
        {
            Assert.That(skill.Description.ToLowerInvariant(), Does.Contain("drop-c"));
            Assert.That(skill.ExamplePrompts.Any(p => p.Contains("drop C", StringComparison.OrdinalIgnoreCase)),
                Is.True, "an ExamplePrompt must anchor the drop-C phrasing for the router");
        });
    }

    // ---------------------------------------------------------------
    // Drop C (2026-07-20) — C G C F A D, closing the BACKLOG "how do I
    // tune to drop-C" gap. Named-lookup and reverse (6-note) lookup.
    // ---------------------------------------------------------------

    [TestCase("how do I tune to drop C")]
    [TestCase("drop C tuning notes")]
    [TestCase("what is drop-C tuning")]
    [TestCase("drop c")]
    public async Task DropC_NamedLookup_ReturnsCGCFAD(string message)
    {
        var answer = await AnswerAsync(message);
        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("Drop C"));
            // low → high: C – G – C – F – A – D
            Assert.That(answer, Does.Contain("C – G – C – F – A – D"),
                $"drop-C must present the C G C F A D layout for: {message}");
        });
    }

    [Test]
    public async Task DropC_ReverseLookup_FromSixNotes_NamesDropC()
    {
        var answer = await AnswerAsync("what tuning is C G C F A D");
        Assert.That(answer, Does.Contain("Drop C"),
            "a bare 6-note C G C F A D sequence should resolve to the Drop C name");
    }

    [Test]
    public async Task DropC_LowSixthString_IsMajorThirdBelowStandard()
    {
        // The distinguishing interval: string 6 E→C is -4 semitones (a major
        // third), not the -2 whole step of the other five strings. The delta
        // column must show it, or the answer is musically wrong.
        var answer = await AnswerAsync("how do I tune to drop C");
        Assert.That(answer, Does.Contain("-4st"),
            "string 6 (E→C) must be reported as -4 semitones (major third down)");
    }

    // ---------------------------------------------------------------
    // Guards — drop-C must not collide with its neighbours.
    // ---------------------------------------------------------------

    [Test]
    public async Task DropD_StillResolvesToDropD_NotDropC()
    {
        var answer = await AnswerAsync("what's drop D tuning");
        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("Drop D"));
            Assert.That(answer, Does.Not.Contain("Drop C"));
        });
    }

    [Test]
    public async Task DropCSharp_DoesNotMatchDropC()
    {
        // "drop C#" is a different tuning not in the table. The (?![#b])
        // lookahead must keep it from resolving to plain Drop C — better to
        // decline than to hand back the wrong tuning.
        var answer = await AnswerAsync("how do I tune to drop C#");
        Assert.That(answer, Does.Not.Contain("C – G – C – F – A – D"),
            "drop C# must not be answered as Drop C");
    }
}
