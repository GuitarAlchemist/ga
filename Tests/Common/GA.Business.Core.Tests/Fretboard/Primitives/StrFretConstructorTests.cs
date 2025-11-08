namespace GA.Business.Core.Tests.Fretboard.Primitives;

using Core.Fretboard.Primitives;

/// <summary>
///     Tests for Str and Fret constructors and creation methods
/// </summary>
[TestFixture]
public class StrFretConstructorTests
{
    [Test]
    public void Str_Constructor_WithValidValue_ShouldCreate()
    {
        // Act
        var str = new Str(1);

        // Assert
        Assert.That(str.Value, Is.EqualTo(1));
    }

    [Test]
    public void Str_Constructor_WithInvalidValue_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Str(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Str(27));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Str(-1));
    }

    [Test]
    public void Str_FromValue_WithValidValue_ShouldCreate()
    {
        // Act
        var str = Str.FromValue(6);

        // Assert
        Assert.That(str.Value, Is.EqualTo(6));
    }

    [Test]
    public void Str_ImplicitConversion_FromInt_ShouldCreate()
    {
        // Act
        Str str = 3;

        // Assert
        Assert.That(str.Value, Is.EqualTo(3));
    }

    [Test]
    public void Str_ImplicitConversion_ToInt_ShouldWork()
    {
        // Arrange
        var str = new Str(4);

        // Act
        int value = str;

        // Assert
        Assert.That(value, Is.EqualTo(4));
    }

    [Test]
    public void Fret_Constructor_WithValidValue_ShouldCreate()
    {
        // Act
        var fret = new Fret(5);

        // Assert
        Assert.That(fret.Value, Is.EqualTo(5));
    }

    [Test]
    public void Fret_Constructor_WithMuted_ShouldCreate()
    {
        // Act
        var fret = new Fret(-1);

        // Assert
        Assert.That(fret.Value, Is.EqualTo(-1));
        Assert.That(fret.IsMuted, Is.True);
    }

    [Test]
    public void Fret_Constructor_WithOpen_ShouldCreate()
    {
        // Act
        var fret = new Fret(0);

        // Assert
        Assert.That(fret.Value, Is.EqualTo(0));
        Assert.That(fret.IsOpen, Is.True);
    }

    [Test]
    public void Fret_Constructor_WithInvalidValue_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fret(-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fret(37));
    }

    [Test]
    public void Fret_FromValue_WithValidValue_ShouldCreate()
    {
        // Act
        var fret = Fret.FromValue(12);

        // Assert
        Assert.That(fret.Value, Is.EqualTo(12));
    }

    [Test]
    public void Fret_ImplicitConversion_FromInt_ShouldCreate()
    {
        // Act
        Fret fret = 7;

        // Assert
        Assert.That(fret.Value, Is.EqualTo(7));
    }

    [Test]
    public void Fret_ImplicitConversion_ToInt_ShouldWork()
    {
        // Arrange
        var fret = new Fret(9);

        // Act
        int value = fret;

        // Assert
        Assert.That(value, Is.EqualTo(9));
    }

    [Test]
    public void Fret_StaticProperties_ShouldWork()
    {
        // Assert
        Assert.That(Fret.Muted.Value, Is.EqualTo(-1));
        Assert.That(Fret.Open.Value, Is.EqualTo(0));
        Assert.That(Fret.One.Value, Is.EqualTo(1));
        Assert.That(Fret.Two.Value, Is.EqualTo(2));
        Assert.That(Fret.Three.Value, Is.EqualTo(3));
        Assert.That(Fret.Four.Value, Is.EqualTo(4));
        Assert.That(Fret.Five.Value, Is.EqualTo(5));
    }

    [Test]
    public void Str_AllCreationMethods_ShouldBeEquivalent()
    {
        // Arrange & Act
        var str1 = new Str(3);
        var str2 = Str.FromValue(3);
        Str str3 = 3;

        // Assert
        Assert.That(str1, Is.EqualTo(str2));
        Assert.That(str2, Is.EqualTo(str3));
        Assert.That(str1, Is.EqualTo(str3));
    }

    [Test]
    public void Fret_AllCreationMethods_ShouldBeEquivalent()
    {
        // Arrange & Act
        var fret1 = new Fret(5);
        var fret2 = Fret.FromValue(5);
        Fret fret3 = 5;

        // Assert
        Assert.That(fret1, Is.EqualTo(fret2));
        Assert.That(fret2, Is.EqualTo(fret3));
        Assert.That(fret1, Is.EqualTo(fret3));
    }
}
