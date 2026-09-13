namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class RelativeKeySkillTests
{
    private static RelativeKeySkill MakeSkill() => new(NullLogger<RelativeKeySkill>.Instance);

    [TestCase("What is the relative minor of Eb major", "The relative minor of **Eb major** is **Cm**.\n\nBoth share the same key signature (3 flats). Same notes, different tonal center — the relative minor starts on the 6th degree of the major scale.")]
    [TestCase("relative minor of Bb major", "The relative minor of **Bb major** is **Gm**.\n\nBoth share the same key signature (2 flats). Same notes, different tonal center — the relative minor starts on the 6th degree of the major scale.")]
    [TestCase("What is the relative major of Am", "The relative major of **A minor** is **C major**.\n\nBoth share the same key signature (no sharps or flats). Same notes, different tonal center — the relative major starts on the 3rd degree of the minor scale.")]
    [TestCase("Parallel minor of C major", "The parallel minor of **C major** is **C minor**.\n\nSame root note (**C**) but different scales — the parallel minor lowers the 3rd, 6th, and 7th degrees. C major has no sharps or flats; C minor has 3 flats (three positions counter-clockwise on the circle of fifths).")]
    [TestCase("What is the parallel major of Am", "The parallel major of **A minor** is **A major**.\n\nSame root note (**A**) but different scales — the parallel major raises the 3rd, 6th, and 7th degrees. The parallel major sits three positions clockwise on the circle of fifths.")]
    [TestCase("How many flats in F major", "**F major** has 1 flat.")]
    [TestCase("How many sharps in D major", "**D major** has 2 sharps.")]
    [TestCase("What's the key signature of E major", "**E major** has 4 sharps.")]
    public async Task RelativeKeySkill_ExecutesCorrectly(string prompt, string expectedSnippet)
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync(prompt);

        Assert.Multiple(() =>
        {
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result.Replace("\r\n", "\n"), Does.Contain(expectedSnippet.Replace("\r\n", "\n")));
        });
    }

    [Test]
    public void ExamplePrompts_ContainRequiredPatterns()
    {
        var skill = MakeSkill();

        Assert.Multiple(() =>
        {
            Assert.That(skill.ExamplePrompts, Has.Some.Contain("relative minor"));
            Assert.That(skill.ExamplePrompts, Has.Some.Contain("relative major"));
            Assert.That(skill.ExamplePrompts, Has.Some.Contain("Parallel minor"));
            Assert.That(skill.ExamplePrompts, Has.Some.Contain("Parallel major"));
            Assert.That(skill.ExamplePrompts, Has.Some.Contain("key signature"));
        });
    }

    [Test]
    public void Description_IsNotEmpty()
    {
        var skill = MakeSkill();
        Assert.That(skill.Description, Is.Not.Null.Or.Empty);
    }
}
