namespace GA.Business.ML.Tests.Unit;

using System.Text.RegularExpressions;
using GA.Business.ML.Agents.Skills;

/// <summary>
/// ga#745: an improvisation request that names only invalid chords must be declined
/// deterministically, never handed to the LLM fallback, which invented theory about
/// "Hm Q7" on the public chatbot. Valid requests must not trigger the decline.
/// </summary>
[TestFixture]
public class InvalidChordNamesTests
{
    private const string CorpusRelativePath = "Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml";

    [TestCase("which arpeggio fits Hm Q7", new[] { "Hm", "Q7" })]
    [TestCase("what scale over X7alt", new[] { "X7alt" })]
    [TestCase("which arpeggios work over Hmaj7 and W9", new[] { "Hmaj7", "W9" })]
    [TestCase("improvise over Rm7b5 Rm7b5", new[] { "Rm7b5" })]
    // An opening chord-shaped token is not an interjection; an opening "A" article is not a chord.
    [TestCase("Q7, which arpeggio should I use?", new[] { "Q7" })]
    [TestCase("Hm, which arpeggio fits Q7?", new[] { "Q7" })]
    [TestCase("A friend asked which arpeggio fits Hm Q7", new[] { "Hm", "Q7" })]
    // A Roman numeral next to an invalid name is left out of the decline.
    [TestCase("which arpeggio fits V7 and Q7", new[] { "Q7" })]
    public void Find_ReturnsEachInvalidChordName(string message, string[] expected)
    {
        Assert.That(InvalidChordNames.Find(message), Is.EqualTo(expected));
    }

    // Valid chords keep their route, even next to an invalid token.
    [TestCase("which arpeggio fits Am F C G")]
    [TestCase("what scale can I use to solo over Cmaj7?")]
    [TestCase("what scale over G13")]
    [TestCase("which arpeggio fits Am and Q7")]
    [TestCase("which arpeggio fits C and Q7")]
    [TestCase("which arpeggio fits Q7 and F#")]
    [TestCase("which arpeggio fits A and Q7")]
    [TestCase("which scale fits C major")]
    // No improvisation intent: not a chord request at all.
    [TestCase("Hm Q7 is my username")]
    // Interjections, times and words that only look chord-like.
    [TestCase("Hmm, which arpeggio should I learn first?")]
    [TestCase("Um, what scale should I use?")]
    [TestCase("Hm, which mode is brightest?")]
    [TestCase("what scale should I practice at 7 PM")]
    [TestCase("which mode did Madden solo in")]
    [TestCase("I'm trying to improvise, what scale helps?")]
    // Roman-numeral harmony keeps the skill's route, which asks for a key.
    [TestCase("what scale for V7?")]
    [TestCase("which arpeggio fits the V7b9")]
    [TestCase("what scale over I7")]
    public void Find_ReturnsNothing_ForValidOrNonChordRequests(string message)
    {
        Assert.That(InvalidChordNames.Find(message), Is.Empty);
    }

    [Test]
    public void Decline_NamesTheTokens_WithoutInventingTheory()
    {
        var answer = InvalidChordNames.Decline(["Hm", "Q7"]);

        Assert.Multiple(() =>
        {
            // The corpus entry for this prompt accepts any of its decline phrases (#743).
            Assert.That(answer, Does.Contain("don't recognize"));
            Assert.That(answer, Does.Contain("\"Hm\" and \"Q7\""));
            Assert.That(answer, Does.Not.Contain("also known as"));
            Assert.That(answer, Does.Contain("A to G"));
        });
    }

    [Test]
    public void Decline_PointsAnHRootToGermanNotation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InvalidChordNames.Decline(["Hm"]), Does.Contain("\"Hm\" is written \"Bm\""));
            Assert.That(InvalidChordNames.Decline(["Q7"]), Does.Not.Contain("German"));
        });
    }

    [Test]
    public void Find_ReturnsNothing_ForTheSkillsOwnExamplePrompts()
    {
        var skill = new ImprovisationSkill(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ImprovisationSkill>.Instance, null!);

        foreach (var prompt in skill.ExamplePrompts)
            Assert.That(InvalidChordNames.Find(prompt), Is.Empty, prompt);
    }

    /// <summary>
    /// Every corpus prompt keeps its route: the decline fires only on entries that
    /// expect a decline phrase (the "Hm Q7" entry that #743 adds).
    /// </summary>
    [Test]
    public void Find_FiresOnlyOnCorpusEntriesThatExpectADecline()
    {
        var entries = LoadCorpusEntries();
        Assert.That(entries, Has.Count.GreaterThan(50), "corpus not parsed");

        foreach (var (prompt, block) in entries)
        {
            var expectsDecline = block.Contains("don't recognize", StringComparison.Ordinal)
                                 || block.Contains("not a valid chord", StringComparison.Ordinal);
            Assert.That(InvalidChordNames.Find(prompt).Count > 0, Is.EqualTo(expectsDecline), prompt);
        }
    }

    private static List<(string Prompt, string Block)> LoadCorpusEntries()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx")))
            dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "Could not find repo root (AllProjects.slnx)");

        var text = File.ReadAllText(Path.Combine(dir!.FullName, CorpusRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var starts = Regex.Matches(text, @"^\s*- prompt:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Multiline);
        var entries = new List<(string, string)>();
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
            entries.Add((Regex.Unescape(starts[i].Groups[1].Value), text[starts[i].Index..end]));
        }
        return entries;
    }
}
