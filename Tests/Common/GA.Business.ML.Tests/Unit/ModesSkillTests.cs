namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public sealed class ModesSkillTests
{
    private static ModesSkill MakeSkill() => new(NullLogger<ModesSkill>.Instance);

    // The 2026-10-03 probe asked this and got a description of Aeolian alone: the longest alias won.
    [Test]
    public async Task TwoModesNamed_ComparesBothAndNamesTheDifferingDegree()
    {
        var response = await MakeSkill().ExecuteAsync("What is the difference between the Dorian and Aeolian modes?");

        Assert.That(response.Result, Does.Contain("**Dorian**").And.Contain("**Aeolian**"));
        Assert.That(response.Result, Does.Contain("degree 6: Dorian `6`, Aeolian `b6`"));
    }

    [Test]
    public async Task ModeNameInsideALongerName_IsNotASecondMode()
    {
        var response = await MakeSkill().ExecuteAsync("What is Lydian dominant");

        Assert.That(response.Result, Does.StartWith("**Lydian Dominant**"));
        Assert.That(response.Result, Does.Not.Contain("Differences"));
    }

    // A 2026-10-03 baseline question: "E mixolydian" was answered with C Mixolydian's notes.
    [TestCase("What notes are in E mixolydian?", "on E its notes are `E F# G# A B C# D`")]
    [TestCase("What is F# lydian dominant", "on F# its notes are `F# G# A# B# C# D# E`")]
    [TestCase("Show me Bb dorian", "on Bb its notes are `Bb C Db Eb F G Ab`")]
    [TestCase("What is a dorian scale", "on C its notes are `C D Eb F G A Bb`")]
    public async Task SingleMode_IsSpelledOnTheRootTheUserNamed(string question, string expected)
    {
        var response = await MakeSkill().ExecuteAsync(question);

        Assert.That(response.Result, Does.Contain(expected));
    }

    // Formulas used to come from list position, so any scale that skips a letter was wrong:
    // major pentatonic C D E G A printed as "1 2 3 ##4 ##5".
    [TestCase("What is major pentatonic", "`1 2 3 5 6`")]
    [TestCase("What is minor pentatonic", "`1 b3 4 5 b7`")]
    [TestCase("What is the blues scale", "`1 b3 4 #4 5 b7`")]
    [TestCase("What is Dorian", "`1 2 b3 4 5 6 b7`")]
    public async Task SingleMode_FormulaFollowsTheNoteLetters(string question, string formula)
    {
        var response = await MakeSkill().ExecuteAsync(question);

        Assert.That(response.Result, Does.Contain($"(formula {formula})"));
    }
}
