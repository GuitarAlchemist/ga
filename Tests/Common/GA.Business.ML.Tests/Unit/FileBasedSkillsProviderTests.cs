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
