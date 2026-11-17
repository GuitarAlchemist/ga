namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;

[TestFixture]
public class ProgressionMoodSkillTests
{
    private static ProgressionMoodSkill MakeSkill() => new();

    [Test]
    public void Metadata_ExposesExamplePrompts()
    {
        var skill = MakeSkill();
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(4));
        Assert.That(skill.Description, Does.Contain("darken").IgnoreCase
            .Or.Contain("brighten").IgnoreCase
            .Or.Contain("modal interchange").IgnoreCase);
    }

    [Test]
    public void CanHandle_AlwaysFalse_DispatchedSemantically()
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle("how do I make this progression sound darker"), Is.False);
    }

    [TestCase("How do I make this progression sound darker?")]
    [TestCase("Make this progression sound moodier")]
    [TestCase("How can I make my chords sound sadder?")]
    public async Task ExecuteAsync_DarkenQueries_ReturnDarkenTechniques(string prompt)
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync(prompt);

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("parallel minor").IgnoreCase);
        Assert.That(response.Result, Does.Contain("Phrygian").Or.Contain("Aeolian"));
        Assert.That(response.Evidence, Has.Some.Contains("parallel minor"));
    }

    [TestCase("Make my song sound brighter")]
    [TestCase("How to make a progression more uplifting")]
    public async Task ExecuteAsync_BrightenQueries_ReturnBrightenTechniques(string prompt)
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync(prompt);

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("parallel major").IgnoreCase);
        Assert.That(response.Result, Does.Contain("Lydian").Or.Contain("Mixolydian"));
    }

    [Test]
    public async Task ExecuteAsync_DarkenAnswerListsAtLeastFiveTechniques()
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync("how do I darken my progression");

        // The answer numbers techniques 1. 2. 3. 4. 5. — verify all five render.
        for (var i = 1; i <= 5; i++)
        {
            Assert.That(response.Result, Does.Contain($"{i}."),
                $"darken answer should number technique {i}");
        }
    }

    // Issue #889: the requested key was ignored and every example stayed in C.
    [TestCase("Suggest a sad chord progression in D minor and explain why it sounds sad", "Dm – Bb – Gm – A7")]
    [TestCase("Give me a sad progression in F# minor", "F#m – D – Bm – C#7")]
    [TestCase("Write a melancholy chord progression in the key of C minor", "Cm – Ab – Fm – G7")]
    [TestCase("Make a sad progression in B flat minor", "Bbm – Gb – Ebm – F7")]
    [TestCase("Sad chords in Em please", "Em – C – Am – B7")]
    [TestCase("In D minor, suggest a sad chord progression", "Dm – Bb – Gm – A7")]
    [TestCase("Key of G minor: something sad", "Gm – Eb – Cm – D7")]
    public async Task ExecuteAsync_SadRequestInAMinorKey_ProposesAConcreteProgressionInThatKey(
        string prompt, string expectedProgression)
    {
        var response = await MakeSkill().ExecuteAsync(prompt);

        Assert.That(response.Result, Does.Contain(expectedProgression));
        Assert.That(response.Result, Does.Contain("i – bVI – iv – V7"));
        Assert.That(response.Evidence, Has.Some.Contains("minor"));
    }

    [Test]
    public async Task ExecuteAsync_SadRequestInDMinor_ExplainsWhyAndTransposesTheTechniques()
    {
        var response = await MakeSkill().ExecuteAsync(
            "Suggest a sad chord progression in D minor and explain why it sounds sad");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("why it sounds sad").IgnoreCase);
            Assert.That(response.Result, Does.Contain("C#"), "the V7 leading tone of D minor");
            Assert.That(response.Result, Does.Contain("`D G A` → `D Gm A`"));
            Assert.That(response.Result, Does.Contain("`D Bb A`"));
            Assert.That(response.Result, Does.Contain("`D C G` instead of `D A G`"));
            Assert.That(response.Result, Does.Contain("Eb"), "bII of D");
            Assert.That(response.Result, Does.Not.Contain("`C F G`"));
        });
    }

    [Test]
    public async Task ExecuteAsync_SadRequestInAMajorKey_ProposesABorrowedIvProgression()
    {
        var response = await MakeSkill().ExecuteAsync("Make a sad progression in Eb major");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("Eb – Cm – Abm – Eb"));
            Assert.That(response.Result, Does.Contain("`Eb Db Ab` instead of `Eb Bb Ab`"));
        });
    }

    // Issue #889: flats were spelled accidental-first ("C bA G", "C bB F").
    [Test]
    public async Task ExecuteAsync_DarkenWithoutKey_SpellsFlatChordsLetterFirst()
    {
        var response = await MakeSkill().ExecuteAsync("How do I make this progression sound darker?");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("`C Ab G`"));
            Assert.That(response.Result, Does.Contain("`C Bb F` instead of `C G F`"));
            Assert.That(response.Result, Does.Not.Match(@"\bb[A-G]\b"), "no accidental-first chord symbols");
        });
    }

    [Test]
    public async Task ExecuteAsync_LowerCaseArticle_IsNotReadAsAKey()
    {
        var response = await MakeSkill().ExecuteAsync("How do I add a darker feel in a minor song?");

        Assert.That(response.Result, Does.Not.Contain("A sad progression in"));
    }

    [Test]
    public async Task ExecuteAsync_BrightenInAKey_UsesThatTonic()
    {
        var response = await MakeSkill().ExecuteAsync("Brighten my progression in E minor");

        Assert.That(response.Result, Does.Contain("(Em → E)"));
    }
}
