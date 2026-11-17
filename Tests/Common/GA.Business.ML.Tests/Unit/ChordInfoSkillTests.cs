namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Note-set questions for <see cref="ChordInfoSkill"/>: a full note set names one chord, and a
///     partial one ("which chords contain both E and G#?") lists the common chords holding it (issue #888).
/// </summary>
[TestFixture]
public class ChordInfoSkillTests
{
    private static readonly ChordInfoSkill Skill = new(NullLogger<ChordInfoSkill>.Instance);

    [Test]
    public async Task ChordsContainingTwoNotes_ListsTheCommonChordsHoldingBoth()
    {
        var response = await Skill.ExecuteAsync("Which common chords contain both E and G#?");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Not.Contain("Could not parse"));
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            foreach (var chord in new[] { "**E**", "**C#m**", "**E7**", "**Emaj7**", "**C#m7**", "**Amaj7**" })
            {
                Assert.That(response.Result, Does.Contain(chord));
            }

            Assert.That(response.Result, Does.Not.Contain("**Em**"), "E minor has G, not G#");
            Assert.That(response.Result, Does.Not.Contain("**A**"), "A major has C#, E and A, but no G#");
        });
    }

    [Test]
    public async Task ChordsContainingNotes_SpellsEachChordFromItsRoot()
    {
        var response = await Skill.ExecuteAsync("Which chords contain both E and G#?");

        Assert.That(response.Result, Does.Contain("**Amaj7** (A C# E G#)"));
    }

    [Test]
    public async Task ChordsContainingNotes_ReadsArticlesAsWords()
    {
        var response = await Skill.ExecuteAsync("What chords have a C and an E in them?");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("**C**"));
            Assert.That(response.Result, Does.Contain("**Am**"));
            Assert.That(response.Result, Does.Not.Contain("**A**"), "the article \"a\" is not the note A");
        });
    }

    [TestCase("What chord contains C E G?", "C major chord contains C, E, and G.")]
    [TestCase("What chord is F A C E", "F major 7 chord contains F, A, C, and E.")]
    public async Task FullNoteSet_StillNamesOneChord(string prompt, string expected)
    {
        var response = await Skill.ExecuteAsync(prompt);

        Assert.That(response.Result, Is.EqualTo(expected));
    }
}
