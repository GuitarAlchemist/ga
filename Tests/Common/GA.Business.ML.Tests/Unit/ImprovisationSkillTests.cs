namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class ImprovisationSkillTests
{
    private static ImprovisationSkill MakeSkill() =>
        new(NullLogger<ImprovisationSkill>.Instance, null!);

    [Test]
    public void Metadata_ExposesExamplePrompts()
    {
        var skill = MakeSkill();
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(6),
            "skill must expose enough example prompts for the SemanticIntentRouter");
        Assert.That(skill.Description.ToLowerInvariant(), Does.Contain("solo")
            .Or.Contain("improvise").Or.Contain("scale").Or.Contain("mode"));
    }

    // ---------------------------------------------------------------
    // CanHandle — positive cases (real improvisation queries).
    // ---------------------------------------------------------------

    [TestCase("what scale can I use to solo over Cmaj7?")]
    [TestCase("what scales work over G7?")]
    [TestCase("modes to solo over Dm7")]
    [TestCase("improvise over Am7")]
    [TestCase("scale for Fmaj7#11")]
    [TestCase("what to play over a Bm7b5")]
    [TestCase("improvisation choices for E7alt")]
    [TestCase("solo over a Cmaj9 chord")]
    [TestCase("scales for G7b9")]
    [TestCase("what scale over D7sus")]
    [TestCase("modes over F#m7")]
    [TestCase("modal choices for Bbmaj7")]
    // 2026-05-17 post-PR-253 feature-dev review — the two ExamplePrompts
    // that the original keyword list missed in the CanHandle fallback path:
    [TestCase("which mode fits Cmaj7")]
    [TestCase("chord-scale for G7")]
    [TestCase("which mode works over Am7")]
    [TestCase("chord scale options for D7")]
    public void CanHandle_True_OnRealImprovQueries(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.True,
            $"expected ImprovisationSkill to claim: {message}");
    }

    // ---------------------------------------------------------------
    // CanHandle — negative cases. Same regression cases as
    // ChordVoicingsSkill (PR #251) plus improvisation-specific
    // routing collisions.
    // ---------------------------------------------------------------

    [TestCase("show me a beginner-friendly shape for the open position")]
    [TestCase("what's a good shape on the fretboard")]
    [TestCase("explain the open shape system")]
    public void CanHandle_False_OnLowercaseArticleFollowedByOtherKeyword(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"bare lowercase article should not trigger improvisation routing: {message}");
    }

    [TestCase("give me a Cmaj7 chord")]
    [TestCase("what are the notes in Cmaj7")]
    [TestCase("voicings for Cmaj7")]
    [TestCase("show me Dm7 shapes")]
    [TestCase("tell me about a major seventh chord")]
    [TestCase("explain the augmented triad")]
    public void CanHandle_False_WhenNoImprovKeyword(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"prompt has no solo/improvise/scale-over keyword: {message}");
    }

    // Keyword present, no chord token. Should not steal generic improv
    // questions that lack a specific chord (those need different routing).
    [TestCase("how do I improvise in general?")]
    [TestCase("explain improvisation")]
    [TestCase("what does it mean to solo")]
    public void CanHandle_False_OnGenericImprovQuestionWithoutChord(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.False,
            $"no chord token present, should not claim: {message}");
    }

    [Test]
    public void CanHandle_False_OnEmptyOrWhitespace()
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(""), Is.False);
        Assert.That(skill.CanHandle("   "), Is.False);
        Assert.That(skill.CanHandle(null!), Is.False);
    }

    // ---------------------------------------------------------------
    // InferQuality — drive each canonical chord symbol through a
    // [TestCase] table. The multi-LLM review of this PR explicitly
    // flagged that ordering bugs (CmMaj7 → Major7) were invisible
    // from CanHandle alone — these tests pin the classifier directly.
    // ---------------------------------------------------------------

    // Expected QualityKind passed as string (NUnit analyzer NUnit1026 rejects
    // internal test methods, and we don't want to widen QualityKind to public
    // just for tests — string-at-the-boundary keeps the API surface tight).
    [TestCase("Cmaj7", "Major7")]
    [TestCase("Cmaj9", "Major7")]
    [TestCase("Cmaj13", "Major7")]
    [TestCase("Cmaj7#11", "LydianMaj7")]
    [TestCase("CmMaj7", "MinorMajor7")]
    [TestCase("CminMaj7", "MinorMajor7")]
    [TestCase("Cm(maj7)", "MinorMajor7")]
    [TestCase("Cm7", "Minor7")]
    [TestCase("Cm9", "Minor7")]
    [TestCase("Cm11", "Minor7")]
    [TestCase("Cm6", "MinorMajor7")]
    [TestCase("C7", "Dominant7")]
    [TestCase("C9", "Dominant7")]
    [TestCase("C13", "Dominant7")]
    [TestCase("C7b9", "AlteredDominant")]
    [TestCase("C7#9", "AlteredDominant")]
    [TestCase("C7b5", "AlteredDominant")]
    [TestCase("C7(b9)", "AlteredDominant")]
    [TestCase("Am7(b5)", "HalfDiminished")]
    [TestCase("Cm7b5", "HalfDiminished")]
    [TestCase("Cdim7", "Diminished7")]
    [TestCase("Cdim", "Diminished7")]
    [TestCase("Caug", "Augmented")]
    [TestCase("C7#5", "Augmented")]
    [TestCase("C7sus", "SuspendedDominant")]
    [TestCase("C7sus4", "SuspendedDominant")]
    [TestCase("C7alt", "Altered")]
    [TestCase("Calt", "Altered")]
    [TestCase("Cm", "Minor")]
    [TestCase("C", "Major")]
    [TestCase("C6", "Major")]
    [TestCase("F#m7", "Minor7")]
    [TestCase("Bbmaj7", "Major7")]
    public void InferQuality_ClassifiesCanonicalChordSymbols(string chord, string expectedKind)
    {
        var result = ImprovisationSkill.InferQuality(chord);
        Assert.That(result.Kind.ToString(), Is.EqualTo(expectedKind),
            $"InferQuality({chord}) classified as {result.Kind}; expected {expectedKind}");
    }

    // ---------------------------------------------------------------
    // ExtractRoot — handles accidentals + validates [A-G] root.
    // ---------------------------------------------------------------

    [TestCase("C", "C")]
    [TestCase("Cmaj7", "C")]
    [TestCase("F#", "F#")]
    [TestCase("Bb", "Bb")]
    [TestCase("F#m7", "F#")]
    [TestCase("Bbmaj7", "Bb")]
    [TestCase("", "C")] // empty fallback
    [TestCase("7", "C")] // pathological parser output → safe fallback
    [TestCase("#G", "C")] // accidental-prefix → fallback
    public void ExtractRoot_HandlesValidAndPathological(string chord, string expected)
    {
        Assert.That(ImprovisationSkill.ExtractRoot(chord), Is.EqualTo(expected));
    }

    [Test]
    public void InferQuality_OnPathologicalSymbol_ReturnsUnknown()
    {
        Assert.That(ImprovisationSkill.InferQuality("").Kind.ToString(), Is.EqualTo("Unknown"));
        Assert.That(ImprovisationSkill.InferQuality("7").Kind.ToString(), Is.EqualTo("Unknown"));
        Assert.That(ImprovisationSkill.InferQuality("#G").Kind.ToString(), Is.EqualTo("Unknown"));
    }
}
