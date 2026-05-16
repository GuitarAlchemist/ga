namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class ChordVoicingsSkillTests
{
    private static ChordVoicingsSkill MakeSkill() =>
        new(NullLogger<ChordVoicingsSkill>.Instance, null!, null!, null!);

    [Test]
    public void Metadata_ExposesExamplePrompts()
    {
        var skill = MakeSkill();
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(8),
            "skill must expose enough example prompts for the SemanticIntentRouter");
        Assert.That(skill.Description.ToLowerInvariant(), Does.Contain("voicing")
            .Or.Contain("shape").Or.Contain("fingering"));
    }

    // ---------------------------------------------------------------
    // CanHandle — positive cases (real voicing-intent queries).
    // ---------------------------------------------------------------

    [TestCase("voicings for Cmaj7")]
    [TestCase("show me Dm7 voicings")]
    [TestCase("shapes for F major")]
    [TestCase("fingerings for G7")]
    [TestCase("drop2 voicings of Cmaj7")]
    [TestCase("shell voicing for Dm7")]
    [TestCase("rootless A7 voicings")]
    [TestCase("barre voicings for Bb major")]
    [TestCase("open chord shape for E minor")]
    [TestCase("show me a shape for G7")]
    [TestCase("Cmaj9 voicings on guitar")]
    [TestCase("F#m fingering")]
    public void CanHandle_True_OnRealVoicingQueries(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.True,
            $"expected ChordVoicingsSkill to claim: {message}");
    }

    // ---------------------------------------------------------------
    // CanHandle — negative cases from the 2026-05-16 multi-LLM review.
    // Each one is a genuine bug or a known-ambiguous boundary.
    // ---------------------------------------------------------------

    // HIGH from /octo-code-reviewer: IgnoreCase made bare "a"/"e" match [A-G]
    // even though English routinely uses lowercase "a"/"e" as articles.
    [TestCase("show me a beginner-friendly shape for the open position")]
    [TestCase("what's a good shape on the fretboard")]
    [TestCase("show me a beginner-friendly shape")]
    [TestCase("explain the open shape system")]
    public void CanHandle_False_OnLowercaseArticleFollowedByShapeKeyword(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"bare lowercase article should not be treated as a chord root in: {message}");
    }

    // No voicing intent — must yield to ChordInfoSkill / ModesSkill / etc.
    [TestCase("give me a Cmaj7 chord")]
    [TestCase("what are the notes in Cmaj7")]
    [TestCase("tell me about a major seventh chord")]
    [TestCase("explain the augmented triad")]
    [TestCase("what is C7b9")]
    public void CanHandle_False_WhenNoVoicingKeyword(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"prompt has no voicing/shape/fingering keyword, should not claim: {message}");
    }

    // English words starting with uppercase A-G that contain quality-shaped
    // substrings — the regex must reject these so noisy chat doesn't trigger.
    [TestCase("Add some shape to this voicing line")]
    [TestCase("Dim the lights and show me a shape")]
    public void CanHandle_False_OnEnglishWordsStartingWithChordLetter(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"English word starting with A-G uppercase shouldn't be parsed as a chord root: {message}");
    }

    [Test]
    public void CanHandle_False_OnEmptyOrWhitespace()
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(""), Is.False);
        Assert.That(skill.CanHandle("   "), Is.False);
        Assert.That(skill.CanHandle(null!), Is.False);
    }
}
