namespace GA.Business.ML.Tests.Unit;

using Agents;

[TestFixture]
public class AgentMdParserTests
{
    [Test]
    public void TryParseContent_ValidAgent_ReturnsAgentMd()
    {
        const string content = """
            ---
            id: test-agent
            name: Test Agent
            role: test
            description: A test agent
            capabilities:
              - analysis
              - coding
            routing_keywords:
              - test
              - demo
            ---

            You are a test agent. Be helpful.
            """;

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("test-agent"));
        Assert.That(result.Name, Is.EqualTo("Test Agent"));
        Assert.That(result.Role, Is.EqualTo("test"));
        Assert.That(result.Description, Is.EqualTo("A test agent"));
        Assert.That(result.Capabilities, Has.Count.EqualTo(2));
        Assert.That(result.Capabilities, Contains.Item("analysis"));
        Assert.That(result.Capabilities, Contains.Item("coding"));
        Assert.That(result.RoutingKeywords, Has.Count.EqualTo(2));
        Assert.That(result.RoutingKeywords, Contains.Item("test"));
        Assert.That(result.Body, Does.Contain("test agent"));
    }

    [Test]
    public void TryParseContent_MissingFrontmatter_ReturnsNull()
    {
        const string content = "Just a plain markdown file.";

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParseContent_MissingId_ReturnsNull()
    {
        const string content = """
            ---
            name: NoId Agent
            role: noid
            ---

            Prompt text.
            """;

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParseContent_MinimalFields_DefaultsGracefully()
    {
        const string content = """
            ---
            id: minimal
            ---

            Minimal prompt.
            """;

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("minimal"));
        Assert.That(result.Name, Is.EqualTo("minimal")); // defaults to id
        Assert.That(result.Role, Is.EqualTo("minimal")); // defaults to id
        Assert.That(result.Description, Is.Empty);
        Assert.That(result.Capabilities, Is.Empty);
        Assert.That(result.RoutingKeywords, Is.Empty);
        Assert.That(result.UseCritique, Is.False);
        Assert.That(result.DelegatesTo, Is.Null);
    }

    [Test]
    public void TryParseContent_WithCritiqueAndDelegation_ParsesFlags()
    {
        const string content = """
            ---
            id: theory
            name: Theory Agent
            role: theory
            use_critique: true
            delegates_to: critic
            ---

            Theory prompt.
            """;

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.UseCritique, Is.True);
        Assert.That(result.DelegatesTo, Is.EqualTo("critic"));
    }

    [Test]
    public void TryParseContent_RoutingKeywords_AreLowercased()
    {
        const string content = """
            ---
            id: test
            routing_keywords:
              - CHORD
              - Scale
              - KEY
            ---

            Prompt.
            """;

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.RoutingKeywords, Is.EqualTo(new[] { "chord", "scale", "key" }));
    }

    [Test]
    public void TryParseContent_PreservesMultilineBody()
    {
        const string content = """
            ---
            id: multi
            ---

            Line one.

            Line two.

            Line three.
            """;

        var result = AgentMdParser.TryParseContent(content);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Body, Does.Contain("Line one."));
        Assert.That(result.Body, Does.Contain("Line two."));
        Assert.That(result.Body, Does.Contain("Line Three.").IgnoreCase);
    }
}

[TestFixture]
public class AgentMdRegistryTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agent_md_registry_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void TryGetByRole_ExistingAgent_ReturnsAgentMd()
    {
        File.WriteAllText(Path.Combine(_tempDir, "theory.md"), """
            ---
            id: theory
            name: Theory Agent
            role: theory
            description: Music theory expert
            ---

            You are a music theory specialist.
            """);

        var registry = new AgentMdRegistry(_tempDir);

        var result = registry.TryGetByRole("theory");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Role, Is.EqualTo("theory"));
        Assert.That(result.Body, Does.Contain("music theory specialist"));
    }

    [Test]
    public void TryGetByRole_CaseInsensitive()
    {
        File.WriteAllText(Path.Combine(_tempDir, "tab.md"), """
            ---
            id: tab
            role: tab
            ---

            Tab prompt.
            """);

        var registry = new AgentMdRegistry(_tempDir);

        Assert.That(registry.TryGetByRole("TAB"), Is.Not.Null);
        Assert.That(registry.TryGetByRole("Tab"), Is.Not.Null);
        Assert.That(registry.TryGetByRole("tab"), Is.Not.Null);
    }

    [Test]
    public void TryGetByRole_NonExistent_ReturnsNull()
    {
        var registry = new AgentMdRegistry(_tempDir);

        Assert.That(registry.TryGetByRole("nonexistent"), Is.Null);
    }

    [Test]
    public void GetAll_ReturnsAllDiscoveredAgents()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.md"), """
            ---
            id: alpha
            role: alpha
            ---

            Alpha prompt.
            """);

        File.WriteAllText(Path.Combine(_tempDir, "b.md"), """
            ---
            id: beta
            role: beta
            ---

            Beta prompt.
            """);

        var registry = new AgentMdRegistry(_tempDir);

        Assert.That(registry.GetAll(), Has.Count.EqualTo(2));
    }

    [Test]
    public void Registry_LaterPathOverridesEarlier()
    {
        var dir1 = Path.Combine(_tempDir, "repo");
        var dir2 = Path.Combine(_tempDir, "user");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        File.WriteAllText(Path.Combine(dir1, "theory.md"), """
            ---
            id: theory
            role: theory
            ---

            Repo-level prompt.
            """);

        File.WriteAllText(Path.Combine(dir2, "theory.md"), """
            ---
            id: theory
            role: theory
            ---

            User-level prompt.
            """);

        var registry = new AgentMdRegistry(dir1, dir2);
        var result = registry.TryGetByRole("theory");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Body, Does.Contain("User-level prompt"));
    }

    [Test]
    public void Registry_EmptyDirectory_ReturnsEmptyList()
    {
        var registry = new AgentMdRegistry(_tempDir);

        Assert.That(registry.GetAll(), Is.Empty);
    }
}
