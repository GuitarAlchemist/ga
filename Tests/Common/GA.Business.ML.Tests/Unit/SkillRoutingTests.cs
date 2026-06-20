namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class SkillRoutingTests
{
    [Test]
    public async Task ChordInfoSkill_Answers_CMajorChordNotes()
    {
        var skill = new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance);

        var response = await skill.ExecuteAsync("What is a C major chord? Be brief.");

        Assert.Multiple(() =>
        {
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result, Is.EqualTo("C major chord contains C, E, and G."));
            Assert.That(response.Evidence, Has.Some.Contains("Notes: C, E, G"));
        });
    }

    [TestCase("What is a B major chord?", "B major chord contains B, D#, and F#.")]
    [TestCase("What is a Bb major chord?", "Bb major chord contains Bb, D, and F.")]
    [TestCase("What is a C# minor chord?", "C# minor chord contains C#, E, and G#.")]
    [TestCase("What is a B7 chord?", "B dominant 7 chord contains B, D#, F#, and A.")]
    [TestCase("What notes are in Dm7?", "D minor 7 chord contains D, F, A, and C.")]
    [TestCase("What notes are in an F minor chord?", "F minor chord contains F, Ab, and C.")]
    [TestCase("What notes are in a C minor chord?", "C minor chord contains C, Eb, and G.")]
    [TestCase("What notes are in a G minor chord?", "G minor chord contains G, Bb, and D.")]
    [TestCase("What notes are in an Ab minor chord?", "Ab minor chord contains Ab, Cb, and Eb.")]
    [TestCase("What notes are in a Cb major chord?", "Cb major chord contains Cb, Eb, and Gb.")]
    public async Task ChordInfoSkill_Spells_CommonChordRoots(string prompt, string expected)
    {
        var skill = new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance);

        var response = await skill.ExecuteAsync(prompt);

        Assert.That(response.Result, Is.EqualTo(expected));
    }

    // Regression: the skill used to lowercase the whole quality token before matching, so an
    // uppercase "M" (major) folded to "m" (minor) and "CM" silently spelled C minor. The PR #80 fix
    // lived only in ChordMcpTools until the shared ChordVocabulary seam (architecture-review #3) gave
    // both adapters one case-sensitive NormalizeQuality. These pin the skill side of that contract.
    [TestCase("What is a CM chord?", "C major chord contains C, E, and G.")]
    [TestCase("What is a CM7 chord?", "C major 7 chord contains C, E, G, and B.")]
    public async Task ChordInfoSkill_UppercaseMQualifier_ResolvesToMajor(string prompt, string expected)
    {
        var skill = new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance);

        var response = await skill.ExecuteAsync(prompt);

        Assert.That(response.Result, Is.EqualTo(expected),
            $"'{prompt}' must resolve to major, NOT minor (skill side of the PR #80 case-sensitivity fix)");
    }

    [Test]
    public async Task ChordInfoSkill_Identifies_ChordFromNoteSet()
    {
        var skill = new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance);

        var response = await skill.ExecuteAsync("Which chord contains C E G?");

        Assert.Multiple(() =>
        {
            Assert.That(skill.CanHandle("Which chord contains C E G?"), Is.True);
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result, Is.EqualTo("C major chord contains C, E, and G."));
        });
    }

    [Test]
    public void ChordInfoSkill_DoesNotHandle_ScaleQuestion()
    {
        var skill = new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance);

        Assert.That(skill.CanHandle("What notes are in the C major scale?"), Is.False);
    }

    [Test]
    public void ScaleInfoSkill_DoesNotHandle_ChordQuestion()
    {
        var skill = new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance);

        Assert.That(skill.CanHandle("What notes are in a C major chord?"), Is.False);
    }

    [Test]
    public void ScaleInfoSkill_DoesNotHandle_NullOrWhitespace()
    {
        var skill = new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(skill.CanHandle(null!), Is.False);
            Assert.That(skill.CanHandle("   "), Is.False);
        });
    }

    [Test]
    public async Task ScaleInfoSkill_StillAnswers_ScaleQuestion()
    {
        var skill = new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance);

        var response = await skill.ExecuteAsync("What notes are in the C major scale?");

        Assert.Multiple(() =>
        {
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result, Does.Contain("C major scale"));
            Assert.That(response.Result, Does.Contain("C"));
            Assert.That(response.Result, Does.Contain("G"));
        });
    }
}
