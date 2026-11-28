namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Issue #815: the comparison regexes read 5 of 20 phrasings of "major vs minor", and
/// "the major vs minor" was refused because, with IgnoreCase, the "e" of "the" and the
/// article "a" were read as key letters by the lookbehind meant to keep "C major vs C minor" out.
/// </summary>
[TestFixture]
public class TheoryComparisonSkillTests
{
    private static readonly TheoryComparisonSkill Skill = new(NullLogger<TheoryComparisonSkill>.Instance);

    [TestCase("What is the difference between major and minor")]
    [TestCase("Compare major and minor")]
    [TestCase("Major vs minor")]
    [TestCase("Major versus minor")]
    [TestCase("Difference between major and minor scales")]
    [TestCase("Explain the difference between major and minor")]
    [TestCase("How do major and minor differ")]
    // The phrasings the skill used to refuse.
    [TestCase("What is the difference between the major and minor scales")]
    [TestCase("Difference between a major and a minor chord")]
    [TestCase("Majors vs minors")]
    [TestCase("Major and minor difference")]
    [TestCase("How are major and minor different")]
    [TestCase("Major or minor: what's the difference?")]
    [TestCase("Major or minor: what’s the difference?")]
    [TestCase("Explain the major vs minor difference")]
    [TestCase("Which sounds sadder, a major vs minor chord?")]
    [TestCase("Compare the major scale with the minor scale")]
    public async Task ComparisonPhrasing_IsAnsweredAndAccepted(string message)
    {
        var response = await Skill.ExecuteAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.False, message);
            Assert.That(response.Result, Does.Contain("third"), message);
            Assert.That(Skill.CanHandle(message), Is.True, "the offline keyword fallback must accept what ExecuteAsync answers");
        });
    }

    [TestCase("C major vs C minor")]
    [TestCase("C major vs minor")]
    [TestCase("What's the difference between C major and C minor")]
    [TestCase("Bb major versus Bb minor")]
    [TestCase("A major vs A minor")]
    [TestCase("C major vs F major")]
    [TestCase("Hendrix vs Clapton")]
    [TestCase("What's the relative minor of C major")]
    [TestCase("Major scale")]
    public async Task NotAComparisonOfTheTwoQualities_IsDeclined(string message)
    {
        var response = await Skill.ExecuteAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.True, message);
            Assert.That(Skill.CanHandle(message), Is.False, message);
        });
    }

    [Test]
    public async Task Answer_DoesNotClaimTheThirdIsTheOnlyDifference()
    {
        var response = await Skill.ExecuteAsync("Major vs minor");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Not.Contain("single interval"));
            Assert.That(response.Result, Does.Contain("one degree that differs in every minor form"));
            Assert.That(response.Result, Does.Contain("differs from major at degrees 3, 6 and 7"), "natural minor");
            Assert.That(response.Result, Does.Contain("differs from major at degrees 3 and 6"), "harmonic minor");
            Assert.That(response.Result, Does.Contain("differs from major at the 3rd alone"), "melodic minor, ascending");
        });
    }

    [TestCase("Major vs major", "major vs minor")]
    [TestCase("minor versus minor", "minor vs major")]
    public async Task SamePair_SuggestsOnlyAComparisonTheSkillAnswers(string message, string suggestion)
    {
        var response = await Skill.ExecuteAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.False);
            Assert.That(response.Result, Does.Contain(suggestion));
            Assert.That(response.Result, Does.Not.Contain("dorian"));
            Assert.That(Skill.CanHandle(suggestion), Is.True, "the suggested question must be one the skill answers");
        });
    }
}
