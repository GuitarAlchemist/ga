namespace GA.Business.ML.Tests.Unit;

using System.Text.RegularExpressions;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Core.Theory.Tonal;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The keyword predicates the offline fallback of <c>SemanticIntentRouter</c> consults
/// when embeddings are unavailable: a relative-key question must reach
/// <see cref="RelativeKeySkill"/>, not <see cref="ScaleInfoSkill"/> (which would list
/// the notes of C major instead of answering "A minor").
/// </summary>
[TestFixture]
public class RelativeKeySkillTests
{
    private static RelativeKeySkill MakeSkill() => new(NullLogger<RelativeKeySkill>.Instance);

    [TestCase("What is the relative minor of C major?")]
    [TestCase("Relative major of A minor")]
    [TestCase("What is the parallel minor of F major")]
    [TestCase("Parallel major of D minor")]
    [TestCase("How many sharps in D major")]
    [TestCase("Key signature of B minor")]
    public void CanHandle_KeyRelationQuestions_ReturnsTrue(string prompt) =>
        Assert.That(MakeSkill().CanHandle(prompt), Is.True, prompt);

    [TestCase("What notes are in C major?")]
    [TestCase("Why does a ii-V-I sound resolved?")]
    [TestCase("")]
    public void CanHandle_OtherQuestions_ReturnsFalse(string prompt) =>
        Assert.That(MakeSkill().CanHandle(prompt), Is.False, prompt);

    [Test]
    public void ScaleInfoSkill_YieldsRelativeKeyQuestion()
    {
        var scaleInfo = new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(scaleInfo.CanHandle("What is the relative minor of C major?"), Is.False);
            Assert.That(scaleInfo.CanHandle("What notes are in C major?"), Is.True,
                "ScaleInfo keeps its own questions");
        });
    }

    [Test]
    public async Task ExecuteAsync_RelativeMinorOfCMajor_IsAMinor()
    {
        var response = await MakeSkill().ExecuteAsync("What is the relative minor of C major?");

        Assert.That(response.Result, Does.Contain("**Am**"));
    }

    [TestCase("What is the relative minor of Eb major", "The relative minor of **Eb major** is **Cm**.\n\nBoth share the same key signature (3 flats). Same notes, different tonal center — the relative minor starts on the 6th degree of the major scale.")]
    [TestCase("relative minor of Bb major", "The relative minor of **Bb major** is **Gm**.\n\nBoth share the same key signature (2 flats). Same notes, different tonal center — the relative minor starts on the 6th degree of the major scale.")]
    [TestCase("What is the relative major of Am", "The relative major of **A minor** is **C major**.\n\nBoth share the same key signature (no sharps or flats). Same notes, different tonal center — the relative major starts on the 3rd degree of the minor scale.")]
    [TestCase("Parallel minor of C major", "The parallel minor of **C major** is **C minor**.\n\nSame root note (**C**) but different scales — the parallel minor lowers the 3rd, 6th, and 7th degrees. C major has no sharps or flats; C minor has 3 flats (three positions counter-clockwise on the circle of fifths).")]
    [TestCase("What is the parallel major of Am", "The parallel major of **A minor** is **A major**.\n\nSame root note (**A**) but different scales — the parallel major raises the 3rd, 6th, and 7th degrees. The parallel major sits three positions clockwise on the circle of fifths.")]
    [TestCase("How many flats in F major", "**F major** has 1 flat.")]
    [TestCase("How many sharps in D major", "**D major** has 2 sharps.")]
    [TestCase("What's the key signature of E major", "**E major** has 4 sharps.")]
    // Issue #769: keys at the edge of the circle, and keys past it.
    [TestCase("How many flats in Cb major", "**Cb major** has 7 flats.")]
    [TestCase("How many sharps in C# major", "**C# major** has 7 sharps.")]
    [TestCase("How many sharps in G# minor", "**G# minor** has 5 sharps.")]
    [TestCase("How many sharps in G# major", "**G# major** has 8 sharps. G# major is a theoretical key, usually written as **Ab major** (4 flats).")]
    [TestCase("How many flats in Db minor", "**Db minor** has 8 flats. Db minor is a theoretical key, usually written as **C# minor** (4 sharps).")]
    [TestCase("What is the parallel minor of G# major", "G# major has 8 sharps; G# minor has 5 sharps (three positions counter-clockwise on the circle of fifths). G# major is a theoretical key, usually written as **Ab major** (4 flats).")]
    [TestCase("What is the parallel minor of Gb major", "Gb major has 6 flats; Gb minor has 9 flats (three positions counter-clockwise on the circle of fifths). Gb minor is a theoretical key, usually written as **F# minor** (3 sharps).")]
    [TestCase("What is the relative minor of G# major", "**G# major** is a theoretical key (8 sharps), usually written as **Ab major** (4 flats). The relative minor of Ab major is **F minor**.")]
    [TestCase("What is the relative major of Fb minor", "**Fb minor** is a theoretical key (11 flats), usually written as **E minor** (1 sharp). The relative major of E minor is **G major**.")]
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

    private static IEnumerable<TestCaseData> StandardKeys() =>
        Key.Items.Select(k => new TestCaseData(k.Root.ToString(), k.KeyMode == KeyMode.Minor)
            .SetArgDisplayNames($"{k.Root} {(k.KeyMode == KeyMode.Minor ? "minor" : "major")}"));

    // Issue #769: ScaleInfoSkill took the first key of the other mode with the same
    // pitch-class set, and Key.Items lists the flat twin first — "B major … Relative
    // minor: Ab minor" while this skill answered G#m.
    [TestCaseSource(nameof(StandardKeys))]
    public async Task ScaleInfoSkill_AndRelativeKeySkill_NameTheSameRelativeKey(string root, bool isMinor)
    {
        var mode = isMinor ? "minor" : "major";
        var scaleInfo = await new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance)
            .ExecuteAsync($"What notes are in {root} {mode}?");
        var relative = await MakeSkill()
            .ExecuteAsync($"What is the relative {(isMinor ? "major" : "minor")} of {root} {mode}?");

        var fromScaleInfo = Regex.Match(scaleInfo.Result, @"Relative (?:minor|major): (?<key>[^.]+)\.").Groups["key"].Value;
        // RelativeKeySkill writes a relative minor as "G#m".
        var fromRelative = Regex.Match(relative.Result, @" is \*\*(?<key>[^*]+)\*\*\.").Groups["key"].Value;
        if (fromRelative.EndsWith('m')) fromRelative = $"{fromRelative[..^1]} minor";

        Assert.Multiple(() =>
        {
            Assert.That(fromScaleInfo, Is.Not.Empty, scaleInfo.Result);
            Assert.That(fromScaleInfo, Is.EqualTo(fromRelative), $"{scaleInfo.Result} | {relative.Result}");
        });
    }

    private static IEnumerable<TestCaseData> SpelledKeys() =>
        from letter in new[] { "C", "D", "E", "F", "G", "A", "B" }
        from accidental in new[] { "", "#", "b" }
        from mode in new[] { "major", "minor" }
        select new TestCaseData(letter + accidental, mode).SetArgDisplayNames($"{letter}{accidental} {mode}");

    // Issue #769: a root missing from the circle-of-fifths tables read as 0 accidentals,
    // so G# major had "no sharps or flats"; Cb was refused outright.
    [TestCaseSource(nameof(SpelledKeys))]
    public async Task KeySignature_IsNoneOnlyForCMajorAndAMinor(string root, string mode)
    {
        var response = await MakeSkill().ExecuteAsync($"How many accidentals in {root} {mode}?");
        var none = (root, mode) is ("C", "major") or ("A", "minor");
        // B# major may still name its written twin, C major, as having none.
        var saysNone = $"**{root} {mode}** has no sharps or flats";

        Assert.Multiple(() =>
        {
            Assert.That(response.Confidence, Is.EqualTo(1.0f), response.Result);
            Assert.That(response.Result, Does.StartWith($"**{root} {mode}** has "));
            Assert.That(response.Result, none ? Does.Contain(saysNone) : Does.Not.Contain(saysNone));
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
