namespace GA.Domain.Core.Tests.Primitives;

using NUnit.Framework;

[TestFixture]
public class StrTests
{
    [Test]
    public void Str_FromValue_ValidValue_CreatesInstance()
    {
        // Arrange & Act
        var str = GA.Domain.Core.Instruments.Primitives.Str.FromValue(6);

        // Assert
        Assert.That(str.Value, Is.EqualTo(6));
    }

    [Test]
    public void ImplicitConversion_FromInt_CreatesStr()
    {
        // Arrange & Act
        GA.Domain.Core.Instruments.Primitives.Str str = 5;

        // Assert
        Assert.That(str.Value, Is.EqualTo(5));
    }

    [Test]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        // Arrange
        var str = GA.Domain.Core.Instruments.Primitives.Str.FromValue(4);

        // Act
        int value = str;

        // Assert
        Assert.That(value, Is.EqualTo(4));
    }

    [TestCase(-1)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(27)]
    public void FromValue_OutOfRange_ThrowsArgumentOutOfRangeException(int value)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => GA.Domain.Core.Instruments.Primitives.Str.FromValue(value));
    }

    [Test]
    public void Min_ReturnsCorrectValue()
    {
        // Assert
        Assert.That(GA.Domain.Core.Instruments.Primitives.Str.Min.Value, Is.EqualTo(1));
    }

    [Test]
    public void Max_ReturnsCorrectValue()
    {
        // Assert
        Assert.That(GA.Domain.Core.Instruments.Primitives.Str.Max.Value, Is.EqualTo(26));
    }

    [Test]
    public void ToString_ReturnsValueAsString()
    {
        // Arrange
        var str = GA.Domain.Core.Instruments.Primitives.Str.FromValue(5);

        // Act & Assert
        Assert.That(str.ToString(), Is.EqualTo("5"));
    }
}