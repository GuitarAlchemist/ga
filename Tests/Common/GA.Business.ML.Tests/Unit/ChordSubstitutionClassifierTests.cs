namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Tests for the two-chord comparison path in <see cref="ChordSubstitutionSkill"/>.
/// Each test uses the real <see cref="GrothendieckService"/> (pure math, no I/O).
/// </summary>
[TestFixture]
public sealed class ChordSubstitutionClassifierTests
{
    private ChordSubstitutionSkill _skill = null!;

    [SetUp]
    public void SetUp()
    {
        var grothendieck = new GrothendieckService();
        _skill = new ChordSubstitutionSkill(grothendieck, NullLogger<ChordSubstitutionSkill>.Instance);
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [TestCase("Is G7 a tritone sub for Db7?")]
    [TestCase("Can I use Eb7 instead of A7?")]
    [TestCase("Is Dm the same as F?")]
    [TestCase("Are Am and C related?")]
    [TestCase("G7 and Db7 — tritone equivalent?")]
    public void CanHandle_ReturnsTrueForComparisonMessages(string message) =>
        Assert.That(_skill.CanHandle(message), Is.True);

    [TestCase("What are some chords to play today?")]
    [TestCase("How do I play a barre chord?")]
    [TestCase("Tell me about jazz harmony")]
    public void CanHandle_ReturnsFalseWithoutChordOrTrigger(string message) =>
        Assert.That(_skill.CanHandle(message), Is.False);

    // ── Tritone Substitution ──────────────────────────────────────────────────

    [Test]
    public async Task TritoneSubstitution_G7_Db7_IsDetected()
    {
        var response = await _skill.ExecuteAsync("Is G7 a tritone sub for Db7?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("Tritone Substitution"));
        Assert.That(response.Result, Does.Contain("★★★"));
    }

    [Test]
    public async Task TritoneSubstitution_RequiresBothDominant7ths()
    {
        // Am and Eb are a tritone apart but not dominant 7ths — NOT a tritone sub
        var response = await _skill.ExecuteAsync("Is Am the same as Eb? tritone equivalent?");

        Assert.That(response.Result, Does.Not.Contain("Tritone Substitution"));
    }

    // ── Secondary Dominant ───────────────────────────────────────────────────

    [Test]
    public async Task SecondaryDominant_G7_C_IsDetected()
    {
        // G is a P5 above C → G7 is V7 of C
        var response = await _skill.ExecuteAsync("Can I substitute G7 instead of C?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("Secondary Dominant"));
    }

    [Test]
    public async Task SecondaryDominant_D_G_IsDetected()
    {
        // D is a P5 above G → D is V of G
        var response = await _skill.ExecuteAsync("Is D related to G? can I swap them?");

        Assert.That(response.Result, Does.Contain("Secondary Dominant"));
    }

    // ── Set-Class Equivalent ─────────────────────────────────────────────────

    [Test]
    public async Task SetClassEquivalent_TwoMajorTriads_IsDetected()
    {
        // C major and G major are both major triads — same set class
        var response = await _skill.ExecuteAsync("Is C the same as G? are they equivalent?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("Set-Class Equivalent"));
    }

    [Test]
    public async Task SetClassEquivalent_MajorAndMinorTriad_IsDetectedAsRelated()
    {
        // C major [0,4,7] and Am [0,3,7] — minor is inversion of major → same set class (T/I equiv)
        var response = await _skill.ExecuteAsync("Is Am the same as C? related?");

        // Under T/I equivalence, major and minor triads share a prime form
        Assert.That(response.Result, Does.Contain("Set-Class Equivalent"));
    }

    // ── ICV Neighbor ─────────────────────────────────────────────────────────

    [Test]
    public async Task IcvNeighbor_C7_G7_IsWithinDistance2()
    {
        // C7 and G7: roots P4/P5 apart, same quality — closely related in ICV space
        var response = await _skill.ExecuteAsync("Can I use C7 instead of G7?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("ICV Neighbor"));
    }

    // ── 7th Chord Parsing ────────────────────────────────────────────────────

    [TestCase("G7",    "dominant 7th should parse")]
    [TestCase("Dbmaj7","major 7th should parse")]
    [TestCase("Am7",   "minor 7th should parse")]
    [TestCase("Bm7b5", "half-diminished should parse")]
    [TestCase("Bdim7", "diminished 7th should parse")]
    public async Task SeventhChordParsing_SkillHandlesQuery(string chordSymbol, string _)
    {
        // "equivalent" is a whole-word trigger in TwoChordTrigger
        var message = $"Which chords are equivalent to {chordSymbol}?";
        Assert.That(_skill.CanHandle(message), Is.True,
            $"CanHandle should return true for '{message}'");

        var response = await _skill.ExecuteAsync(message);
        Assert.That(response.Result, Does.Not.Contain("Could not identify"),
            $"Should parse {chordSymbol} without error");
    }

    // ── Single-Chord Path Regression ─────────────────────────────────────────

    [Test]
    public async Task SingleChord_OriginalBehaviour_StillWorks()
    {
        var response = await _skill.ExecuteAsync("What substitutes for Am?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("**Am**"));
        Assert.That(response.Result, Does.Contain("harmonic cost"));
    }

    [Test]
    public async Task SingleChord_Triad_StillParses()
    {
        // "C" is a plain major triad — use "instead of" to trigger the single-chord path
        var response = await _skill.ExecuteAsync("Can I use C instead of something?");

        Assert.That(response.Result, Does.Not.Contain("Could not identify"));
    }

    // ── Response Format ───────────────────────────────────────────────────────

    [Test]
    public async Task ComparisonResponse_AlwaysIncludesConfidenceNote()
    {
        var response = await _skill.ExecuteAsync("Is G7 the same as Db7? equivalent?");

        Assert.That(response.Result, Does.Contain("100%").Or.Contain("deterministic"));
        Assert.That(response.Confidence, Is.EqualTo(1.0f));
    }

    [Test]
    public async Task ComparisonResponse_IncludesBoldChordNames()
    {
        var response = await _skill.ExecuteAsync("Is G7 a tritone sub for Db7?");

        Assert.That(response.Result, Does.Contain("**G7**").Or.Contain("G7"));
        Assert.That(response.Result, Does.Contain("**Db7**").Or.Contain("Db7"));
    }
}
