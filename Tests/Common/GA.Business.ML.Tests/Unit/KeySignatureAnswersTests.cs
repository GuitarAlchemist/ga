namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Key-signature questions get a direct answer from the domain <c>Key</c>, whichever of
/// <see cref="RelativeKeySkill"/> or <see cref="CircleOfFifthsSkill"/> the router picks.
/// On 2026-10-03 "How many sharps are in the key of A major?" routed to the circle-of-fifths
/// catalog skill and got its whole page back.
/// </summary>
[TestFixture]
public class KeySignatureAnswersTests
{
    [TestCase("How many sharps are in the key of A major?", "**A major** has 3 sharps. The sharps are F#, C#, G#. Its relative minor, F# minor, shares this key signature.")]
    [TestCase("how many flats does the key of Eb major have", "**Eb major** has 3 flats. The flats are Bb, Eb, Ab. Its relative minor, C minor, shares this key signature.")]
    [TestCase("What's the key signature of F# minor?", "**F# minor** has 3 sharps. The sharps are F#, C#, G#. Its relative major, A major, shares this key signature.")]
    [TestCase("key signature for Bbm", "**Bb minor** has 5 flats. The flats are Bb, Eb, Ab, Db, Gb. Its relative major, Db major, shares this key signature.")]
    [TestCase("How many flats in F major", "**F major** has 1 flat. The flat is Bb. Its relative minor, D minor, shares this key signature.")]
    [TestCase("how many flats in E flat major", "**Eb major** has 3 flats. The flats are Bb, Eb, Ab. Its relative minor, C minor, shares this key signature.")]
    [TestCase("How many sharps does F-sharp minor have?", "**F# minor** has 3 sharps. The sharps are F#, C#, G#. Its relative major, A major, shares this key signature.")]
    [TestCase("how many sharps does C major have","**C major** has no sharps or flats. Its relative minor, A minor, shares this key signature.")]
    [TestCase("how many sharps in d?", "**D major** has 2 sharps. The sharps are F#, C#. Its relative minor, B minor, shares this key signature.")]
    [TestCase("key signature for e", "**E major** has 4 sharps. The sharps are F#, C#, G#, D#. Its relative minor, C# minor, shares this key signature.")]
    [TestCase("how many flats in b flat?", "**Bb major** has 2 flats. The flats are Bb, Eb. Its relative minor, G minor, shares this key signature.")]
    public void KeyToSignature(string question, string expected) =>
        Assert.That(Answer(question), Is.EqualTo(expected));

    [TestCase("which major key has 4 flats?", "**Ab major** has 4 flats: Bb, Eb, Ab, Db. Its relative minor, **F minor**, shares this key signature.")]
    [TestCase("What minor key has two sharps", "**B minor** has 2 sharps: F#, C#. Its relative major, **D major**, shares this key signature.")]
    [TestCase("what key has 5 sharps", "5 sharps (F#, C#, G#, D#, A#) is the key signature of **B major** and its relative minor, **G# minor**.")]
    [TestCase("which key has no sharps", "No sharps or flats is the key signature of **C major** and its relative minor, **A minor**.")]
    [TestCase("which major key has 7 flats", "**Cb major** has 7 flats: Bb, Eb, Ab, Db, Gb, Cb, Fb. Its relative minor, **Ab minor**, shares this key signature.")]
    public void SignatureToKey(string question, string expected) =>
        Assert.That(Answer(question), Is.EqualTo(expected));

    [Test]
    public void MoreThanSevenAccidentals_SaysNoStandardKeyHasThem() =>
        Assert.That(Answer("which key has 8 sharps"), Does.StartWith("No standard key signature has 8 sharps: the most is 7"));

    [Test]
    public void TheoreticalKey_PointsToItsEnharmonic() =>
        Assert.That(Answer("How many sharps are in G# major?"), Is.EqualTo(
            "**G# major** is a theoretical key: its signature would need 8 sharps, some of them double sharps. " +
            "Music in that key is written in its enharmonic equivalent, **Ab major**, which has 4 flats. The flats are Bb, Eb, Ab, Db."));

    [TestCase("Explain the circle of fifths")]
    [TestCase("What is the relative minor of C major?")]
    [TestCase("How many sharps can a key signature have?")]
    [TestCase("What notes are in A major?")]
    [TestCase("How many sharps does a key signature have?")]
    [TestCase("What's the key signature of a song?")]
    [TestCase("How many sharps are in a key signature?")]
    public void OtherQuestions_AreNotKeySignatureQuestions(string question) =>
        Assert.That(KeySignatureAnswers.TryAnswer(question, out _, out _), Is.False);

    [Test]
    public async Task CircleOfFifthsSkill_AnswersASpecificKeySignatureDirectly()
    {
        var skill = new CircleOfFifthsSkill(NullLogger<CircleOfFifthsSkill>.Instance);

        var specific = await skill.ExecuteAsync("How many sharps are in the key of A major?");
        var general = await skill.ExecuteAsync("Explain the circle of fifths");

        Assert.Multiple(() =>
        {
            Assert.That(specific.Result, Does.StartWith("**A major** has 3 sharps. The sharps are F#, C#, G#."));
            Assert.That(specific.Result, Does.Not.Contain("Circle of Fifths"));
            Assert.That(general.Result, Does.Contain("key signature"));
            Assert.That(general.Result.Length, Is.GreaterThan(500), "the general question still gets the whole page");
        });
    }

    [TestCase("How many sharps are in the key of A major?", "**A major** has 3 sharps.")]
    [TestCase("which major key has 4 flats?", "**Ab major** has 4 flats")]
    [TestCase("how many sharps in d?", "**D major** has 2 sharps.")]
    [TestCase("key signature for f#", "**F# major** has 6 sharps. The sharps are F#, C#, G#, D#, A#, E#.")]
    public async Task RelativeKeySkill_AnswersBothDirections(string question, string expected)
    {
        var skill = new RelativeKeySkill(NullLogger<RelativeKeySkill>.Instance);

        var response = await skill.ExecuteAsync(question);

        Assert.Multiple(() =>
        {
            Assert.That(skill.CanHandle(question), Is.True);
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result, Does.StartWith(expected));
        });
    }

    private static string Answer(string question)
    {
        Assert.That(KeySignatureAnswers.TryAnswer(question, out var answer, out _), Is.True, question);
        return answer;
    }
}
