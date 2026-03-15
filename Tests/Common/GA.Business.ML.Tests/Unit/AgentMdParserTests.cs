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
        Assert.That(result.Body, Does.Contain("Line three."));
    }
}
