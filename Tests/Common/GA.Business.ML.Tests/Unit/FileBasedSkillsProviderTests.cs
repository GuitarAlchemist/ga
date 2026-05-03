namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.AgentFramework;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Moq;

[TestFixture]
public class FileBasedSkillsProviderTests
{
    private string _tmpRoot = string.Empty;

    [SetUp]
    public void Setup()
    {
        _tmpRoot = Path.Combine(Path.GetTempPath(), "ga-skills-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpRoot);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_tmpRoot))
        {
            try { Directory.Delete(_tmpRoot, recursive: true); }
            catch { /* best effort — Windows file locks under load */ }
        }
    }

    private string WriteSkill(string name, string frontmatter, string body)
    {
        var dir = Path.Combine(_tmpRoot, name);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "SKILL.md");
        File.WriteAllText(path, $"---\n{frontmatter}\n---\n\n{body}");
        return path;
    }

    private static AIContextProvider.InvokingContext UserContext(params ChatMessage[] messages) =>
        new(
            agent: new Mock<AIAgent>().Object,
            session: null,
            aiContext: new AIContext { Messages = [.. messages] });

    private static AIContextProvider.InvokingContext UserContext(string userMessage) =>
        UserContext(new ChatMessage(ChatRole.User, userMessage));

    [Test]
    public async Task InvokingAsync_TriggerMatches_ReturnsSkillBodyAsInstructions()
    {
        WriteSkill(
            "qa-architect",
            "Name: \"qa-architect\"\nDescription: \"Test\"\nTriggers:\n  - \"verdict\"\n  - \"qa-architect\"",
            "QA architect skill body — invoke when verdicts are involved.");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        var ctx = await provider.InvokingAsync(UserContext("Please emit a verdict for this PR"));

        Assert.That(ctx.Instructions, Is.Not.Null);
        Assert.That(ctx.Instructions, Does.Contain("QA architect skill body"));
        Assert.That(ctx.Instructions, Does.Contain("Skill: qa-architect"));
    }

    [Test]
    public async Task InvokingAsync_NoTriggerMatch_ReturnsEmptyContext()
    {
        WriteSkill(
            "transpose",
            "Name: \"transpose\"\nDescription: \"Test\"\nTriggers:\n  - \"transpose\"\n  - \"transposition\"",
            "Transpose skill body");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        var ctx = await provider.InvokingAsync(UserContext("What chord is this?"));

        Assert.That(ctx.Instructions, Is.Null.Or.Empty);
    }

    [Test]
    public async Task InvokingAsync_MultipleSkillsMatch_ConcatenatedInLoadOrder()
    {
        WriteSkill(
            "skill-a",
            "Name: \"a\"\nDescription: \"A\"\nTriggers:\n  - \"alpha\"",
            "Body of A.");
        WriteSkill(
            "skill-b",
            "Name: \"b\"\nDescription: \"B\"\nTriggers:\n  - \"alpha\"\n  - \"beta\"",
            "Body of B.");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        var ctx = await provider.InvokingAsync(UserContext("alpha and beta"));

        Assert.That(ctx.Instructions, Does.Contain("Body of A"));
        Assert.That(ctx.Instructions, Does.Contain("Body of B"));
        Assert.That(ctx.Instructions!.IndexOf("Body of A", StringComparison.Ordinal),
            Is.LessThan(ctx.Instructions.IndexOf("Body of B", StringComparison.Ordinal)),
            "skills must concatenate in load order so the agent sees a deterministic context");
    }

    [Test]
    public async Task InvokingAsync_NoUserMessage_ReturnsEmptyContext()
    {
        WriteSkill(
            "always-on",
            "Name: \"always-on\"\nDescription: \"x\"\nTriggers:\n  - \"anything\"",
            "Body");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        var ctx = await provider.InvokingAsync(
            UserContext(new ChatMessage(ChatRole.System, "system only")));

        Assert.That(ctx.Instructions, Is.Null.Or.Empty);
    }

    [Test]
    public async Task InvokingAsync_OnlyLatestUserMessageInspected()
    {
        WriteSkill(
            "transpose",
            "Name: \"transpose\"\nDescription: \"Test\"\nTriggers:\n  - \"transpose\"",
            "Body");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        // The earlier user turn mentioned the trigger; the latest does not.
        // Earlier-message scanning would cause stale matches across long sessions.
        var earlier = new ChatMessage(ChatRole.User, "transpose Am7 up a fifth");
        var assistant = new ChatMessage(ChatRole.Assistant, "Em7");
        var latest = new ChatMessage(ChatRole.User, "Now what is C major?");
        var ctx = await provider.InvokingAsync(UserContext(earlier, assistant, latest));

        Assert.That(ctx.Instructions, Is.Null.Or.Empty,
            "only the latest user message should drive trigger matching");
    }

    [Test]
    public void Constructor_MissingDirectory_LoadsZeroSkills()
    {
        var missing = Path.Combine(_tmpRoot, "does-not-exist");
        var provider = new FileBasedSkillsProvider(missing);

        Assert.That(provider.Skills, Is.Empty);
    }

    [Test]
    public void Constructor_MalformedFrontmatter_SilentlyIgnored()
    {
        // SkillMdLoader contract: files without triggers are skipped silently.
        // We verify the provider is resilient to that — it should load zero
        // skills from a malformed directory rather than throwing on construction.
        WriteSkill("malformed", "name: malformed\nthis-is-not-yaml: 'unterminated", "Body");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        Assert.That(provider.Skills, Is.Empty,
            "malformed SKILL.md files must not break provider construction");
    }

    [Test]
    public void Constructor_NullOrWhitespaceDirectory_Throws()
    {
        Assert.That(() => new FileBasedSkillsProvider(null!), Throws.InstanceOf<ArgumentNullException>());
        Assert.That(() => new FileBasedSkillsProvider(""),    Throws.ArgumentException);
        Assert.That(() => new FileBasedSkillsProvider("   "), Throws.ArgumentException);
    }

    [Test]
    public async Task InvokingAsync_TriggerMatchIsCaseInsensitive()
    {
        WriteSkill(
            "qa",
            "Name: \"qa\"\nDescription: \"x\"\nTriggers:\n  - \"VERDICT\"",
            "QA body");

        var provider = new FileBasedSkillsProvider(_tmpRoot);

        var ctx = await provider.InvokingAsync(UserContext("emit a verdict please"));

        Assert.That(ctx.Instructions, Does.Contain("QA body"));
    }

    [Test]
    public async Task InvokingAsync_LoadsRealQaArchitectSkillFromRepo()
    {
        // Smoke test against the canonical skill we just authored at the repo root.
        // Skip if the directory isn't present — keeps CI green if someone moves it.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        Assert.That(provider.Skills.Any(s => s.Name == "qa-architect"), Is.True,
            "skills/qa-architect/SKILL.md should be discovered with name 'qa-architect'");

        var ctx = await provider.InvokingAsync(UserContext("emit a qa verdict"));
        Assert.That(ctx.Instructions, Does.Contain("QA Architect Skill"));
    }

    [Test]
    public async Task InvokingAsync_LoadsProgressionCompletionSkillFromRepo()
    {
        // Seventh and final canary — completes the migration. Notable for
        // REUSING ga_key_identify rather than introducing a new MCP tool;
        // the deterministic detection is identical to KeyIdentification, only
        // the SKILL.md instructions differ (cadence picking vs key naming).
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var pc = provider.Skills.SingleOrDefault(s => s.Name == "progression-completion");
        Assert.That(pc, Is.Not.Null,
            "skills/progression-completion/SKILL.md must be discovered with name 'progression-completion'");

        Assert.That(pc!.Triggers.Any(t => t.Contains("what comes next")), Is.True);
        Assert.That(pc.Triggers.Any(t => t.Contains("finish this")), Is.True);
        Assert.That(pc.Triggers.Any(t => t.Contains("continue this")), Is.True);

        // Body must reference the REUSED ga_key_identify tool.
        Assert.That(pc.Body, Does.Contain("ga_key_identify"),
            "progression-completion SKILL.md reuses ga_key_identify (no new tool needed)");

        // The four-cadence catalog is load-bearing — every suggestion the LLM
        // makes must come from this list. If a row is dropped or a Roman
        // numeral typo'd, the LLM will silently teach wrong theory.
        Assert.That(pc.Body, Does.Contain("Authentic").And.Contain("Half")
                                  .And.Contain("Deceptive").And.Contain("Plagal"),
            "all four cadence types must be named in the body");
        Assert.That(pc.Body, Does.Contain("V → I").Or.Contain("V→I"),
            "authentic cadence Roman numeral must be present");
        Assert.That(pc.Body, Does.Contain("IV → I").Or.Contain("IV→I"),
            "plagal cadence Roman numeral must be present");

        // Hard constraints section guards the LLM against making up chords.
        Assert.That(pc.Body, Does.Contain("DiatonicSet"),
            "the 'every suggested chord must appear in DiatonicSet' constraint must survive");

        var ctx = await provider.InvokingAsync(UserContext("what comes next after C G Am"));
        Assert.That(ctx.Instructions, Does.Contain("ga_key_identify"));
    }

    [Test]
    public async Task InvokingAsync_LoadsKeyIdentificationSkillFromRepo()
    {
        // Sixth MCP-tool-driven canary, and the FIRST hybrid port (the C#
        // skill combined deterministic detection with LLM phrasing). The
        // SKILL.md body now carries the phrasing rules; the tool carries
        // the deterministic match.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var keyId = provider.Skills.SingleOrDefault(s => s.Name == "key-identification");
        Assert.That(keyId, Is.Not.Null,
            "skills/key-identification/SKILL.md must be discovered with name 'key-identification'");

        Assert.That(keyId!.Triggers.Any(t => t.Contains("what key is")), Is.True);
        Assert.That(keyId.Triggers.Any(t => t.Contains("identify the key")), Is.True);

        Assert.That(keyId.Body, Does.Contain("ga_key_identify"),
            "tool-driven SKILL.md must name the MCP tool the LLM should call");
        Assert.That(keyId.Body, Does.Contain("DiatonicSet").And.Contain("TopCandidates"),
            "body must document the structured result fields the LLM consumes");

        // Both phrasing paths must be documented (single candidate vs tied
        // relative pair) — the relative-pair ambiguity is the most common
        // case (any I-vi-IV-V progression), and missing the tie-handling
        // would make the LLM emit confidently-wrong single-key answers.
        Assert.That(keyId.Body, Does.Contain("Tied").Or.Contain("relative"),
            "body must explain how to handle relative-pair ties");

        var ctx = await provider.InvokingAsync(UserContext("what key is C Am F G in"));
        Assert.That(ctx.Instructions, Does.Contain("ga_key_identify"));
    }

    [Test]
    public async Task InvokingAsync_LoadsChordSubstitutionSkillFromRepo()
    {
        // Fifth MCP-tool-driven canary, and first SKILL.md to expose TWO
        // allowed-tools (ga_chord_substitutions + ga_chord_compare). Confirms
        // the loader doesn't drop tool-list entries past the first.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var sub = provider.Skills.SingleOrDefault(s => s.Name == "chord-substitution");
        Assert.That(sub, Is.Not.Null,
            "skills/chord-substitution/SKILL.md must be discovered with name 'chord-substitution'");

        Assert.That(sub!.Triggers.Any(t => t.Contains("substitut")), Is.True);
        Assert.That(sub.Triggers.Any(t => t.Contains("tritone")),    Is.True);

        // Both tool names must be present in the body so the LLM knows what's
        // available — single-tool SKILL.mds can omit one in error.
        Assert.That(sub.Body, Does.Contain("ga_chord_substitutions"),
            "tool-driven SKILL.md must name the substitutions tool");
        Assert.That(sub.Body, Does.Contain("ga_chord_compare"),
            "tool-driven SKILL.md must name the comparison tool");

        // Trigger fires on common phrasings.
        var ctx1 = await provider.InvokingAsync(UserContext("substitutions for Cmaj7"));
        Assert.That(ctx1.Instructions, Does.Contain("ga_chord_substitutions"));

        var ctx2 = await provider.InvokingAsync(UserContext("is G7 a tritone sub for Db7"));
        Assert.That(ctx2.Instructions, Does.Contain("ga_chord_compare"));
    }

    [Test]
    public async Task InvokingAsync_LoadsFretSpanSkillFromRepo()
    {
        // Fourth MCP-tool-driven canary. Confirms the template covers chord-
        // diagram-shaped inputs (different from interval/scale/chord which
        // take note-name strings).
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var fretSpan = provider.Skills.SingleOrDefault(s => s.Name == "fret-span");
        Assert.That(fretSpan, Is.Not.Null,
            "skills/fret-span/SKILL.md must be discovered with name 'fret-span'");

        Assert.That(fretSpan!.Triggers.Any(t => t.Contains("fret span")), Is.True);
        Assert.That(fretSpan.Triggers.Any(t => t.Contains("playability")), Is.True);

        Assert.That(fretSpan.Body, Does.Contain("ga_fret_span"),
            "tool-driven SKILL.md must name the MCP tool the LLM should call");
        Assert.That(fretSpan.Body, Does.Contain("diagram"),
            "body must document the tool's argument name");

        var ctx = await provider.InvokingAsync(UserContext("what's the fret span on x-3-2-0-1-0"));
        Assert.That(ctx.Instructions, Does.Contain("ga_fret_span"));
    }

    [Test]
    public async Task InvokingAsync_LoadsModesSkillFromRepo()
    {
        // Third catalog SKILL.md (after beginner-chords and progression-mood).
        // Confirms the catalog-style template works for the modes data — the
        // 7-row mode table is load-bearing and a missing row would silently
        // teach wrong theory.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var modes = provider.Skills.SingleOrDefault(s => s.Name == "modes");
        Assert.That(modes, Is.Not.Null,
            "skills/modes/SKILL.md must be discovered with name 'modes'");

        // All seven mode names must appear in the body — a dropped row is
        // the worst-case content drift.
        var expectedModes = new[] { "Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian" };
        foreach (var modeName in expectedModes)
        {
            Assert.That(modes!.Body, Does.Contain(modeName),
                $"modes SKILL.md body must include '{modeName}' row");
        }

        // Each mode's signature degree formula must be present (a missing 'b3'
        // or '#4' would silently teach wrong theory).
        Assert.That(modes!.Body, Does.Contain("1 2 3 4 5 6 7"),    "Ionian degrees");
        Assert.That(modes.Body,  Does.Contain("1 2 b3 4 5 6 b7"),  "Dorian degrees");
        Assert.That(modes.Body,  Does.Contain("1 b2 b3 4 5 b6 b7"), "Phrygian degrees");
        Assert.That(modes.Body,  Does.Contain("1 2 3 #4 5 6 7"),   "Lydian degrees");
        Assert.That(modes.Body,  Does.Contain("1 2 3 4 5 6 b7"),   "Mixolydian degrees");
        Assert.That(modes.Body,  Does.Contain("1 2 b3 4 5 b6 b7"), "Aeolian degrees");
        Assert.That(modes.Body,  Does.Contain("1 b2 b3 4 b5 b6 b7"), "Locrian degrees");

        // The mnemonic is what makes the catalog memorable; if it gets dropped,
        // the LLM loses the most useful single-line pedagogy in the skill.
        Assert.That(modes.Body, Does.Contain("I Don't Particularly Like Modes A Lot"),
            "the I-Don't-Particularly-Like-Modes-A-Lot mnemonic must survive into the catalog body");

        // Trigger fires on common phrasings.
        var ctx1 = await provider.InvokingAsync(UserContext("what are the modes of the major scale"));
        Assert.That(ctx1.Instructions, Does.Contain("Lydian"),
            "trigger 'modes of' / 'major scale modes' should inject the catalog body");

        var ctx2 = await provider.InvokingAsync(UserContext("explain dorian"));
        Assert.That(ctx2.Instructions, Does.Contain("Dorian"),
            "single-mode trigger ('dorian') should also fire and surface the catalog");
    }

    [Test]
    public async Task InvokingAsync_LoadsChordInfoSkillFromRepo()
    {
        // Third MCP-tool-driven canary. Confirms the template scales to a
        // skill with richer parsing semantics (chord symbol regex, quality
        // suffix normalization, enharmonic respelling).
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var chord = provider.Skills.SingleOrDefault(s => s.Name == "chord-info");
        Assert.That(chord, Is.Not.Null,
            "skills/chord-info/SKILL.md must be discovered with name 'chord-info'");

        Assert.That(chord!.Triggers.Any(t => t.Contains("chord notes")), Is.True);
        Assert.That(chord.Triggers.Any(t => t.Contains("notes in a")), Is.True);

        Assert.That(chord.Body, Does.Contain("ga_chord_info"),
            "tool-driven SKILL.md must name the MCP tool the LLM should call");
        Assert.That(chord.Body, Does.Contain("chordSymbol"),
            "body must document the tool's argument name so the LLM doesn't guess");

        var ctx = await provider.InvokingAsync(UserContext("what notes are in a Cmaj7"));
        Assert.That(ctx.Instructions, Does.Contain("ga_chord_info"));
    }

    [Test]
    public async Task InvokingAsync_LoadsScaleInfoSkillFromRepo()
    {
        // Second MCP-tool-driven canary. Confirms the tool-driven SKILL.md
        // pattern works for more than just the original interval canary.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var allNames = string.Join(", ", provider.Skills.Select(s => s.Name));
        var scale = provider.Skills.SingleOrDefault(s => s.Name == "scale-info");
        Assert.That(scale, Is.Not.Null,
            $"skills/scale-info/SKILL.md must be discovered with name 'scale-info'. Loaded skills: [{allNames}]");

        Assert.That(scale!.Triggers, Is.Not.Null,
            $"Triggers must be a populated list. Skill: {scale.Name}, FilePath: {scale.FilePath}");
        // Direct LINQ — NUnit's Has.Some.Contain has flaky overload resolution
        // for IReadOnlyList<string> in this fixture; the LINQ form is unambiguous.
        Assert.That(scale.Triggers.Any(t => t.Contains("notes in")), Is.True,
            $"Triggers should include a 'notes in' phrase but were [{string.Join(", ", scale.Triggers)}]");
        Assert.That(scale.Triggers.Any(t => t.Contains("scale of")), Is.True);

        Assert.That(scale.Body, Is.Not.Null.And.Not.Empty,
            $"Body must not be null/empty. Body length: {scale.Body?.Length ?? -1}");
        Assert.That(scale.Body!.Contains("ga_scale_get_notes"), Is.True,
            $"tool-driven SKILL.md must name the MCP tool the LLM should call. Body starts with: {scale.Body[..Math.Min(scale.Body.Length, 100)]}");
        Assert.That(scale.Body.Contains("root") && scale.Body.Contains("mode"), Is.True,
            "body must document the tool's argument names so the LLM doesn't guess");

        // Use a query phrasing that matches a trigger LITERALLY — "notes in"
        // is a substring of "list the notes in C major" but NOT of
        // "what notes ARE in C major" (because of the intervening "are").
        // Substring triggers can't express AND — for the broader "any keyword
        // matters" pattern we'd need a different routing layer.
        var ctx = await provider.InvokingAsync(UserContext("list the notes in C major"));
        Assert.That(ctx.Instructions, Does.Contain("ga_scale_get_notes"));
    }

    [Test]
    public async Task InvokingAsync_LoadsIntervalSkillFromRepo()
    {
        // Canary for the MCP-tool-driven authoring style — different shape from
        // the catalog SKILL.md files (BeginnerChords, ProgressionMood). The body
        // doesn't carry data; it instructs the LLM to call the MCP tool. If this
        // test fails the loader broke for tool-driven skills specifically.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var interval = provider.Skills.SingleOrDefault(s => s.Name == "interval");
        Assert.That(interval, Is.Not.Null,
            "skills/interval/SKILL.md must be discovered with name 'interval'");

        // Triggers must cover the main phrasings users employ.
        Assert.That(interval!.Triggers, Has.Some.Contain("interval between"));
        Assert.That(interval.Triggers,  Has.Some.Contain("distance from"));
        Assert.That(interval.Triggers,  Has.Some.Contain("how many semitones"));

        // Body must reference the tool by exact name so the LLM knows what to call.
        Assert.That(interval.Body, Does.Contain("ga_interval_compute"),
            "tool-driven SKILL.md must name the MCP tool the LLM should call");
        Assert.That(interval.Body, Does.Contain("lowerNote").And.Contain("upperNote"),
            "body must document the tool's argument names so the LLM doesn't guess");

        // Trigger match returns the body — same path as catalog skills.
        var ctx = await provider.InvokingAsync(UserContext("interval between C and G"));
        Assert.That(ctx.Instructions, Does.Contain("ga_interval_compute"));
    }

    [Test]
    public async Task InvokingAsync_LoadsProgressionMoodSkillFromRepo()
    {
        // Second-canary smoke test — once two catalog skills are wired, the
        // SKILL.md authoring story is no longer single-point-of-failure.
        // If this fails the most likely cause is frontmatter regression on
        // the file itself (drift from the C# implementation it replaces).
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var mood = provider.Skills.SingleOrDefault(s => s.Name == "progression-mood");
        Assert.That(mood, Is.Not.Null,
            "skills/progression-mood/SKILL.md must be discovered with name 'progression-mood'");
        Assert.That(mood!.Description, Does.Contain("modal interchange").IgnoreCase
            .Or.Contain("darken").IgnoreCase.Or.Contain("brighten").IgnoreCase,
            "Description should mention the two main mood-shift techniques");

        // Both branch headings must be present so the LLM can pick the right one.
        Assert.That(mood.Body, Does.Contain("Darken branch"));
        Assert.That(mood.Body, Does.Contain("Brighten branch"));

        // Five canonical darken techniques (parallel minor / Aeolian / Phrygian /
        // Dorian / bVII-for-V) — assert at least one signature phrase per item
        // so a careless edit can't drop a technique without the test catching it.
        Assert.That(mood.Body, Does.Contain("parallel minor"));
        Assert.That(mood.Body, Does.Contain("Aeolian"));
        Assert.That(mood.Body, Does.Contain("Phrygian"));
        Assert.That(mood.Body, Does.Contain("Dorian"));
        Assert.That(mood.Body, Does.Contain("bVII"));

        // Brighten branch — four techniques.
        Assert.That(mood.Body, Does.Contain("parallel major"));
        Assert.That(mood.Body, Does.Contain("Lydian"));
        Assert.That(mood.Body, Does.Contain("Mixolydian"));

        // Trigger match in either polarity should surface the body.
        var darkenCtx = await provider.InvokingAsync(UserContext("how do I make this progression sound darker?"));
        Assert.That(darkenCtx.Instructions, Does.Contain("Darken branch"),
            "darken-mood query should inject body via 'darker' / 'darken' triggers");

        var brightenCtx = await provider.InvokingAsync(UserContext("make my chords sound brighter please"));
        Assert.That(brightenCtx.Instructions, Does.Contain("Brighten branch"),
            "brighten-mood query should inject body via 'brighter' / 'brighten' triggers");
    }

    [Test]
    public async Task InvokingAsync_LoadsBeginnerChordsSkillFromRepo()
    {
        // Canary for SKILL.md authoring: the beginner-chords skill is the first
        // deterministic-catalog skill to be ported from C# to a markdown spec.
        // If this test fails, SKILL.md authoring is broken — block the migration
        // before porting more skills.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var provider = new FileBasedSkillsProvider(repoSkills!);

        var beginner = provider.Skills.SingleOrDefault(s => s.Name == "beginner-chords");
        Assert.That(beginner, Is.Not.Null,
            "skills/beginner-chords/SKILL.md must be discovered with name 'beginner-chords'");
        Assert.That(beginner!.Description, Does.Contain("first-position open"),
            "Description should describe the catalog scope");
        Assert.That(beginner.Triggers, Has.Some.Contain("beginner chord"));
        Assert.That(beginner.Triggers, Has.Some.Contain("first chord"));

        // Body must contain every diagram so the LLM cannot drop one.
        var expectedDiagrams = new[]
        {
            "x-3-2-0-1-0", "3-2-0-0-0-3", "x-x-0-2-3-2", "x-0-2-2-2-0",
            "0-2-2-1-0-0", "x-0-2-2-1-0", "0-2-2-0-0-0", "x-x-0-2-3-1",
        };
        foreach (var diagram in expectedDiagrams)
        {
            Assert.That(beginner.Body, Does.Contain(diagram),
                $"beginner-chords SKILL.md body must include diagram '{diagram}'");
        }

        // Provider hands the body back as Instructions when a trigger fires.
        var ctx = await provider.InvokingAsync(UserContext("show me some beginner chord shapes"));
        Assert.That(ctx.Instructions, Does.Contain("beginner-chords"));
        Assert.That(ctx.Instructions, Does.Contain("x-3-2-0-1-0"),
            "trigger match should inject the catalog body verbatim");
    }

    private static string? ResolveRepoSkillsDir()
    {
        var dir = new DirectoryInfo(Environment.GetEnvironmentVariable("GA_REPO_ROOT")
                                    ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "skills");
            if (Directory.Exists(candidate)) return candidate;
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return null;
            dir = dir.Parent;
        }
        return null;
    }
}
