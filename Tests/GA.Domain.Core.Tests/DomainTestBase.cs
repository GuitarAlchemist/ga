namespace GA.Domain.Core.Tests;

using NUnit.Framework;
using System.Reflection;

/// <summary>
/// Base class for domain tests with common setup and utilities.
/// </summary>
public abstract class DomainTestBase
{
    [SetUp]
    public virtual void Setup()
    {
        // Common setup for all domain tests
        // Can be overridden in derived classes
    }

    [TearDown]
    public virtual void Teardown()
    {
        // Common cleanup for all domain tests
        // Can be overridden in derived classes
    }

    /// <summary>
    /// Asserts that a record has the expected property values.
    /// </summary>
    protected static void AssertRecordProperties<T>(T record, params (string PropertyName, object ExpectedValue)[] expectations)
    {
        foreach (var (propertyName, expectedValue) in expectations)
        {
            var property = typeof(T).GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Property {propertyName} not found on {typeof(T).Name}");
            
            var actualValue = property?.GetValue(record);
            Assert.That(actualValue, Is.EqualTo(expectedValue), 
                $"Property {propertyName} expected {expectedValue} but was {actualValue}");
        }
    }

    /// <summary>
    /// Asserts that an exception of type T is thrown with the expected message.
    /// </summary>
    protected static void AssertException<T>(TestDelegate code) where T : System.Exception
    {
        var exception = Assert.Throws<T>(code);
        // Can add message validation if needed in specific tests
    }

    /// <summary>
    /// Creates a test instance of T with sample data.
    /// </summary>
    protected static T CreateTestInstance<T>()
    {
        return Activator.CreateInstance<T>();
    }
}