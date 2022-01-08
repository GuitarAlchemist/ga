using System.Runtime.CompilerServices;
using GA.Business.Core.Intervals;

namespace GA.Business.Core.Notes;

/// <inheritdoc cref="IEquatable{Fret}" />
/// <inheritdoc cref="IComparable{Fret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An MIDI note between 0 and 127
/// </summary>
[PublicAPI]
public readonly record struct MidiNote : IValue<MidiNote>
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
    private static MidiNote Create(int value) => new() { Value = value };

    public static MidiNote Min => Create(_minValue);
    public static MidiNote Max => Create(_maxValue);
    public static MidiNote Open => Create(0);
    public static IReadOnlyCollection<MidiNote> All => ValueUtils<MidiNote>.All();

    public static int CheckRange(int value) => ValueUtils<MidiNote>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<MidiNote>.CheckRange(value, minValue, maxValue);
    public static MidiNote Create(Octave octave, PitchClass pitchClass) => Create((octave.Value - Octave.Min.Value) * 12 + pitchClass.Value);
    public static MidiNote operator ++(MidiNote midiNote) => Create(midiNote._value + 1);
    public static MidiNote operator --(MidiNote midiNote) => Create(midiNote._value - 1);
    public static MidiNote operator +(MidiNote midiNote, int increment) => Create(midiNote._value + increment);
    public static MidiNote operator -(MidiNote midiNote, int increment) => Create(midiNote._value - increment);
    public static implicit operator MidiNote(int value) => Create(value);
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