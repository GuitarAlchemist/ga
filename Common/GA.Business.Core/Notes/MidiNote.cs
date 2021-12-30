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
    public static MidiNote operator ++(MidiNote midiNote) => Create(midiNote.Value + 1);
    public static MidiNote operator --(MidiNote midiNote) => Create(midiNote.Value - 1);
    public static implicit operator MidiNote(int value) => Create(value);
    public static implicit operator int(MidiNote midiNote) => midiNote.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public override string ToString() => Value.ToString();

    public PitchClass PitchClass => new() {Value = Value % 12};
    public Note Note => SharpNote;
    public Octave Octave => new() {Value = Octave.Min.Value + Value / 12};
    public Pitch Pitch => SharpPitch;
    public Note.Sharp SharpNote => Note.Sharp.FromPitchClass(PitchClass);
    public Note.Flat FlatNote => Note.Flat.FromPitchClass(PitchClass);
    public Pitch.Sharp SharpPitch => new(SharpNote, Octave);
    public Pitch.Flat FlatPitch => new(FlatNote, Octave);
}