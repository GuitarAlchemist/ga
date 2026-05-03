namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class BeginnerChordsSkillTests
{
    private static BeginnerChordsSkill MakeSkill() => new(NullLogger<BeginnerChordsSkill>.Instance);

    [Test]
    public void Metadata_ExposesExamplePrompts()
    {
        var skill = MakeSkill();
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(3),
            "skill must expose enough example prompts for the SemanticIntentRouter");
        Assert.That(skill.Description, Does.Contain("beginner").IgnoreCase
            .Or.Contain("open").IgnoreCase);
    }

    [Test]
    public void CanHandle_AlwaysFalse_DispatchedSemantically()
    {
        // The skill is registered for SemanticIntentRouter dispatch only —
        // CanHandle stays false so it doesn't fire on incidental keyword matches
        // in the legacy fallback path.
        var skill = MakeSkill();
        Assert.That(skill.CanHandle("show me some easy beginner chords"), Is.False);
        Assert.That(skill.CanHandle("anything"), Is.False);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsAllEightChords()
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync("show me some easy beginner chords");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        // The eight chords every curriculum lists first.
        var expected = new[] { "C major", "G major", "D major", "A major", "E major", "A minor", "E minor", "D minor" };
        foreach (var chord in expected)
        {
            Assert.That(response.Result, Does.Contain(chord),
                $"expected '{chord}' in the result");
        }
    }

    [Test]
    public async Task ExecuteAsync_DiagramFormatIsValidLowToHigh()
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync("show me some easy beginner chords");

        // Spot-check a few canonical diagrams. Format: low-E A D G B high-e.
        // C major: x-3-2-0-1-0
        Assert.That(response.Result, Does.Contain("x-3-2-0-1-0"));
        // E major: 0-2-2-1-0-0 — the fullest open chord
        Assert.That(response.Result, Does.Contain("0-2-2-1-0-0"));
        // E minor: 0-2-2-0-0-0 — the easiest chord
        Assert.That(response.Result, Does.Contain("0-2-2-0-0-0"));
    }

    [Test]
    public async Task ExecuteAsync_EvidenceListsEveryChord()
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync("show me some easy beginner chords");

        Assert.That(response.Evidence, Has.Count.GreaterThanOrEqualTo(8),
            "evidence should include one entry per catalog chord plus the catalog-size summary");
        Assert.That(response.Evidence, Has.Some.Contains("C major: x-3-2-0-1-0"));
    }
}
