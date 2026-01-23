namespace GA.Domain.Core.Tests.Theory.Atonal;

using NUnit.Framework;
using GA.Domain.Core.Theory.Atonal;

[TestFixture]
public class PitchClassTests
{
    [Test]
    public void FromValue_ValidValue_CreatesInstance()
    {
        // Arrange & Act
        var pitchClass = PitchClass.FromValue(5);

        // Assert
        Assert.That(pitchClass.Value, Is.EqualTo(5));
    }

    [TestCase(0)]
    [TestCase(5)]
    [TestCase(11)]
    public void FromValue_BoundaryValues_CreatesInstance(int value)
    {
        // Arrange & Act
        var pitchClass = PitchClass.FromValue(value);

        // Assert
        Assert.That(pitchClass.Value, Is.EqualTo(value));
    }

    [TestCase(-1)]
    [TestCase(12)]
    [TestCase(13)]
    public void FromValue_OutOfRange_ThrowsArgumentException(int value)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => PitchClass.FromValue(value));
    }

    [Test]
    public void ImplicitConversion_FromInt_CreatesPitchClass()
    {
        // Arrange & Act
        PitchClass pitchClass = 7;

        // Assert
        Assert.That(pitchClass.Value, Is.EqualTo(7));
    }

    [Test]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        // Arrange
        var pitchClass = PitchClass.FromValue(8);

        // Act
        int value = pitchClass;

        // Assert
        Assert.That(value, Is.EqualTo(8));
    }

    [Test]
    public void StaticProperties_ReturnCorrectValues()
    {
        // Assert
        Assert.That(PitchClass.C.Value, Is.EqualTo(0));
        Assert.That(PitchClass.CSharp.Value, Is.EqualTo(1));
        Assert.That(PitchClass.D.Value, Is.EqualTo(2));
        Assert.That(PitchClass.E.Value, Is.EqualTo(4));
        Assert.That(PitchClass.F.Value, Is.EqualTo(5));
        Assert.That(PitchClass.G.Value, Is.EqualTo(7));
        Assert.That(PitchClass.A.Value, Is.EqualTo(9));
        Assert.That(PitchClass.B.Value, Is.EqualTo(11));
    }

    [TestCase(0, "0")]
    [TestCase(9, "9")]
    [TestCase(10, "T")]
    [TestCase(11, "E")]
    public void ToString_SpecialCases_ReturnsCorrectFormat(int value, string expected)
    {
        // Arrange
        var pitchClass = PitchClass.FromValue(value);

        // Act
        var result = pitchClass.ToString();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Subtraction_ReturnsCorrectDifference()
    {
        // Arrange
        var pc1 = PitchClass.FromValue(9);
        var pc2 = PitchClass.FromValue(4);

        // Act
        var result = pc1 - pc2;

        // Assert
        Assert.That(result.Value, Is.EqualTo(5)); // 9 - 4 = 5
    }

    [Test]
    public void Comparison_WrappedValues_WorksCorrectly()
    {
        // Arrange
        var pc1 = PitchClass.FromValue(2);
        var pc2 = PitchClass.FromValue(10);

        // Act & Assert
        Assert.That(pc1 < pc2, Is.True);
        Assert.That(pc2 > pc1, Is.True);
        Assert.That(pc1 <= pc2, Is.True);
        Assert.That(pc2 >= pc1, Is.True);
    }

    [TestCase("C", 0)]
    [TestCase("T", 10)]
    [TestCase("E", 11)]
    [TestCase("A", 10)] // Hex-style parsing
    [TestCase("B", 11)] // Hex-style parsing
    public void TryParse_ValidInput_ReturnsSuccess(string input, int expectedValue)
    {
        // Arrange & Act
        var result = PitchClass.TryParse(input, null, out var pitchClass);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(pitchClass.Value, Is.EqualTo(expectedValue));
    }

    [TestCase("")]
    [TestCase("X")]
    [TestCase("12")]
    [TestCase("-1")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        // Arrange & Act
        var result = PitchClass.TryParse(input, null, out var pitchClass);

        // Assert
        Assert.That(result, Is.False);
    }
}