namespace GA.Core.Tests.ValueObjects;

using Core.Abstractions;
using Core.ValueObjects;
using Domain.Core.Instruments.Primitives;

/// <summary>
///     Tests verifying that IsValueInRange's normalization mirrors EnsureValueRange's normalization exactly.
/// </summary>
/// <remarks>
///     <see cref="Str" /> is used only as a concrete <c>IRangeValueObject</c> type witness;
///     the min/max values under test are passed explicitly and are unrelated to <see cref="Str" />'s own range.
/// </remarks>
public class ValueObjectUtilsTests
{
    [TestCase(12)]
    [TestCase(-1)]
    [TestCase(23)]
    public void IsValueInRange_ZeroBasedRange_Normalize_MatchesEnsureValueRange(int value)
    {
        // Arrange
        const int minValue = 0;
        const int maxValue = 11;

        // Act
        var isInRange = ValueObjectUtils<Str>.IsValueInRange(value, minValue, maxValue, normalize: true);
        var normalized = ValueObjectUtils<Str>.EnsureValueRange(value, minValue, maxValue, normalize: true);

        // Assert
        Assert.That(isInRange, Is.True);
        Assert.That(normalized, Is.InRange(minValue, maxValue));
    }

    [TestCase(11)]
    [TestCase(17)]
    [TestCase(4)]
    public void IsValueInRange_NonZeroBasedRange_Normalize_MatchesEnsureValueRange(int value)
    {
        // Arrange
        const int minValue = 5;
        const int maxValue = 10;

        // Act
        var isInRange = ValueObjectUtils<Str>.IsValueInRange(value, minValue, maxValue, normalize: true);
        var normalized = ValueObjectUtils<Str>.EnsureValueRange(value, minValue, maxValue, normalize: true);

        // Assert
        Assert.That(isInRange, Is.True);
        Assert.That(normalized, Is.InRange(minValue, maxValue));
    }

    [Test]
    public void IsValueInRange_WithoutNormalize_OutOfRangeValue_ReturnsFalse()
    {
        var result = ValueObjectUtils<Str>.IsValueInRange(12, 0, 11, normalize: false);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValueInRange_ValueAlreadyInRange_ReturnsTrue()
    {
        var result = ValueObjectUtils<Str>.IsValueInRange(5, 0, 11, normalize: false);

        Assert.That(result, Is.True);
    }

    [TestCase(12, 0)]
    [TestCase(-1, 11)]
    [TestCase(23, 11)]
    [TestCase(-13, 11)]
    public void EnsureValueInRange_Normalize_FoldsIntoRange(int value, int expected)
    {
        // The interface helper used to fold with an off-by-one range size (max - min instead of max - min + 1)
        var normalized = IRangeValueObject<Str>.EnsureValueInRange(value, 0, 11, normalize: true);

        Assert.That(normalized, Is.EqualTo(expected));
    }

    [Test]
    public void EnsureValueInRange_Normalize_MatchesValueObjectUtils()
    {
        for (var value = -30; value <= 30; value++)
        {
            Assert.That(
                IRangeValueObject<Str>.EnsureValueInRange(value, 5, 10, normalize: true),
                Is.EqualTo(ValueObjectUtils<Str>.EnsureValueRange(value, 5, 10, normalize: true)));
        }
    }
}
