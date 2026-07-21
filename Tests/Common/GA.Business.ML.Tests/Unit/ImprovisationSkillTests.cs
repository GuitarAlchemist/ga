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
    // Valid bare-extension chord must still route after the ga#261 tightening.
    [TestCase("what scale over G13")]
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

    // ga#261: letter+digit tokens that are NOT chords must not route here.
    // "solo over the B12 molecule" / "solo for 8 hours on G7 highway" carry a
    // real improv keyword AND a chord-LOOKING token, so the token regex itself
    // has to reject the non-extension digits (12/4). The keyword-less variants
    // are guarded by the intent gate but pinned as regression cases.
    [TestCase("solo over the B12 molecule")]
    [TestCase("I want to solo for 8 hours on the A4 highway")]
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
    // Cdim is the triad, NOT the 7th chord. Fixed 2026-05-17 (PR for
    // multi-LLM finding-5): pre-fix returned Diminished7 due to
    // StartsWith("dim") match order. Now returns the dedicated triad class.
    [TestCase("Cdim", "Diminished")]
    [TestCase("C°", "Diminished")]
    [TestCase("Co7", "Diminished7")]
    [TestCase("C°7", "Diminished7")]
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
    public void ExtractRoot_HandlesValidAndPathological(string chord, string expected) => Assert.That(ImprovisationSkill.ExtractRoot(chord), Is.EqualTo(expected));

    [Test]
    public void InferQuality_OnPathologicalSymbol_ReturnsUnknown()
    {
        Assert.That(ImprovisationSkill.InferQuality("").Kind.ToString(), Is.EqualTo("Unknown"));
        Assert.That(ImprovisationSkill.InferQuality("7").Kind.ToString(), Is.EqualTo("Unknown"));
        Assert.That(ImprovisationSkill.InferQuality("#G").Kind.ToString(), Is.EqualTo("Unknown"));
    }

    // ===============================================================
    // v2 (2026-07-20) — progression / arpeggio path.
    // ===============================================================

    // ---------------------------------------------------------------
    // ArpeggioFor — the label must be the CANONICAL chord symbol, never
    // root + full-suffix concatenation. This is the exact defect in the
    // MCP arpeggio tool (GuitaristProblemTools.cs), which built "Am" +
    // "m7" = "Amm7". Pin every quality so it cannot regress here.
    // ---------------------------------------------------------------

    [TestCase("Am", "Am")]      // minor triad — NOT "Amm"
    [TestCase("A", "A")]        // major triad — bare root, no suffix
    [TestCase("Dm7", "Dm7")]    // NOT "Dmm7"
    [TestCase("Cmaj7", "Cmaj7")]
    [TestCase("G7", "G7")]
    [TestCase("Bm7b5", "Bm7b5")]
    [TestCase("Cdim7", "Cdim7")]
    [TestCase("Cdim", "Cdim")]
    [TestCase("Caug", "Caug")]
    [TestCase("CmMaj7", "CmMaj7")]
    [TestCase("C7alt", "C7alt")]
    [TestCase("Cmaj7#11", "Cmaj7#11")]
    public void ArpeggioFor_ReturnsCanonicalSymbol_NotConcatenatedSuffix(string chord, string expected)
    {
        var root = ImprovisationSkill.ExtractRoot(chord);
        var quality = ImprovisationSkill.InferQuality(chord);
        Assert.That(ImprovisationSkill.ArpeggioFor(root, quality), Is.EqualTo(expected),
            $"ArpeggioFor({chord}) must be the canonical chord symbol, not a concatenation");
    }

    // ---------------------------------------------------------------
    // ExtractChordRun — pulls chord symbols out of a progression query
    // in order, ignoring the English words around them.
    // ---------------------------------------------------------------

    [TestCase("which arpeggio fits Am F C G", new[] { "Am", "F", "C", "G" })]
    [TestCase("what arpeggios work over Dm7 G7 Cmaj7", new[] { "Dm7", "G7", "Cmaj7" })]
    [TestCase("arpeggios to solo over Am F C G", new[] { "Am", "F", "C", "G" })]
    [TestCase("how do I improvise over Am F C G", new[] { "Am", "F", "C", "G" })]
    [TestCase("Em C G D", new[] { "Em", "C", "G", "D" })]
    // Single chord — still extracted, but the >=2 gate in ExecuteAsync keeps it
    // on the single-chord path.
    [TestCase("solo over Cmaj7", new[] { "Cmaj7" })]
    public void ExtractChordRun_PullsChordsInOrder(string message, string[] expected)
    {
        var run = ImprovisationSkill.ExtractChordRun(message);
        Assert.That(run, Is.EqualTo(expected).AsCollection,
            $"ExtractChordRun({message}) did not return the expected chord run");
    }

    [Test]
    public void ExtractChordRun_IgnoresLowercaseArticlesAndVerbs()
    {
        // "do", "I", "a" must not be read as D / — / A chords.
        var run = ImprovisationSkill.ExtractChordRun("how do I improvise over a Dm7 and a G7");
        Assert.That(run, Is.EqualTo(new[] { "Dm7", "G7" }).AsCollection);
    }

    // ---------------------------------------------------------------
    // CanHandle — progression / arpeggio queries claim the skill.
    // ---------------------------------------------------------------

    [TestCase("which arpeggio fits Am F C G")]
    [TestCase("what arpeggios work over Dm7 G7 Cmaj7")]
    [TestCase("arpeggios to solo over Am F C G")]
    [TestCase("which arpeggio for each chord in Em C G D")]
    public void CanHandle_True_OnProgressionArpeggioQueries(string message)
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle(message), Is.True,
            $"expected ImprovisationSkill to claim progression query: {message}");
    }

    // ---------------------------------------------------------------
    // ExecuteAsync — progression path returns one line per chord with the
    // correct (non-concatenated) arpeggio, and never routes through the
    // single-chord extractor (which is null in this fixture — proving the
    // progression branch runs first).
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_Progression_ReturnsPerChordArpeggios()
    {
        var skill = MakeSkill(); // extractor is null! — must not be called
        var response = await skill.ExecuteAsync("which arpeggio fits Am F C G");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("Am"));
            Assert.That(response.Result, Does.Not.Contain("Amm"),
                "the Amm7-class concatenation bug must not appear");
            Assert.That(response.Result, Does.Contain("Aeolian").Or.Contain("Dorian"),
                "each chord should carry a scale suggestion");
            Assert.That(response.Evidence, Has.Count.EqualTo(4),
                "one evidence line per chord in the progression");
        });
    }

    [Test]
    public async Task ExecuteAsync_Progression_ClassifiesBorrowedChordByWrittenQuality()
    {
        // The whole point of the key-agnostic design: an A MAJOR chord in a
        // C-major context is a secondary dominant / borrowed chord. The MCP
        // tool would call it degree vi and hand back A minor. We must classify
        // it as major from what the user wrote.
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync("arpeggios over C A Dm G");

        // "A" (major) must yield the A major arpeggio, not "Am".
        Assert.That(response.Evidence.Any(e => e.StartsWith("A:") && !e.StartsWith("Am")),
            Is.True, $"A major must be classified major, not minor. Evidence: {string.Join(" | ", response.Evidence)}");
    }
}
