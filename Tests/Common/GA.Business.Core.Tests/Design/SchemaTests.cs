namespace GA.Domain.Core.Tests.Design;

using System.Reflection;
using GA.Domain.Core.Design;
using GA.Domain.Core.Theory.Atonal;
using NUnit.Framework;

[TestFixture]
public class SchemaTests
{
    [Test]
    public void IntervalClassVector_HasRelationshipAttributes()
    {
        var type = typeof(IntervalClassVector);
        var attributes = type.GetCustomAttributes<DomainRelationshipAttribute>().ToList();
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(attributes.Any(a => a.TargetType == typeof(PitchClassSet) && a.Type == RelationshipType.IsParentOf));
        Assert.That(attributes.Any(a => a.TargetType == typeof(ModalFamily) && a.Type == RelationshipType.Groups));
    }
    [Test]
    public void PitchClassSet_HasRelationshipAttributes()
    {
        var type = typeof(PitchClassSet);
        var attributes = type.GetCustomAttributes<DomainRelationshipAttribute>().ToList();
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(attributes.Any(a => a.TargetType == typeof(IntervalClassVector) && a.Type == RelationshipType.IsChildOf));
        Assert.That(attributes.Any(a => a.TargetType == typeof(ModalFamily) && a.Type == RelationshipType.IsChildOf));
    }
    [Test]
    public void ModalFamily_HasRelationshipAttributes()
    {
        var type = typeof(ModalFamily);
        var attributes = type.GetCustomAttributes<DomainRelationshipAttribute>().ToList();
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(attributes.Any(a => a.TargetType == typeof(PitchClassSet) && a.Type == RelationshipType.Groups));
        Assert.That(attributes.Any(a => a.TargetType == typeof(IntervalClassVector) && a.Type == RelationshipType.IsChildOf));
    }
    [Test]
    public void ForteNumber_HasRelationshipAttributes()
    {
        var type = typeof(ForteNumber);
        var attributes = type.GetCustomAttributes<DomainRelationshipAttribute>().ToList();
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(attributes.Any(a => a.TargetType == typeof(PitchClassSet) && a.Type == RelationshipType.IsMetadataFor));
        Assert.That(attributes.Any(a => a.TargetType == typeof(SetClass) && a.Type == RelationshipType.IsChildOf));
    }
    [Test]
    public void SetClass_HasRelationshipAttributes()
    {
        var type = typeof(SetClass);
        var attributes = type.GetCustomAttributes<DomainRelationshipAttribute>().ToList();
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(attributes.Any(a => a.TargetType == typeof(PitchClassSet) && a.Type == RelationshipType.Groups));
        Assert.That(attributes.Any(a => a.TargetType == typeof(ForteNumber) && a.Type == RelationshipType.IsParentOf));
    }
}