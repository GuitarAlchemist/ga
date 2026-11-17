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

    // A plural question asks for every chord holding the notes, even when they also spell one chord.
    [Test]
    public async Task PluralQuestion_ListsEveryChordHoldingAFullNoteSet()
    {
        var response = await Skill.ExecuteAsync("Which common chords contain C E G?");

        Assert.Multiple(() =>
        {
            foreach (var chord in new[] { "**C**", "**C7**", "**Cmaj7**", "**Am7**" })
            {
                Assert.That(response.Result, Does.Contain(chord));
            }
        });
    }

    // Fsus2 and Csus4 share the notes C, F and G, but they are different chords; only the
    // symmetric chords (augmented, diminished seventh) are one chord under several roots.
    [Test]
    public async Task SuspendedChordsSharingNotes_AreBothListed()
    {
        var response = await Skill.ExecuteAsync("Which chords contain C and F?");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("**Fsus2**"));
            Assert.That(response.Result, Does.Contain("**Csus4**"));
        });
    }

    [Test]
    public async Task SymmetricChords_AreListedOnce()
    {
        var response = await Skill.ExecuteAsync("Which chords contain both E and G#?");

        Assert.That(response.Result.Split("aug**").Length - 1, Is.EqualTo(1), response.Result);
    }
}
