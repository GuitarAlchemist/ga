using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace GA.Business.Core.Fretboard.Primitives;

/// <inheritdoc cref="IEquatable{Fret}" />
/// <inheritdoc cref="IComparable{Fret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An non-muted instrument fret (Between <see cref="MinValue" /> and <see cref="MaxValue" />)
/// </summary>
[PublicAPI]
public readonly struct Fret : IEquatable<Fret>, IComparable<Fret>, IComparable
{
    #region Relational members

    public int CompareTo(Fret other)
    {
        return Value.CompareTo(other.Value);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is Fret other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Str)}");
    }

    public static bool operator <(Fret left, Fret right) =>left.CompareTo(right) < 0;
    public static bool operator >(Fret left, Fret right) => left.CompareTo(right) > 0;
    public static bool operator <=(Fret left, Fret right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Fret left, Fret right) => left.CompareTo(right) >= 0;

    #endregion

    public const uint MinValue = 0;
    public const uint MaxValue = 36;
    public static readonly Fret Open = new(0);

    /// <summary>
    /// Ensures the value is between <see cref="MinValue"/> and <see cref="MaxValue"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
    public static uint CheckRange(uint value)
    {
        CheckRange(value, MinValue, MaxValue);

        return value;
    }

    public static IReadOnlyCollection<Fret> GetCollection(int start, int count)
    {
        CheckRange((uint)count, MinValue, MaxValue);
        return new FretCollection(start, count);
    }

    /// <summary>
    /// Ensures the value is within the provided range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
    public static void CheckRange(
        uint value, 
        uint minValue, 
        uint maxValue)
    {
        minValue = Math.Max(minValue, MinValue);
        maxValue = Math.Max(maxValue, MinValue);

        if (value < MinValue) throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(value)} must be greater than {minValue}");
        if (value > MaxValue) throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(value)} must be less or equal to {maxValue}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fret(uint value)
    {
        CheckRange(value);

        Value = value;
    }

    /// <summary>
    /// A value between 0 and <see cref="F:GA.Business.Core.Primitives.StringIndex.MaxValue" />
    /// </summary>
    public uint Value { get; }

    /// <summary>
    /// True if open fret, false otherwise.
    /// </summary>
    public bool IsOpen => Value == 0;

    public void CheckMaxValue(uint maxValue)
    {
        CheckRange(Value, MinValue, maxValue);
    }

    public override bool Equals([NotNullWhen(true)] object? value) => value is Fret fret && Value == fret.Value;
    public bool Equals(Fret other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(Fret left, Fret right) => left.Equals(right);
    public static bool operator !=(Fret left, Fret right) => !(left == right);
    public static implicit operator Fret(uint value) => new(value);
    public override string ToString() => Value.ToString();

    private class FretCollection : IReadOnlyCollection<Fret>
    {
        private readonly IEnumerable<Fret> _range;

        public FretCollection(int start, int count)
        {
            _range = 
                Enumerable.Range(start, count)
                          .Select(i => new Fret((uint)i));
            Count = count;
        }

        public IEnumerator<Fret> GetEnumerator() => _range.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count { get; }
    }
}