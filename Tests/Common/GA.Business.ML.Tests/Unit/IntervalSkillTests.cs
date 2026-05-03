namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class IntervalSkillTests
{
    private static IntervalSkill MakeSkill() =>
        new(NullLogger<IntervalSkill>.Instance);

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [TestCase("what is the interval between C and G")]
    [TestCase("Interval from C to G please")]
    [TestCase("distance from F# to D")]
    [TestCase("the interval C to G")]
    [TestCase("Distance between A and E")]
    public void CanHandle_PositiveCases_ReturnsTrue(string prompt)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(prompt), Is.True, prompt);
    }

    [TestCase("what is a C major chord")]
    [TestCase("show me a C scale")]
    [TestCase("C and G")]                          // no interval/distance keyword
    [TestCase("interval question without notes")]  // keyword but no note pair
    [TestCase("")]
    [TestCase("   ")]
    public void CanHandle_NegativeCases_ReturnsFalse(string prompt)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(prompt), Is.False, prompt);
    }

    [Test]
    public void CanHandle_Null_ReturnsFalse()
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(null!), Is.False);
    }

    // ── ExecuteAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_C_To_G_ReturnsPerfectFifth()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("what is the interval between C and G");

        Assert.Multiple(() =>
        {
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result, Does.Contain("C"));
            Assert.That(response.Result, Does.Contain("G"));
            Assert.That(response.Result, Does.Contain("perfect fifth"));
            Assert.That(response.Result, Does.Contain("P5"));
            Assert.That(response.Result, Does.Contain("7 semitones"));
        });
    }

    [Test]
    public async Task ExecuteAsync_C_To_E_ReturnsMajorThird()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("interval from C to E");

        Assert.That(response.Result, Does.Contain("major third"));
        Assert.That(response.Result, Does.Contain("4 semitones"));
    }

    [Test]
    public async Task ExecuteAsync_C_To_Eb_ReturnsMinorThird()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("interval from C to Eb");

        Assert.That(response.Result, Does.Contain("minor third"));
        Assert.That(response.Result, Does.Contain("3 semitones"));
    }

    [Test]
    public async Task ExecuteAsync_FSharp_To_C_ReturnsTritone()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("distance from F# to C");

        // F# → C upward is a diminished fifth (6 semitones).
        Assert.That(response.Result, Does.Contain("6 semitones"));
    }

    [Test]
    public async Task ExecuteAsync_UnrecognisedNote_ReturnsCannotHelp()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("interval from H to Q");

        // The pattern won't match H/Q at all (regex is [A-Ga-g][#b]?), so the
        // skill falls through to CannotHelp via the regex no-match path.
        Assert.That(response.Confidence, Is.EqualTo(0.0f));
        Assert.That(response.Result, Does.Contain("note").IgnoreCase
            .Or.Contain("parse").IgnoreCase
            .Or.Contain("recognise").IgnoreCase);
    }

    [Test]
    public async Task ExecuteAsync_EvidenceContainsNotesAndSemitones()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("interval from D to A");

        Assert.That(response.Evidence, Has.Some.Contains("D"));
        Assert.That(response.Evidence, Has.Some.Contains("A"));
        Assert.That(response.Evidence, Has.Some.Contains("Semitones"));
    }
}
