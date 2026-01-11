namespace GA.Business.Core.Tonal;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using GA.Core.Extensions;
using Intervals.Primitives;
using JetBrains.Annotations;
using Notes.Extensions;
using Notes.Primitives;
using KeyNote = Notes.Note.KeyNote;

/// <summary>
///     Key signature (See https://en.wikipedia.org/wiki/Key_signature)
/// </summary>
/// <remarks>
///     Implements <see cref="IRangeValueObject{TSelf}" /> |
///     <see cref="IStaticReadonlyCollectionFromValues{TSelf}" /> | <see cref="IReadOnlyCollection{T}" />
/// </remarks>
[PublicAPI]
public readonly record struct KeySignature : IStaticReadonlyCollectionFromValues<KeySignature>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private KeySignature([ValueRange(_minValue, _maxValue)] int value) : this()
    {
        _value = value;

        var accidentedNotes = GetAccidentedNotes(value).AsPrintable();
        AccidentedNotes = accidentedNotes;
        AccidentedNaturalNotesSet = accidentedNotes.Select(note => note.NaturalNote).ToImmutableHashSet().AsPrintable();
    }

    /// <summary>
    ///     Gets the accidental count <see cref="int" />
    /// </summary>
    public int AccidentalCount => Math.Abs(_value);

    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{T}" /> of accidented notes
    /// </summary>
    public PrintableReadOnlyCollection<KeyNote> AccidentedNotes { get; }

    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlySet{NaturalNote}" /> of accidented notes
    /// </summary>
    public PrintableReadOnlySet<NaturalNote> AccidentedNaturalNotesSet { get; }

    /// <summary>
    ///     Gets the <see cref="AccidentalKind" />
    /// </summary>
    public AccidentalKind AccidentalKind => _value < 0 ? AccidentalKind.Flat : AccidentalKind.Sharp;

    /// <summary>
    ///     True if sharp key, false otherwise
    /// </summary>
    public bool IsSharpKey => _value >= 0;

    /// <summary>
    ///     True if flat key, false otherwise
    /// </summary>
    public bool IsFlatKey => _value < 0;

    /// <summary>
    ///     Indicates if the specified <paramref name="naturalNote" /> is accidented
    /// </summary>
    /// <param name="naturalNote">The <see cref="NaturalNote" /></param>
    /// <returns>True if accidented, false otherwise</returns>
    public bool IsNoteAccidented(NaturalNote naturalNote)
    {
        return AccidentedNaturalNotesSet.Contains(naturalNote);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return AccidentedNotes.ToString();
    }

    private static ImmutableList<KeyNote> GetAccidentedNotes(int keySignatureValue)
    {
        var count = Math.Abs(keySignatureValue);
        IEnumerable<KeyNote> notes =
            keySignatureValue < 0
                ? GetNotes(NaturalNote.B, count, SimpleIntervalSize.Fourth)
                    .ToFlatNotes() // Circle of Fourths, starting from B
                : GetNotes(NaturalNote.F, count, SimpleIntervalSize.Fifth)
                    .ToSharpNotes(); // Circle of Fifths, starting from F
        return [.. notes];

        static IEnumerable<NaturalNote> GetNotes(NaturalNote firstItem, int count, SimpleIntervalSize increment)
        {
            var item = firstItem;
            for (var i = 0; i < count; i++)
            {
                yield return item;
                item += increment;
            }
        }
    }

    #region IStaticReadonlyCollectionFromValues<KeySignature> Members

    // Note: Use an explicit, allocation-safe construction to avoid potential
    // static initialization ordering issues observed in some test runners.
    public static IReadOnlyCollection<KeySignature> Items
        => Enumerable.Range(_minValue, _maxValue - _minValue + 1)
            .Select(FromValue)
            .ToImmutableList();

    /// <summary>
    /// Gets the cached span representing the full key signature range.
    /// </summary>
    public static ReadOnlySpan<KeySignature> ItemsSpan => ValueObjectUtils<KeySignature>.ItemsSpan;

    /// <summary>
    /// Gets the cached span representing the numeric values for each key signature.
    /// </summary>
    public static ReadOnlySpan<int> ValuesSpan => ValueObjectUtils<KeySignature>.ValuesSpan;

    public static KeySignature Min => new(_minValue);
    public static KeySignature Max => new(_maxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new(value);
    }

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = IRangeValueObject<KeySignature>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static implicit operator KeySignature(int value)
    {
        return new(value);
    }

    public static implicit operator int(KeySignature keySignature)
    {
        return keySignature.Value;
    }

    private const int _minValue = -7;
    private const int _maxValue = 7;

    #endregion

    #region Relational members

    public int CompareTo(KeySignature other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(KeySignature left, KeySignature right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(KeySignature left, KeySignature right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(KeySignature left, KeySignature right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(KeySignature left, KeySignature right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion

    #region Equality Members

    public bool Equals(KeySignature other)
    {
        return _value == other._value;
    }

    public override int GetHashCode()
    {
        return _value;
    }

    #endregion

    #region Static Helpers

    public static KeySignature Sharp([ValueRange(0, 7)] int count)
    {
        return new(count);
    }

    public static KeySignature Flat([ValueRange(1, 7)] int count)
    {
        return new(-count);
    }

    #endregion
}
