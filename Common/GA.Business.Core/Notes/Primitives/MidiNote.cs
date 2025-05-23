﻿namespace GA.Business.Core.Notes.Primitives;

using Atonal;
using Atonal.Abstractions;
using Intervals;

/// <summary>
/// A MIDI note (0 to 127)
/// </summary>
/// <remarks>
/// Implements <see cref="IRangeValueObject{MidiNote}"/> and <see cref="IPitchClass"/>
/// </remarks>
[PublicAPI]
public readonly record struct MidiNote : IRangeValueObject<MidiNote>, IPitchClass
{
    #region Relational members

    public int CompareTo(MidiNote other) => _value.CompareTo(other._value);
    public static bool operator <(MidiNote left, MidiNote right) => left.CompareTo(right) < 0;
    public static bool operator >(MidiNote left, MidiNote right) => left.CompareTo(right) > 0;
    public static bool operator <=(MidiNote left, MidiNote right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MidiNote left, MidiNote right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 127;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MidiNote FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MidiNote Min => FromValue(_minValue);
    public static MidiNote Max => FromValue(_maxValue);
    public static MidiNote Open => FromValue(0);
    public static IReadOnlyCollection<MidiNote> All => ValueObjectUtils<MidiNote>.Items;

    public static int CheckRange(int value) => ValueObjectUtils<MidiNote>.EnsureValueRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<MidiNote>.EnsureValueRange(value, minValue, maxValue);
    public static MidiNote Create(Octave octave, PitchClass pitchClass) => FromValue((octave.Value - Octave.Min.Value) * 12 + pitchClass.Value);
    public static MidiNote operator ++(MidiNote midiNote) => FromValue(midiNote._value + 1);
    public static MidiNote operator --(MidiNote midiNote) => FromValue(midiNote._value - 1);
    public static MidiNote operator +(MidiNote midiNote, int increment) => FromValue(midiNote._value + increment);
    public static MidiNote operator -(MidiNote midiNote, int increment) => FromValue(midiNote._value - increment);
    public static implicit operator MidiNote(int value) => FromValue(value);
    public static implicit operator int(MidiNote midiNote) => midiNote._value;
    public static implicit operator Pitch.Chromatic(MidiNote midiNote) => midiNote.ToChromaticPitch();
    public static implicit operator Pitch.Sharp(MidiNote midiNote) => midiNote.ToSharpPitch();
    public static implicit operator Pitch.Flat(MidiNote midiNote) => midiNote.ToFlatPitch();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public override string ToString() => _value.ToString();

    public PitchClass PitchClass => new() {Value = _value % 12};
    public Note Note => ToSharpNote();
    public Octave Octave => new() {Value = Octave.Min.Value + _value / 12};

    public Pitch ToPitch() => ToSharpPitch();
    public Note.Chromatic ToChromaticNote() => PitchClass.ToChromaticNote();
    public Note.Sharp ToSharpNote() => PitchClass.ToSharpNote();
    public Note.Flat ToFlatNote() => PitchClass.ToFlatNote();
    public Pitch.Chromatic ToChromaticPitch() => new(ToChromaticNote(), Octave);
    public Pitch.Sharp ToSharpPitch() => new(ToSharpNote(), Octave);
    public Pitch.Flat ToFlatPitch() => new(ToFlatNote(), Octave);
}