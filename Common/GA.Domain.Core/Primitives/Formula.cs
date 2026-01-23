namespace GA.Domain.Core.Primitives;

using GA.Domain.Core.Extensions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Core.Extensions;
using GA.Core.Collections;
using GA.Core.Extensions;
using Theory.Atonal;

public class Formula : IReadOnlyCollection<FormulaIntervalBase>, IEquatable<Formula>
{
    public Formula(IEnumerable<FormulaIntervalBase> intervals)
    {
        var intervalsSet = intervals.ToImmutableSortedSet();
        Intervals = intervalsSet.AsPrintable();
        PitchClassSet = intervalsSet.ToPitchClassSet();
        PrintOut = Intervals.PrintOut;
    }

    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{T}" />
    /// </summary>
    public PrintableReadOnlySet<FormulaIntervalBase> Intervals { get; }

    public PitchClassSet PitchClassSet { get; }
    public string PrintOut { get; }

    public bool Equals(Formula? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return PrintOut == other.PrintOut;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((Formula)obj);
    }

    public override int GetHashCode()
    {
        return PrintOut.GetHashCode();
    }

    public static bool operator ==(Formula? left, Formula? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Formula? left, Formula? right)
    {
        return !Equals(left, right);
    }

    public override string ToString()
    {
        return PrintOut;
    }

    #region IReadOnlyCollection<FormulaIntervalBase> Members

    public IEnumerator<FormulaIntervalBase> GetEnumerator()
    {
        return Intervals.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Intervals).GetEnumerator();
    }

    public int Count => Intervals.Count;

    #endregion
}