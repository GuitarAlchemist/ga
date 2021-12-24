using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace GA.Business.Core.Fretboard.Primitives;

public interface IValue<TSelf> : IEquatable<TSelf>, IComparable<TSelf>, IComparable
    where TSelf : struct, IValue<TSelf>
{
   static abstract TSelf MinValue { get; }
   static abstract TSelf MaxValue { get; }
}

/// <inheritdoc cref="IEquatable{Noteing}" />
/// <inheritdoc cref="IComparable{Noteing}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// A musical note
/// </summary>
[PublicAPI]
public readonly struct Note : IValue<Note>
{
    #region Relational members

    public int CompareTo(Note other)
    {
        return Value.CompareTo(other.Value);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is Note other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Note)}");
    }

    public static bool operator <(Note left, Note right) =>left.CompareTo(right) < 0;
    public static bool operator >(Note left, Note right) => left.CompareTo(right) > 0;
    public static bool operator <=(Note left, Note right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Note left, Note right) => left.CompareTo(right) >= 0;

    #endregion

    public static Note C { get; } = new(0);
    public static Note D { get; } = new(1);
    public static Note E { get; } = new(2);
    public static Note F { get; } = new(3);
    public static Note G { get; } = new(4);
    public static Note A { get; } = new(5);
    public static Note B { get; } = new(6);

    public static Note MinValue { get; } = C;
    public static Note MaxValue { get; } = B;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Note(uint value)
    {
        Value = value;
    }

    /// <summary>
    /// A value Between <see cref="MinValue"/> and <see cref="MaxValue" />.
    /// </summary>
    public uint Value { get; }

    public override bool Equals([NotNullWhen(true)] object? value) => value is Note Note && Value == Note.Value;
    public bool Equals(Note other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(Note left, Note right) => left.Equals(right);
    public static bool operator !=(Note left, Note right) => !(left == right);
    public static implicit operator Note(uint value) => new(value);
    public static implicit operator uint (Note value) => value.Value;
    public override string ToString() => Value.ToString();
}

