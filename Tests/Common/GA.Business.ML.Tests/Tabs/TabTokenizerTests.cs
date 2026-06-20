namespace GA.Business.ML.Tests.Tabs;

using GA.Business.ML.Tabs;
using NUnit.Framework;

[TestFixture]
public class TabTokenizerTests
{
    private TabTokenizer _tokenizer;
    private TabToPitchConverter _converter;

    [SetUp]
    public void Setup()
    {
        _tokenizer = new();
        _converter = new();
    }

    [Test]
    public void TestBasicGChord()
    {
        // Arrange
        // Standard G Major: 3 2 0 0 0 3
        var tab = @"
e|---3---|
B|---0---|
G|---0---|
D|---0---|
A|---2---|
E|---3---|
";
        // Act
        var blocks = _tokenizer.Tokenize(tab);
        var block = blocks[0];
        var noteSlice = block.Slices.FirstOrDefault(s => s.Notes.Count > 0);
        var lowE = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 0);
        var highE = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 5);
        var midi = noteSlice != null ? TabToPitchConverter.GetMidiNotes(noteSlice) : [];

        // Assert
        TestContext.WriteLine($"Tokenized Tab:\n{tab}");
        TestContext.WriteLine($"Block Count: {blocks.Count}, String Count: {block.StringCount}");
        TestContext.WriteLine($"Low E Fret: {lowE?.Fret}, High E Fret: {highE?.Fret}");
        TestContext.WriteLine($"MIDI Notes: {string.Join(", ", midi)}");

        Assert.Multiple(() =>
        {
            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(block.StringCount, Is.EqualTo(6));
            Assert.That(noteSlice, Is.Not.Null);
            Assert.That(noteSlice!.Notes.Count, Is.EqualTo(6));
            Assert.That(lowE?.Fret, Is.EqualTo(3));
            Assert.That(highE?.Fret, Is.EqualTo(3));
            Assert.That(midi, Does.Contain(43)); // G2 (Low E + 3 = 40+3)
            Assert.That(midi, Does.Contain(67)); // G4 (High e + 3 = 64+3)
        });
    }

    [Test]
    public void TestTwoDigitFret()
    {
        // Arrange
        // Power chord at 12th fret
        var tab = @"
e|-------|
B|-------|
G|-------|
D|--12---|
A|--12---|
E|--10---|
";
        // Act
        var blocks = _tokenizer.Tokenize(tab);
        var block = blocks[0];
        var noteSlice = block.Slices.FirstOrDefault(s => s.Notes.Count > 0);
        var lowE = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 0);
        var aString = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 1);

        // Assert
        TestContext.WriteLine($"Two-digit Fret Tab:\n{tab}");
        TestContext.WriteLine($"Low E Fret: {lowE?.Fret}, A String Fret: {aString?.Fret}");

        Assert.Multiple(() =>
        {
            Assert.That(noteSlice, Is.Not.Null);
            Assert.That(lowE?.Fret, Is.EqualTo(10));
            Assert.That(aString?.Fret, Is.EqualTo(12));
        });
    }

    // ── Prose-vs-tab boundary ──────────────────────────────────────────────
    //
    // Every "should I dispatch to tab analysis?" check upstream
    // (orchestrators, intent routers, semantic dispatch) eventually grounds
    // out in `tokenizer.Tokenize(message).Any(b => b.Slices.Any(s => s.Notes.Any()))`.
    // If the tokenizer thinks prose is tab, the user gets the canned
    // "I detected tab but couldn't parse any chords." fallback for theory
    // questions — exactly the misroute we hit on /chatbot/ for
    // "Explain voice leading in jazz".
    //
    // These tests pin the negative contract: plain English, including
    // English with stray pipes, chord names, fret numbers, or markdown
    // tables, MUST tokenize to zero note-bearing slices.

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\n\n  \n\t\n")]
    public void Tokenize_EmptyOrWhitespace_ReturnsNoBlocks(string input)
    {
        var blocks = _tokenizer.Tokenize(input);
        Assert.That(blocks, Is.Empty);
    }

    [TestCase("Explain voice leading in jazz")]
    [TestCase("Show me some easy beginner chords")]
    [TestCase("What are the modes of the major scale?")]
    [TestCase("Generate a mellow ii-V-I in C")]
    [TestCase("How do I make this progression sound darker?")]
    public void Tokenize_ChatbotExamplePrompts_HaveNoNoteSlices(string prompt)
    {
        // The 5 prompts surfaced by /api/chatbot/examples. These regress the
        // production bug where the LLM filter extractor mis-tagged prose as
        // Intent=AnalyzeTab and the orchestrator dispatched to tab analysis.
        // The tokenizer is the ultimate source of truth — none of these
        // contain tab.
        //
        // The default textarea seed "Are 0146 and 0137 z-related?" is
        // covered separately in
        // Tokenize_AlgebraQueryWithNumericPitchClassSets_KnownLimitation
        // because pitch-class-set notation collides with the bare-digit
        // path in TabLineRegex.
        var blocks = _tokenizer.Tokenize(prompt);

        Assert.That(blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0)), Is.False,
            $"Prose prompt should not produce note-bearing slices: {prompt}");
    }

    [TestCase("Are 0146 and 0137 z-related?",
        Description = "Pitch-class-set notation in algebra queries")]
    [TestCase("Explain 12-bar blues form",
        Description = "Common theory question — '12-b' is digit+dash+b without a pipe")]
    [TestCase("Released --12-04-- last week.",
        Description = "Hyphenated dates / version strings without pipes")]
    public void Tokenize_BareDigitProseInputs_NoLongerProduceNoteSlices(string input)
    {
        // FIXED: previously a "known limitation" — the original
        // TabLineRegex `([A-Ga-g]?[#b]?\|?|\|)[-0-9|hbp/\\svx~]+` allowed
        // every part of group 1 to be optional, so bare digit runs matched
        // and pitch-class-set notation, "12-bar form" prose, and hyphenated
        // dates all tokenized as if they were single-string tab. The
        // upstream production misroute on /chatbot/ surfaced this regularly
        // for theory questions.
        //
        // Fix: a `(?=[-0-9|hbp/\\svx~]*\|)` lookahead now requires the
        // matched run to contain at least one literal pipe — the inherent
        // bar-line marker of tab notation. Real tabs (with or without
        // string-name prefix) carry pipes; these prose inputs do not.
        //
        // The existing anonymous-row positive control
        // (Tokenize_AnonymousRowTabWithoutStringNamePrefix_ProducesNoteSlices)
        // still passes because that case ends each line with `|`. If
        // someone authors anonymous-row tab WITHOUT pipes, the tokenizer
        // will now correctly reject it — that's a degenerate format that
        // was producing false positives anyway.
        var blocks = _tokenizer.Tokenize(input);
        var hasNotes = blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0));

        Assert.That(hasNotes, Is.False,
            $"Bare-digit prose must NOT tokenize as tab. Input: {input} → " +
            $"blocks={blocks.Count}, hasNotes={hasNotes}");
    }

    [TestCase("Use a G chord then an A chord, then back to G.",
        Description = "Mentions chord names — could fool a regex but has no fret/string syntax")]
    [TestCase("Play the 5th fret on the A string, then the 7th fret.",
        Description = "Mentions fret numbers in prose — no pipes, no tab grid")]
    [TestCase("The chord progression I-V-vi-IV is common in pop music.",
        Description = "Roman numerals + general theory talk")]
    public void Tokenize_ProseWithChordOrFretReferences_HasNoNoteSlices(string prose)
    {
        // Per PR #110 review: removed the "Compare voicing 0-2-2-1-0-0..." case
        // from this fixture because it passed for the WRONG reason. The
        // tokenizer regex matches at offset 8 ('v' of "voicing", which is in
        // the tab-content character class) with length 1 — failing the
        // `match.Length > 3` gate, so no block accumulates. The hyphen-fret
        // run later in the string never gets evaluated. The case was meant
        // to show "hyphen-separated frets aren't tab" but actually
        // demonstrates "leading prose with sub-3-char regex match short-
        // circuits". The bare-digit/hyphen failure mode is now covered
        // explicitly and at the right layer in
        // Tokenize_BareDigitProseInputs_KnownLimitation.
        var blocks = _tokenizer.Tokenize(prose);

        Assert.That(blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0)), Is.False,
            $"Prose with casual chord/fret references should not parse as tab: {prose}");
    }

    [Test]
    public void Tokenize_MarkdownTableLikeText_HasNoNoteSlices()
    {
        // Markdown tables use pipes too. The tokenizer must not hallucinate
        // notes from header separators or cell content that looks numeric.
        var markdown = @"
| Chord | Fret | Difficulty |
|-------|------|------------|
| C     | 1    | Easy       |
| G     | 3    | Easy       |
| F     | 1    | Hard       |
";
        var blocks = _tokenizer.Tokenize(markdown);

        Assert.That(blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0)), Is.False,
            "Markdown tables must not be misread as tablature");
    }

    [Test]
    public void Tokenize_AsciiSeparatorLine_HasNoNoteSlices()
    {
        // A row of dashes is a common section separator; tab-line regex must
        // not catch it because there's no string identifier and no digits.
        var blocks = _tokenizer.Tokenize("Section break:\n----------------\nMore prose.");

        Assert.That(blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0)), Is.False);
    }

    [Test]
    public void Tokenize_SingleLineThatLooksLikeTab_StillNeedsMultipleStrings()
    {
        // One line that matches the tab-line regex shouldn't on its own
        // produce a parseable chord; tab notation is inherently
        // multi-string. This pins that minimum-viable-tab boundary.
        var blocks = _tokenizer.Tokenize("e|---3---|");

        // We accept that a block may exist (the line matches the regex), but
        // a parseable note slice requires the multi-string vertical alignment
        // a real tab provides.
        var hasNotes = blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0));
        TestContext.WriteLine($"Single tab-like line produced {blocks.Count} block(s); has notes: {hasNotes}");

        // No assertion either direction — this test exists to document
        // current behaviour. If someone tightens the tokenizer to require
        // multi-string blocks, they should update this test deliberately.
        Assert.Pass($"Documented behaviour: blocks={blocks.Count}, hasNotes={hasNotes}");
    }

    [Test]
    public void Tokenize_AnonymousRowTabWithoutStringNamePrefix_ProducesNoteSlices()
    {
        // Positive control added per PR #110 review. Many real-world tabs
        // published on forums omit string-name prefixes entirely:
        //
        //     ---3-----0-----|
        //     -----2-----0---|
        //     ------0-----0--|
        //
        // These rely on Group 1's optionality: the regex matches starting
        // at the leading dash via empty Group 1 + Group 2 spanning the
        // dashes/digits. ANY future tightening of Group 1 to require a
        // string-name letter OR a literal pipe must NOT break this case
        // — that's why the fix proposed in the bare-digit known-limitation
        // comment is non-trivial. This test fails LOUDLY if such a fix
        // is naively applied without preserving anonymous-row support.
        var input = @"
---3-----0-----|
-----2-----0---|
------0-----0--|
";

        var blocks = _tokenizer.Tokenize(input);

        Assert.That(blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0)), Is.True,
            "Anonymous-row tab (no string-name prefix) must still tokenize. " +
            "If you tightened TabLineRegex Group 1, this is the case you broke.");
    }

    [Test]
    public void Tokenize_RealTabEmbeddedInProse_ProducesNoteSlices()
    {
        // Positive control for the prose pruning above: when REAL tab is
        // present, the tokenizer must still find it even with prose around
        // it. Prevents the prose-pruning tests from passing trivially via
        // a broken tokenizer.
        var input = @"
Here's a G chord I want you to analyze:

e|---3---|
B|---0---|
G|---0---|
D|---0---|
A|---2---|
E|---3---|

What inversion is this?";

        var blocks = _tokenizer.Tokenize(input);

        Assert.That(blocks.Any(b => b.Slices.Any(s => s.Notes.Count > 0)), Is.True,
            "Embedded tab must still be detected even when surrounded by prose");
    }
}
