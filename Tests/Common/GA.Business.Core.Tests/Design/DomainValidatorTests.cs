namespace GA.Business.Core.Tests.Design;

using Domain.Core.Design.Attributes;
using GA.Domain.Services.Validation;

[TestFixture]
public class DomainValidatorTests
{
    [SetUp]
    public void Setup() => _validator = new DomainValidator();

    private DomainValidator _validator;

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
