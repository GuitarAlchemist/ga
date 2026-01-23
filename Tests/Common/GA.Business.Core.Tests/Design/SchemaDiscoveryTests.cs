namespace GA.Domain.Core.Tests.Design;

using System.Linq;
using GA.Domain.Core.Design;
using GA.Domain.Core.Theory.Atonal;
using Theory.Harmony;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

[TestFixture]
public class SchemaDiscoveryTests
{
    private SchemaDiscoveryService _service;
    [SetUp]
    public void Setup()
    {
        _service = new SchemaDiscoveryService();
    }
    [Test]
    public void DiscoverSchema_ShouldFindAnnotatedCoreTypes()
    {
        var coreAssembly = typeof(PitchClassSet).Assembly;
        var schema = _service.DiscoverSchema(coreAssembly).ToList();
        Assert.That(schema.Any(t => t.Name == nameof(PitchClassSet)), "PitchClassSet not found in schema");
        Assert.That(schema.Any(t => t.Name == nameof(Chord)), "Chord not found in schema");
        Assert.That(schema.Any(t => t.Name == nameof(Scale)), "Scale not found in schema");
    }
    [Test]
    public void GetTypeSchema_PitchClassSet_ShouldHaveCorrectRelationships()
    {
        var info = _service.GetTypeSchema(typeof(PitchClassSet));
        Assert.That(info.Relationships.Any(r => r.TargetType == typeof(IntervalClassVector) && r.Type == RelationshipType.IsChildOf));
        Assert.That(info.Relationships.Any(r => r.TargetType == typeof(ModalFamily) && r.Type == RelationshipType.IsChildOf));
    }
    [Test]
    public void GetTypeSchema_Chord_ShouldHaveInvariants()
    {
        var info = _service.GetTypeSchema(typeof(Chord));
        Assert.That(info.Invariants.Any(i => i.Description.Contains("root note")), "Chord invariant missing");
    }
}