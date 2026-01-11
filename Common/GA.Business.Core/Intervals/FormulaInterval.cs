namespace GA.Business.Core.Intervals;

using System;
using System.Collections.Generic;
using System.Text;
using Atonal;
using Atonal.Abstractions;
using Primitives;

/// <summary>
///     Scale or chord formula interval base abstract class
/// </summary>
/// <param name="size">The <see cref="IIntervalSize" /></param>
/// <param name="quality">The <see cref="IntervalQuality" /></param>
public abstract class FormulaIntervalBase(
    IIntervalSize size,
    IntervalQuality quality) :
    IEquatable<FormulaIntervalBase>,
    IComparable<FormulaIntervalBase>, IComparable,
    IPitchClass
{
    /// <summary>
    ///     Gets the <see cref="IIntervalSize" />
    /// </summary>
    public IIntervalSize Size { get; } = size;

    /// <summary>
    ///     Gets the <see cref="IntervalQuality" />
    /// </summary>
    public IntervalQuality Quality { get; } = quality;

    /// <summary>
    ///     Get the <see cref="IntervalConsonance" />
    /// </summary>
    public IntervalConsonance Consonance => Size.Consonance;

    /// <inheritdoc />
    public PitchClass PitchClass => PitchClass.FromSemitones(ToSemitones());

    /// <summary>
    ///     Gets the interval semitones
    /// </summary>
    /// <returns>The <see cref="Semitones" /></returns>
    public Semitones ToSemitones()
    {
        var result = Size.Semitones;
        var accidental = Quality.ToAccidental(Consonance);
        if (accidental.HasValue)
        {
            result += accidental.Value.ToSemitones();
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        var accidental = Quality.ToAccidental(Consonance);

        if (accidental.HasValue)
        {
            sb.Append(accidental.Value.ToString());
        }

        sb.Append(Size);
        var result = sb.ToString();

        return result;
    }

    #region Equality Members

    public bool Equals(FormulaIntervalBase? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Size.Equals(other.Size) && Quality.Equals(other.Quality);
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

        return obj.GetType() == GetType() && Equals((FormulaIntervalBase)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Size, Quality);
    }

    public static bool operator ==(FormulaIntervalBase? left, FormulaIntervalBase? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FormulaIntervalBase? left, FormulaIntervalBase? right)
    {
        return !Equals(left, right);
    }

    #endregion

    #region Relation Members

    public int CompareTo(FormulaIntervalBase? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        var sizeComparison = Size.CompareTo(other.Size);
        return sizeComparison != 0 ? sizeComparison : Quality.CompareTo(other.Quality);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return 1;
        }

        if (ReferenceEquals(this, obj))
        {
            return 0;
        }

        return obj is FormulaIntervalBase other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(FormulaIntervalBase)}");
    }

    public static bool operator <(FormulaIntervalBase? left, FormulaIntervalBase? right)
    {
        return Comparer<FormulaIntervalBase>.Default.Compare(left, right) < 0;
    }

    public static bool operator >(FormulaIntervalBase? left, FormulaIntervalBase? right)
    {
        return Comparer<FormulaIntervalBase>.Default.Compare(left, right) > 0;
    }

    public static bool operator <=(FormulaIntervalBase? left, FormulaIntervalBase? right)
    {
        return Comparer<FormulaIntervalBase>.Default.Compare(left, right) <= 0;
    }

    public static bool operator >=(FormulaIntervalBase? left, FormulaIntervalBase? right)
    {
        return Comparer<FormulaIntervalBase>.Default.Compare(left, right) >= 0;
    }

    #endregion
}

/// <summary>
///     Scale or chord formula interval abstract class (Strongly-typed)
/// </summary>
/// <typeparam name="TIntervalSize">The interval size type (Must implement <see cref="IIntervalSize" />)</typeparam>
/// <param name="size">The <paramtyperef name="TIntervalSize" /></param>
/// <param name="quality">The <see cref="IntervalQuality" /></param>
public abstract class FormulaInterval<TIntervalSize>(TIntervalSize size, IntervalQuality quality)
    : FormulaIntervalBase(size, quality)
    where TIntervalSize : IIntervalSize
{
    /// <summary>
    ///     Gets the <typeparamref name="TIntervalSize" />
    /// </summary>
    public new TIntervalSize Size { get; } = size;
}
