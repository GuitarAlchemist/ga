using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace GA.Business.Core.Fretboard.Primitives;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An instrument string (Between <see cref="MinValue"/> and <see cref="MaxValue" />)
/// </summary>
/// <remarks>
/// String 1 is the string with the highest pitch.
/// </remarks>
[PublicAPI]
public readonly struct Str : IEquatable<Str>, IComparable<Str>, IComparable
{
    #region Relational members

    public int CompareTo(Str other)
    {
        return Value.CompareTo(other.Value);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is Str other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Str)}");
    }

    public static bool operator <(Str left, Str right) =>left.CompareTo(right) < 0;
    public static bool operator >(Str left, Str right) => left.CompareTo(right) > 0;
    public static bool operator <=(Str left, Str right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Str left, Str right) => left.CompareTo(right) >= 0;

    #endregion

    public const uint MinValue = 1;
    public const uint MaxValue = 26;

    /// <summary>
    /// Ensures the value is between <see cref="MinValue"/> and <see cref="MaxValue"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
    public static uint CheckRange(uint value)
    {
        CheckRange(value, MinValue, MaxValue);
        return value;
    }

    public static IReadOnlyCollection<Str> GetCollection(int count)
    {
        CheckRange((uint)count, MinValue, MaxValue);
        return new StrCollection(count);
    }

    /// <summary>
    /// Ensures the value is within the provided range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
    private static void CheckRange(
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
    public Str(uint value)
    {
        CheckRange(value);
        Value = value;
    }

    /// <summary>
    /// A value Between <see cref="MinValue"/> and <see cref="MaxValue" />.
    /// </summary>
    public uint Value { get; }

    public void CheckMaxValue(uint maxValue) => CheckRange(Value, MinValue, maxValue);

    public override bool Equals([NotNullWhen(true)] object? value) => value is Str str && Value == str.Value;
    public bool Equals(Str other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(Str left, Str right) => left.Equals(right);
    public static bool operator !=(Str left, Str right) => !(left == right);
    public static implicit operator Str(uint value) => new(value);
    public static implicit operator uint (Str value) => value.Value;
    public override string ToString() => Value.ToString();

    private class StrCollection : IReadOnlyCollection<Str>
    {
        private readonly IEnumerable<Str> _range;

        public StrCollection(int count)
        {
            _range = 
                Enumerable.Range((int)MinValue, count)
                          .Select(i => new Str((uint)i));
            Count = (int)count;
        }

        public IEnumerator<Str> GetEnumerator() => _range.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count { get; }
    }
}

