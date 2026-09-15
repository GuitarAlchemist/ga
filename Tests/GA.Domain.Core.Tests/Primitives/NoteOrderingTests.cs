namespace GA.Domain.Core.Tests.Primitives;

using NUnit.Framework;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;

/// <summary>
///     Ordering must agree with structural equality: <c>CompareTo</c> returns 0 only for equal instances.
/// </summary>
[TestFixture]
public class NoteOrderingTests
{
    [Test]
    public void CompareTo_EnharmonicNotes_IsNotZero()
    {
        // Arrange
        Note cSharp = Note.Sharp.CSharp;
        Note dFlat = Note.Flat.DFlat;

        // Assert
        Assert.That(cSharp.PitchClass, Is.EqualTo(dFlat.PitchClass));
        Assert.That(cSharp, Is.Not.EqualTo(dFlat));
        Assert.That(cSharp.CompareTo(dFlat), Is.Not.Zero);
    }

    [Test]
    public void CompareTo_IsAntisymmetric()
    {
        // Arrange
        Note cSharp = Note.Sharp.CSharp;
        Note dFlat = Note.Flat.DFlat;

        // Assert
        Assert.That(Math.Sign(cSharp.CompareTo(dFlat)), Is.EqualTo(-Math.Sign(dFlat.CompareTo(cSharp))));
    }

    [Test]
    public void CompareTo_EqualNotes_IsZero()
    {
        // Arrange
        Note first = Note.Sharp.CSharp;
        Note second = Note.Sharp.CSharp;

        // Assert
        Assert.That(first.CompareTo(second), Is.Zero);
    }

    [Test]
    public void SortedSet_KeepsEnharmonicSpellings()
    {
        // Arrange
        SortedSet<Note> notes = [Note.Sharp.CSharp, Note.Flat.DFlat];

        // Assert - a comparison of 0 would have collapsed the two spellings into one entry
        Assert.That(notes, Has.Count.EqualTo(2));
    }

    [Test]
    public void CompareTo_OrdersByPitchClassFirst()
    {
        // Arrange
        Note c = Note.Sharp.C;
        Note d = Note.Flat.D;

        // Assert
        Assert.That(c.CompareTo(d), Is.LessThan(0));
    }

    [Test]
    public void CompareTo_Null_IsGreater() => Assert.That(Note.Sharp.C.CompareTo(null), Is.GreaterThan(0));

    [Test]
    public void IntervalCompareTo_EnharmonicIntervals_IsNotZero()
    {
        // Arrange
        Interval augmentedUnison = Interval.Simple.A1;
        Interval minorSecond = Interval.Simple.m2;

        // Assert
        Assert.That(augmentedUnison.Semitones, Is.EqualTo(minorSecond.Semitones));
        Assert.That(augmentedUnison, Is.Not.EqualTo(minorSecond));
        Assert.That(augmentedUnison.CompareTo(minorSecond), Is.Not.Zero);
        Assert.That(Math.Sign(augmentedUnison.CompareTo(minorSecond)),
            Is.EqualTo(-Math.Sign(minorSecond.CompareTo(augmentedUnison))));
    }

    [Test]
    public void IntervalCompareTo_EqualIntervals_IsZero() =>
        Assert.That(Interval.Simple.P5.CompareTo(Interval.Simple.P5), Is.Zero);

    [Test]
    public void IntervalSortedSet_KeepsEnharmonicIntervals()
    {
        // Arrange
        SortedSet<Interval> intervals = [Interval.Simple.A1, Interval.Simple.m2];

        // Assert
        Assert.That(intervals, Has.Count.EqualTo(2));
    }
}
