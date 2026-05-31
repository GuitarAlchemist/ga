namespace GuitarAlchemist.Registry.Tests;

using System.Text.Json.Nodes;
using GuitarAlchemist.Registry;

[TestFixture]
public class RegistryDiscoveryTests
{
    [GaSkill("test.echo", "test", Description = "Echoes the input JSON node.")]
    public static JsonNode Echo(JsonNode input) => input;

    [GaSkill("test.echo.tagged", "test", GovernanceTags = new[] { "deterministic", "pure" })]
    public static JsonNode EchoTagged(JsonNode input) => input;

    // Intentionally NOT annotated — must not appear in Registry.All.
    public static JsonNode Unannotated(JsonNode input) => input;

    [Test]
    public void Discover_FindsAnnotatedMethod()
    {
        var skill = Registry.ByName("test.echo");

        Assert.That(skill, Is.Not.Null, "Expected test.echo to be discovered");
        Assert.That(skill!.Domain, Is.EqualTo("test"));
        Assert.That(skill.Description, Is.EqualTo("Echoes the input JSON node."));
    }

    [Test]
    public void Discover_InvokesHandler_Echoes()
    {
        var skill = Registry.ByName("test.echo");
        Assert.That(skill, Is.Not.Null);

        var input = JsonNode.Parse("""{"hello":"world","n":42}""")!;
        var result = skill!.Handler(input);

        Assert.That(result.ToJsonString(), Is.EqualTo(input.ToJsonString()));
    }

    [Test]
    public void Discover_IgnoresUnannotated()
    {
        var names = Registry.All.Select(s => s.Name).ToList();

        Assert.That(names, Does.Not.Contain("Unannotated"));
        Assert.That(names, Does.Not.Contain("test.unannotated"));
    }

    [Test]
    public void Discover_CapturesGovernanceTags()
    {
        var skill = Registry.ByName("test.echo.tagged");

        Assert.That(skill, Is.Not.Null);
        Assert.That(skill!.GovernanceTags, Is.EquivalentTo(new[] { "deterministic", "pure" }));
    }

    [Test]
    public void Discover_DefaultSchema_IsEmptyJsonObject()
    {
        var skill = Registry.ByName("test.echo");
        Assert.That(skill, Is.Not.Null);

        var schema = skill!.Schema();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema.Count, Is.EqualTo(0));
    }
}
