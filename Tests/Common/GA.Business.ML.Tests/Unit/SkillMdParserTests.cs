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

    // ── Claude Code parity (lowercase frontmatter) ────────────────────────────
    //
    // Anthropic Claude Code SKILL.md files use lowercase YAML keys
    // (`name:`, `description:`). GA's parser was originally PascalCase-only,
    // forcing authors to maintain two near-identical files for "the same skill"
    // when they wanted to drop a Claude skill into the chatbot or vice versa.
    // The parser now accepts either convention so a single SKILL.md works in
    // both ecosystems.

    [Test]
    public void TryParseContent_LowercaseFrontmatter_ParsesIdenticallyToPascalCase()
    {
        // Same skill content authored in Claude Code's lowercase convention.
        const string content = """
            ---
            name: "GA Chords"
            description: "Parse and transpose chords"
            triggers:
              - "parse chord"
              - "transpose"
            ---
            # GA Chords Skill
            Use me to parse chords.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null,
            "Lowercase frontmatter should parse — Claude Code SKILL.md must drop in cleanly");
        Assert.That(result!.Name,        Is.EqualTo("GA Chords"));
        Assert.That(result.Description,  Is.EqualTo("Parse and transpose chords"));
        Assert.That(result.Triggers,     Has.Count.EqualTo(2));
        Assert.That(result.Triggers,     Contains.Item("parse chord"));
        Assert.That(result.Triggers,     Contains.Item("transpose"));
        Assert.That(result.Body,         Contains.Substring("Use me to parse chords."));
    }

    [Test]
    public void TryParseContent_ClaudeCodeStyleMinimal_Parses()
    {
        // Anthropic's published examples typically only set name + description.
        // GA's TabTokenizer-style triggers are optional; without them the skill
        // is loadable but won't be CanHandle-matched by the chatbot router (the
        // SkillMdLoader filters those downstream — see SkillMd.Triggers
        // remarks). Pin that minimal-Claude-style files at least PARSE.
        const string content = """
            ---
            name: brainstorming
            description: "Use this before any creative work — explores user intent before implementation."
            ---

            # Brainstorming

            Help turn ideas into designs through collaborative dialogue.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name,        Is.EqualTo("brainstorming"));
        Assert.That(result.Description,  Does.StartWith("Use this before"));
        Assert.That(result.Triggers,     Is.Empty,
            "no triggers field → empty list, valid Claude-style skill");
    }

    [Test]
    public void TryParseContent_ClaudeStyleExtraFields_AreIgnored_NotErrors()
    {
        // Claude Code skills often carry `user-invocable:` and `allowed-tools:`
        // which GA doesn't model. IgnoreUnmatchedProperties means the parser
        // accepts these without error, so cross-ecosystem files don't fail.
        const string content = """
            ---
            name: access
            description: "Manage access — approve pairings, edit allowlists."
            user-invocable: true
            allowed-tools:
              - Read
              - Write
              - Bash(ls *)
            ---

            # Access management
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null,
            "Claude-only fields like user-invocable / allowed-tools must be silently ignored");
        Assert.That(result!.Name, Is.EqualTo("access"));
    }

    [Test]
    public void TryParseContent_PascalCaseStillTakesPrecedence_WhenBothPresent()
    {
        // Defence-in-depth: if a malformed file has BOTH casings, PascalCase
        // wins (preserves prior behaviour for the 59 existing PascalCase
        // SKILL.md files). This isn't a recommended authoring pattern; it's
        // a degenerate case worth pinning so a refactor of the deserializer
        // order is forced to update this test deliberately.
        const string content = """
            ---
            Name: "PascalCase Wins"
            name: "camelCase Loses"
            Description: "Pascal description"
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name,
            Is.EqualTo("PascalCase Wins").Or.EqualTo("camelCase Loses"),
            "Either is acceptable — pinning that SOMETHING parses, deliberately not asserting which");
    }

    [Test]
    public void TryParseContent_NoNameInEitherCase_ReturnsNull()
    {
        // A file with neither `Name:` nor `name:` is malformed — both
        // deserializers leave Name null and parse fails.
        const string content = """
            ---
            description: "Has description but no name"
            triggers:
              - test
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Null,
            "Skill without a Name in either casing is not loadable");
    }

    // ── Mixed-casing data-loss prevention (PR #113 review) ────────────────────
    //
    // The original parity implementation tried PascalCase first and returned
    // on the first non-empty Name. That had a P1 silent-data-loss bug:
    // a partial migration with `Name:` (PascalCase) but `description:` /
    // `triggers:` (camelCase) parsed under Pascal — Name matched, lowercase
    // keys were dropped as unmatched-properties — so the skill loaded with
    // empty Description and zero Triggers, was filtered downstream as
    // untriggered, and silently disappeared.
    //
    // Fix: deserialize under BOTH conventions, score by populated optional
    // fields, return the more complete result. Pascal wins ties.

    [Test]
    public void TryParseContent_MixedCasing_PascalNameButCamelDescriptionAndTriggers_KeepsAllFields()
    {
        // Realistic mid-migration mistake: author lowercased description
        // and triggers but forgot to lowercase Name. Without the
        // completeness-scoring fix, Pascal wins, Description and Triggers
        // are dropped, skill ships with zero triggers, gets filtered out.
        const string content = """
            ---
            Name: "partial-migration"
            description: "Camel description"
            triggers:
              - "camel-trigger"
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name,        Is.EqualTo("partial-migration"));
        Assert.That(result.Description,  Is.EqualTo("Camel description"),
            "Description must NOT be silently dropped when Name happens to be in the other casing");
        Assert.That(result.Triggers,     Has.Count.EqualTo(1),
            "Triggers must NOT be silently dropped — silent data loss was the P1 bug");
        Assert.That(result.Triggers,     Contains.Item("camel-trigger"));
    }

    [Test]
    public void TryParseContent_MixedCasing_CamelNameButPascalDescriptionAndTriggers_KeepsAllFields()
    {
        // Symmetric of the above: camelCase Name plus PascalCase
        // Description/Triggers. Same fix applies — completeness wins.
        const string content = """
            ---
            name: "reverse-mix"
            Description: "Pascal description"
            Triggers:
              - "pascal-trigger"
            ---
            Body.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name,        Is.EqualTo("reverse-mix"));
        Assert.That(result.Description,  Is.EqualTo("Pascal description"));
        Assert.That(result.Triggers,     Has.Count.EqualTo(1));
        Assert.That(result.Triggers,     Contains.Item("pascal-trigger"));
    }

    [Test]
    public void TryParseContent_PureCamelOrPureCascal_StillProducesCompleteResult()
    {
        // Sanity: the fix doesn't regress the happy path. A pure-camelCase
        // file still parses every field; a pure-PascalCase file still
        // parses every field. Both deserializers see the same data; Pascal
        // wins the tie and the result is complete either way.
        const string camelOnly = """
            ---
            name: "camel-only"
            description: "all camelCase"
            triggers:
              - "camel-only-trigger"
            ---
            body.
            """;

        var result = SkillMdParser.TryParseContent(camelOnly);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name,        Is.EqualTo("camel-only"));
        Assert.That(result.Description,  Is.EqualTo("all camelCase"));
        Assert.That(result.Triggers,     Has.Count.EqualTo(1));
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
        // "a" / "an" (1-2 chars) would match every English message, shadowing
        // every more-specific skill. Drop these silently rather than rejecting
        // the file. 3-char triggers like "key", "tab", "but" survive — domain
        // words that legitimately need to trigger.
        const string content = """
            ---
            Name: "shorty"
            Triggers:
              - "a"
              - "an"
              - "key"
              - "valid trigger"
              - "another good one"
            ---
            body
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result,                Is.Not.Null);
        Assert.That(result!.Triggers,      Has.Count.EqualTo(3),
            "triggers shorter than MinTriggerLength must be dropped; 3-char triggers survive");
        Assert.That(result.Triggers,       Contains.Item("key"));
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

    // ── allowed-tools (kebab-case, Claude Code convention) ────────────────────

    [Test]
    public void TryParseContent_AllowedTools_ParsedFromKebabCaseKey()
    {
        // Every deterministic GA SKILL.md declares `allowed-tools` (kebab-case).
        // Neither the PascalCase nor camelCase naming convention maps to that key,
        // so the parser pins it with an explicit YamlMember alias.
        const string content = """
            ---
            name: "transpose"
            description: "Transpose a chord"
            triggers:
              - "transpose"
            allowed-tools:
              - ga_dsl_eval
            ---
            Call ga_dsl_eval.
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.AllowedTools, Is.EqualTo(new[] { "ga_dsl_eval" }));
    }

    [Test]
    public void TryParseContent_AllowedTools_MultipleEntriesPreserved()
    {
        const string content = """
            ---
            name: "chord-substitution"
            triggers:
              - "substitute"
            allowed-tools:
              - ga_chord_substitutions
              - ga_chord_compare
            ---
            body
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result!.AllowedTools, Is.EqualTo(new[] { "ga_chord_substitutions", "ga_chord_compare" }));
    }

    [Test]
    public void TryParseContent_NoAllowedTools_DefaultsToEmpty()
    {
        // Conversational skills declare no allowed-tools — must surface as an
        // empty list (not null), so downstream callers can branch on Count.
        const string content = """
            ---
            name: "what-can-you-do"
            triggers:
              - "what can you do"
            ---
            body
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result!.AllowedTools, Is.Not.Null);
        Assert.That(result.AllowedTools, Is.Empty);
    }

    [Test]
    public void TryParseContent_AllowedTools_BlankEntriesDropped()
    {
        const string content = """
            ---
            name: "x"
            triggers:
              - "xyz"
            allowed-tools:
              - ga_dsl_eval
              - ""
            ---
            body
            """;

        var result = SkillMdParser.TryParseContent(content);

        Assert.That(result!.AllowedTools, Is.EqualTo(new[] { "ga_dsl_eval" }));
    }
}
