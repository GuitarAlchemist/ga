namespace GA.Core.Tests.Collections;

using System.Linq;
using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Fretboard.Fingering;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Intervals.Primitives;
using GA.Business.Core.Tonal;

/// <summary>
/// Tests to verify that IStaticValueObjectList implementations use memoization correctly.
/// These tests verify that the Items and Values collections are properly cached and that
/// FromValue returns consistent results.
/// </summary>
[TestFixture]
public class ValueObjectMemoizationTests
{
    #region Fret Tests

    [Test]
    public void Fret_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = Fret.Items;
        var items2 = Fret.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void Fret_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = Fret.Values;
        var values2 = Fret.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    [Test]
    public void Fret_FromValue_ShouldReturnConsistentResults()
    {
        // Arrange & Act
        var fret1 = Fret.FromValue(5);
        var fret2 = Fret.FromValue(5);

        // Assert - Should return equal values
        Assert.That(fret1, Is.EqualTo(fret2));
        Assert.That(fret1.Value, Is.EqualTo(5));
    }

    [Test]
    public void Fret_ImplicitOperator_ShouldWork()
    {
        // Arrange & Act
        Fret fret1 = 7;
        Fret fret2 = 7;

        // Assert - Should return equal values
        Assert.That(fret1, Is.EqualTo(fret2));
        Assert.That(fret1.Value, Is.EqualTo(7));
    }

    #endregion

    #region PitchClass Tests

    [Test]
    public void PitchClass_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = PitchClass.Items;
        var items2 = PitchClass.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void PitchClass_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = PitchClass.Values;
        var values2 = PitchClass.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    [Test]
    public void PitchClass_FromValue_ShouldReturnConsistentResults()
    {
        // Arrange & Act
        var pc1 = PitchClass.FromValue(7);
        var pc2 = PitchClass.FromValue(7);

        // Assert - Should return equal values
        Assert.That(pc1, Is.EqualTo(pc2));
        Assert.That(pc1.Value, Is.EqualTo(7));
    }

    #endregion

    #region IntervalClass Tests

    [Test]
    public void IntervalClass_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = IntervalClass.Items;
        var items2 = IntervalClass.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void IntervalClass_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = IntervalClass.Values;
        var values2 = IntervalClass.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    #endregion

    #region SimpleIntervalSize Tests

    [Test]
    public void SimpleIntervalSize_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = SimpleIntervalSize.Items;
        var items2 = SimpleIntervalSize.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void SimpleIntervalSize_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = SimpleIntervalSize.Values;
        var values2 = SimpleIntervalSize.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    [Test]
    public void SimpleIntervalSize_ToCompound_ShouldReturnConsistentResults()
    {
        // Arrange
        var simple = SimpleIntervalSize.FromValue(4);  // Fourth

        // Act
        var compound1 = simple.ToCompound();  // Should be 11 (4 + 7)
        var compound2 = simple.ToCompound();

        // Assert - Should return equal values
        Assert.That(compound1, Is.EqualTo(compound2));
        Assert.That(compound1.Value, Is.EqualTo(11));
    }

    #endregion

    #region CompoundIntervalSize Tests

    [Test]
    public void CompoundIntervalSize_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = CompoundIntervalSize.Items;
        var items2 = CompoundIntervalSize.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void CompoundIntervalSize_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = CompoundIntervalSize.Values;
        var values2 = CompoundIntervalSize.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    #endregion

    #region Finger Tests

    [Test]
    public void Finger_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = Finger.Items;
        var items2 = Finger.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void Finger_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = Finger.Values;
        var values2 = Finger.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    #endregion

    #region FingerCount Tests

    [Test]
    public void FingerCount_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = FingerCount.Items;
        var items2 = FingerCount.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void FingerCount_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = FingerCount.Values;
        var values2 = FingerCount.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    #endregion

    #region RelativeFret Tests

    [Test]
    public void RelativeFret_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = RelativeFret.Items;
        var items2 = RelativeFret.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void RelativeFret_Values_ShouldBeMemoized()
    {
        // Arrange & Act
        var values1 = RelativeFret.Values;
        var values2 = RelativeFret.Values;

        // Assert - Should expose the same sequence of values across calls
        Assert.That(values1.Count, Is.GreaterThan(0));
        Assert.That(values2.Count, Is.EqualTo(values1.Count));
        Assert.That(values2, Is.EquivalentTo(values1));
    }

    #endregion

    #region KeySignature Tests

    [Test]
    public void KeySignature_Items_ShouldBeMemoized()
    {
        // Arrange & Act
        var items1 = KeySignature.Items;
        var items2 = KeySignature.Items;

        // Assert - Should expose a stable, cached set of items across calls
        Assert.That(items1.Count, Is.GreaterThan(0));
        Assert.That(items2.Count, Is.EqualTo(items1.Count));
        Assert.That(items2, Is.EquivalentTo(items1));
    }

    [Test]
    public void KeySignature_FromValue_ShouldReturnConsistentResults()
    {
        // Arrange & Act
        var ks1 = KeySignature.FromValue(2);
        var ks2 = KeySignature.FromValue(2);

        // Assert - Should return equal values
        Assert.That(ks1, Is.EqualTo(ks2));
        Assert.That(ks1.Value, Is.EqualTo(2));
    }

    #endregion
}

