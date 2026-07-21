namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class OutsideNotesSkillTests
{
    private static OutsideNotesSkill MakeSkill() =>
        new(NullLogger<OutsideNotesSkill>.Instance);

    private static async Task<string> AnswerAsync(string message) =>
        (await MakeSkill().ExecuteAsync(message)).Result;

    [Test]
    public void Metadata_ExposesExamplePrompts()
    {
        var skill = MakeSkill();
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(skill.Description.ToLowerInvariant(),
            Does.Contain("avoid").Or.Contain("tension").Or.Contain("outside"));
    }

    // ===============================================================
    // Classify — the domain core. Pitch classes: C=0 .. B=11.
    // Rule: chord tone if in the formula; avoid if a semitone above a
    // chord tone; otherwise available tension.
    // ===============================================================

    // Cmaj7 = {C,E,G,B} = {0,4,7,11}. root C = 0.
    [TestCase(0, "major 7", 5, "Avoid")]    // F  = 11th, a semitone above E (3rd) → avoid
    [TestCase(0, "major 7", 1, "Avoid")]    // Db = b9, a semitone above C (root) → avoid
    [TestCase(0, "major 7", 9, "Tension")]  // A  = 13, whole step above G → tension
    [TestCase(0, "major 7", 6, "Tension")]  // F# = #11, not a semitone above any tone → tension
    [TestCase(0, "major 7", 2, "Tension")]  // D  = 9 → tension
    [TestCase(0, "major 7", 4, "ChordTone")]// E  = the major 3rd
    [TestCase(0, "major 7", 11, "ChordTone")]// B = the major 7th
    [TestCase(0, "major 7", 7, "ChordTone")]// G = the 5th
    // G7 = {G,B,D,F} = {7,11,2,5}. root G = 7.
    [TestCase(7, "dominant 7", 0, "Avoid")]   // C = 11 over G7, a semitone above B (3rd) → avoid
    [TestCase(7, "dominant 7", 9, "Tension")] // A = 9 over G7 → tension
    [TestCase(7, "dominant 7", 8, "Avoid")]   // Ab = b9 over G7, a semitone above the root G → avoid (used as the b9 alteration)
    // Cm7 = {C,Eb,G,Bb} = {0,3,7,10}. The natural 11 (F) is NOT avoid over minor.
    [TestCase(0, "minor 7", 5, "Tension")]  // F = 11 over Cm7 — Eb+1=E not F, so no clash → tension
    [TestCase(0, "minor 7", 8, "Avoid")]    // Ab = b13, a semitone above G (5th) → avoid
    [TestCase(0, "minor 7", 2, "Tension")]  // D = 9 → tension
    [TestCase(0, "minor 7", 3, "ChordTone")]// Eb = the minor 3rd
    public void Classify_AppliesTheAvoidNoteRule(int rootPc, string quality, int notePc, string expectedKind)
    {
        var verdict = OutsideNotesSkill.Classify(rootPc, quality, notePc);
        Assert.That(verdict.Kind.ToString(), Is.EqualTo(expectedKind),
            $"note pc {notePc} over root {rootPc} {quality} classified {verdict.Kind}; expected {expectedKind} ({verdict.DegreeLabel})");
    }

    [Test]
    public void Classify_Ab_Over_G7_IsAvoid_BecauseAboveRoot()
    {
        // Ab (8) over G7 (root 7): 7+1 = 8, and the root IS a chord tone, so Ab
        // is a semitone above the root → avoid (the b9). Sanity-check the rule
        // catches "semitone above the root", not just above upper structure.
        var v = OutsideNotesSkill.Classify(7, "dominant 7", 8);
        Assert.That(v.Kind, Is.EqualTo(OutsideNotesSkill.RelationKind.Avoid));
        Assert.That(v.DegreeLabel, Does.Contain("b9"));
    }

    [TestCase(0, "root")]
    [TestCase(1, "b9")]
    [TestCase(2, "9")]
    [TestCase(5, "11")]
    [TestCase(6, "#11")]
    [TestCase(8, "b13")]
    [TestCase(9, "13")]
    public void ExtensionLabel_NamesDegrees(int rel, string expectedFragment)
    {
        Assert.That(OutsideNotesSkill.ExtensionLabel(rel), Does.Contain(expectedFragment));
    }

    // ===============================================================
    // Parsing — "<note> ... over <chord>".
    // ===============================================================

    [TestCase("why does F sound outside over Cmaj7", 5, 0, "major 7")]
    [TestCase("is F an avoid note over Cmaj7", 5, 0, "major 7")]
    [TestCase("what is F over G7", 5, 7, "dominant 7")]
    [TestCase("why does Bb sound outside over C major", 10, 0, "major")]
    [TestCase("is A a chord tone or a tension over Cmaj7", 9, 0, "major 7")]
    [TestCase("why does F# clash over a Cm7 chord", 6, 0, "minor 7")]
    public void ParseNoteOverChord_ExtractsNoteAndChord(string message, int notePc, int rootPc, string quality)
    {
        var parsed = OutsideNotesSkill.ParseNoteOverChord(message);
        Assert.That(parsed, Is.Not.Null, $"failed to parse: {message}");
        Assert.Multiple(() =>
        {
            Assert.That(parsed!.Value.NotePc, Is.EqualTo(notePc), "note pc");
            Assert.That(parsed!.Value.RootPc, Is.EqualTo(rootPc), "root pc");
            Assert.That(parsed!.Value.Quality, Is.EqualTo(quality), "quality");
        });
    }

    [TestCase("what scale should I use")]            // no chord, no note-over-chord shape
    [TestCase("tell me about the C major scale")]    // no preposition
    [TestCase("")]
    public void ParseNoteOverChord_ReturnsNull_OnUnparseable(string message)
    {
        Assert.That(OutsideNotesSkill.ParseNoteOverChord(message), Is.Null);
    }

    // ===============================================================
    // ExecuteAsync — end-to-end wording.
    // ===============================================================

    [Test]
    public async Task Execute_F_Over_Cmaj7_ExplainsTheAvoidClash()
    {
        var answer = await AnswerAsync("why does F sound outside over Cmaj7");
        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("avoid"));
            Assert.That(answer, Does.Contain("11"));
            Assert.That(answer.ToLowerInvariant(), Does.Contain("semitone").Or.Contain("half-step").Or.Contain("half step"));
        });
    }

    [Test]
    public async Task Execute_A_Over_Cmaj7_IsAvailableTension()
    {
        var answer = await AnswerAsync("is A a tension over Cmaj7");
        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("tension"));
            Assert.That(answer, Does.Contain("13"));
            Assert.That(answer, Does.Not.Contain("avoid"));
        });
    }

    [Test]
    public async Task Execute_E_Over_Cmaj7_IsChordTone()
    {
        var answer = await AnswerAsync("is E a chord tone or tension over Cmaj7");
        Assert.That(answer.ToLowerInvariant(), Does.Contain("chord tone").Or.Contain("major third"));
    }

    // ===============================================================
    // CanHandle gating.
    // ===============================================================

    [TestCase("why does F sound outside over Cmaj7")]
    [TestCase("is F an avoid note over Cmaj7")]
    [TestCase("why does A clash against a G7")]
    public void CanHandle_True_OnOutsideQueries(string message)
    {
        Assert.That(MakeSkill().CanHandle(message), Is.True, $"should claim: {message}");
    }

    [TestCase("what notes are in Cmaj7")]         // no outside intent
    [TestCase("what scale can I solo over G7")]   // improvisation, not note-classification
    [TestCase("transpose C E G up a fifth")]
    [TestCase("")]
    public void CanHandle_False_OnNonOutsideQueries(string message)
    {
        Assert.That(MakeSkill().CanHandle(message), Is.False, $"should NOT claim: {message}");
    }
}
