namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

/// <summary>
/// Pins the shared <see cref="ChordSpelling.Spell"/> contract that
/// <see cref="GA.Business.ML.Agents.Skills.ChordInfoSkill"/> and
/// <see cref="ChordMcpTools"/> both depend on. Same input → same output;
/// any future drift breaks here before the consumers diverge.
/// </summary>
[TestFixture]
public class ChordSpellingTests
{
    // Triad letter-steps = [0, 2, 4]; seventh-chord letter-steps = [0, 2, 4, 6].
    // Pitch classes are 0=C 1=C# 2=D 3=Eb 4=E 5=F 6=F# 7=G 8=Ab 9=A 10=Bb 11=B.

    [TestCase("C", 0, 0,  "C")]   // root of C
    [TestCase("C", 4, 2,  "E")]   // major third of C → E natural
    [TestCase("C", 3, 2,  "Eb")]  // minor third of C → E flat (NOT D# — letter-step 2 forces E)
    [TestCase("C", 7, 4,  "G")]   // perfect fifth
    [TestCase("C", 6, 4,  "Gb")]  // diminished fifth → G flat (NOT F# — letter-step 4 forces G)
    [TestCase("C", 11, 6, "B")]   // major seventh
    [TestCase("C", 10, 6, "Bb")]  // minor seventh → B flat (NOT A# — letter-step 6 forces B)
    public void Spell_TriadAndSeventhChordTones_HonourLetterSteps(
        string root, int pitchClass, int letterSteps, string expected)
    {
        Assert.That(ChordSpelling.Spell(root, pitchClass, letterSteps), Is.EqualTo(expected));
    }

    [TestCase("F#", 6,  0, "F#")]
    [TestCase("F#", 10, 2, "A#")]   // major third of F# → A# (NOT Bb — letter-step 2 from F# forces A)
    [TestCase("Bb", 10, 0, "Bb")]
    [TestCase("Bb", 1,  2, "Db")]   // minor third of Bb → Db (NOT C# — letter-step 2 from Bb forces D)
    [TestCase("Bb", 5,  4, "F")]    // fifth of Bb
    public void Spell_AccidentalRoots_PickRightLetterStep(
        string root, int pitchClass, int letterSteps, string expected)
    {
        Assert.That(ChordSpelling.Spell(root, pitchClass, letterSteps), Is.EqualTo(expected));
    }

    [Test]
    public void Spell_BdimVoicing_SpellsAsBDF()
    {
        // The textbook diminished-triad case — F is the diminished fifth,
        // NOT E# despite same pitch class. Letter-step 4 from B forces F.
        Assert.That(ChordSpelling.Spell("B", 11, 0), Is.EqualTo("B"));
        Assert.That(ChordSpelling.Spell("B", 2,  2), Is.EqualTo("D"));
        Assert.That(ChordSpelling.Spell("B", 5,  4), Is.EqualTo("F"));
    }

    [Test]
    public void Spell_DoubleAccidentals_RenderCorrectly()
    {
        // Edge case: Cdim7 has a doubly-diminished seventh = Bbb.
        // Letter-step 6 from C → letter B; pitch class 9 (A) means
        // accidental = (9-11) mod 12 = 10 → "bb".
        Assert.That(ChordSpelling.Spell("C", 9, 6), Is.EqualTo("Bbb"));
    }
}
