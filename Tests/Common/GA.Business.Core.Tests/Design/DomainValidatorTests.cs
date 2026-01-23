namespace GA.Domain.Core.Tests.Design;

using System.Linq;
using GA.Domain.Core.Design;
using NUnit.Framework;

[TestFixture]
public class DomainValidatorTests
{
    private DomainValidator _validator;
    [SetUp]
    public void Setup()
    {
        _validator = new DomainValidator();
    }
    [Test]
    public void Validate_ShouldCheckAnnotatedProperties()
    {
        var instance = new TestEntity { Name = null };
        var result = _validator.Validate(instance);
        Assert.That(result.Results.Any(r => r.Message.Contains("Name")), "Property invariant not checked");
    }
    private class TestEntity
    {
        [DomainInvariant("Name cannot be null", "!= null")]
        public string Name { get; set; }
    }
}