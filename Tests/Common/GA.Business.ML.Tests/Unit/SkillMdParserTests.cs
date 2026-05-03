namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Skills;

[TestFixture]
public class SkillMdParserTests
{
    // ── Happy path ────────────────────────────────────────────────────────────

    [Test]
    public void TryParseContent_ValidFrontmatter_ReturnsParsedSkill()
    {
        const string content = """
            ---
            Name: "GA Chords"
            Description: "Parse and transpose chords"
            Triggers:
              - "parse chord"
              - "transpose"
            ---
            # GA Chords Skill
            Use me to parse chords.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name,        Is.EqualTo("GA Chords"));
        Assert.That(result.Description,  Is.EqualTo("Parse and transpose chords"));
        Assert.That(result.Triggers,     Has.Count.EqualTo(2));
        Assert.That(result.Triggers,     Contains.Item("parse chord"));
        Assert.That(result.Triggers,     Contains.Item("transpose"));
        Assert.That(result.Body,         Contains.Substring("Use me to parse chords."));
    }

    [Test]
    public void TryParseContent_BodyStartsAfterClosingDelimiter()
    {
        const string content = """
            ---
            Name: "Test"
            ---
            Body content here.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result!.Body.TrimStart(), Does.StartWith("Body content here."));
        Assert.That(result.Body, Does.Not.Contain("---"));
    }

    [Test]
    public void TryParseContent_FilePath_PreservedOnRecord()
    {
        const string content = "---\nName: Test\n---\nbody";

        var result = SkillMdParser.TryParseContent(content, "/some/path/SKILL.md");

        Assert.That(result!.FilePath, Is.EqualTo("/some/path/SKILL.md"));
    }

    // ── Triggers filtering ────────────────────────────────────────────────────

    [Test]
    public void TryParseContent_NoTriggersField_ReturnsSkillWithEmptyTriggers()
    {
        const string content = """
            ---
            Name: "No Triggers Skill"
            Description: "Claude Code guide only"
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null, "Skill is still valid without triggers");
        Assert.That(result!.Triggers, Is.Empty);
    }

    [Test]
    public void TryParseContent_EmptyTriggersList_ReturnsSkillWithEmptyTriggers()
    {
        const string content = """
            ---
            Name: "Empty Triggers"
            Triggers: []
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Triggers, Is.Empty);
    }

    // ── Missing required fields ────────────────────────────────────────────────

    [Test]
    public void TryParseContent_MissingName_ReturnsNull()
    {
        const string content = """
            ---
            Description: "No name"
            Triggers:
              - "foo"
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParseContent_EmptyName_ReturnsNull()
    {
        const string content = "---\nName: \"\"\n---\nbody";

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Null);
    }

    // ── Missing frontmatter ───────────────────────────────────────────────────

    [Test]
    public void TryParseContent_NoFrontmatterDelimiter_ReturnsNull()
    {
        const string content = "# Just a markdown file\nNo frontmatter here.";

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParseContent_UnclosedFrontmatter_ReturnsNull()
    {
        const string content = "---\nName: Test\nNo closing delimiter";

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParseContent_EmptyContent_ReturnsNull()
    {
        var result = SkillMdParser.TryParseContent(string.Empty);

        Assert.That(result, Is.Null);
    }

    // ── Default file path ─────────────────────────────────────────────────────

    [Test]
    public void TryParseContent_NoFilePath_UsesMemoryDefault()
    {
        const string content = "---\nName: Test\n---\nbody";

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result!.FilePath, Is.EqualTo("<memory>"));
    }

    // ── Hardening: body cap + min trigger length ──────────────────────────────

    [Test]
    public void TryParseContent_BodyOverMaxBodyCharacters_RejectsFile()
    {
        // A SKILL.md whose body exceeds the cap is a prompt-injection
        // amplifier — a malicious skill author could shove a megabyte of
        // adversarial text into every triggered LLM call. Parser must
        // reject the file outright.
        var bigBody = new string('A', SkillMdParser.MaxBodyCharacters + 1);
        var content = $"---\nName: \"toobig\"\nTriggers:\n  - \"toobig\"\n---\n{bigBody}";

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Null,
            "SKILL.md whose body exceeds MaxBodyCharacters must be rejected outright");
    }

    [Test]
    public void TryParseContent_BodyAtCapBoundary_AcceptsFile()
    {
        // Off-by-one defense: body of EXACTLY MaxBodyCharacters must still
        // parse — "exceeds the cap" means strictly greater than.
        var atCapBody = new string('A', SkillMdParser.MaxBodyCharacters);
        var content = $"---\nName: \"atcap\"\nTriggers:\n  - \"atcap\"\n---\n{atCapBody}";

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null,
            "body at exactly MaxBodyCharacters must be accepted (cap is exclusive)");
    }

    [Test]
    public void TryParseContent_TriggersBelowMinLength_AreDropped()
    {
        // "a" / "an" would match every English message, shadowing every
        // more-specific skill. Drop these silently rather than rejecting
        // the file — most violations are typos, not hostile.
        const string content = """
            ---
            Name: "shorty"
            Triggers:
              - "a"
              - "an"
              - "but"
              - "valid trigger"
              - "another good one"
            ---
            body
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result,                Is.Not.Null);
        Assert.That(result!.Triggers,      Has.Count.EqualTo(2),
            "triggers shorter than MinTriggerLength must be dropped silently");
        Assert.That(result.Triggers,       Contains.Item("valid trigger"));
        Assert.That(result.Triggers,       Contains.Item("another good one"));
        Assert.That(result.Triggers.All(t => t.Length >= SkillMdParser.MinTriggerLength), Is.True);
    }

    [Test]
    public void TryParseContent_AllTriggersDropped_StillParsesButYieldsEmptyTriggers()
    {
        // SkillMdLoader filters skills with empty Triggers downstream — the
        // parser stage just has to faithfully report what survived.
        const string content = """
            ---
            Name: "all-short"
            Triggers:
              - "a"
              - "x"
            ---
            body
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result,           Is.Not.Null);
        Assert.That(result!.Triggers, Is.Empty,
            "if all triggers are short, the survivor list is empty (downstream filter handles registration)");
    }
}
