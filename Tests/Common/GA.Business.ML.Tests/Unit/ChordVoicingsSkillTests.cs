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
    // Valid bare-extension chords must still route after the ga#261 regex
    // tightening (power chord, 6th, 13th — all real chord-extension digits).
    [TestCase("voicings for E5")]
    [TestCase("shapes for C6")]
    [TestCase("G13 voicings")]
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

    // ga#261: letter+digit tokens that are NOT chords (vitamin, paper size,
    // battery, PowerShell version) collided with the old bare-`\d` regex branch.
    // "what shape is the B12 molecule" is the worst case — it carries a real
    // voicing keyword ("shape") AND a chord-LOOKING token ("B12"), so only
    // tightening the token regex (12/4 aren't valid chord extensions) rejects
    // it. The rest are guarded by the keyword gate but pinned here as
    // regression cases so a future regex loosening can't silently reintroduce them.
    [TestCase("what shape is the B12 molecule")]
    [TestCase("I need 8 hours of focus on G7 problems")]
    [TestCase("buy some D5 batteries")]
    [TestCase("that's an A4 paper-size question")]
    [TestCase("the E5 PowerShell module")]
    public void CanHandle_False_OnNonChordLetterDigitTokens(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"letter+digit token is not a chord and should not be routed here: {message}");
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
