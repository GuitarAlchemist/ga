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
}
