namespace GA.Domain.Core.Primitives.Notes;

using GA.Core.Abstractions;
using GA.Core.Functional;
using Theory.Atonal;
using Theory.Atonal.Abstractions;

/// <summary>
///     A MIDI note (0 to 127)
/// </summary>
/// <remarks>
///     Implements <see cref="IRangeValueObject{TSelf}" /> and <see cref="IPitchClass" />
/// </remarks>
[PublicAPI]
public readonly record struct MidiNote : IRangeValueObject<MidiNote>, IPitchClass
{
    private const int _minValue = 0;
    private const int _maxValue = 127;

    private readonly int _value;

    /// <summary>
    ///     Creates a new MidiNote from an int value with range validation.
    /// </summary>
    /// <param name="value">The MIDI note number. Must be between <see cref="Min" /> (0) and <see cref="Max" /> (127).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="value" /> is outside the valid range
    ///     [0..127].
    /// </exception>
    /// <remarks>
    ///     MIDI note 60 is Middle C (C4). You can also use implicit conversion: <c>MidiNote note = 60;</c>
    /// </remarks>
    public MidiNote([ValueRange(_minValue, _maxValue)] int value) => _value = CheckRange(value);

    public static MidiNote Open => FromValue(0);
    public static IReadOnlyCollection<MidiNote> All => ValueObjectUtils<MidiNote>.Items;
    public Note Note => ToSharpNote();
    public Octave Octave => new() { Value = Octave.Min.Value + _value / 12 };

    public PitchClass PitchClass => new() { Value = _value % 12 };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MidiNote FromValue([ValueRange(_minValue, _maxValue)] int value) =>
        new() { Value = value };

    public static MidiNote Min => FromValue(_minValue);
    public static MidiNote Max => FromValue(_maxValue);

    public static implicit operator MidiNote(int value) => FromValue(value);

    public static implicit operator int(MidiNote midiNote) => midiNote._value;

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    /// <summary>
    ///     Attempts to create a MidiNote from an int value, returning a Result instead of throwing.
    /// </summary>
    /// <param name="value">The MIDI note number to validate.</param>
    /// <returns>A Result containing either a valid MidiNote or an error message.</returns>
    /// <remarks>
    ///     This method enables functional error handling without exceptions.
    ///     Example:
    ///     <code>
    /// var result = MidiNote.TryCreate(userInput)
    ///     .Map(note => note.Value)
    ///     .Match(
    ///         onSuccess: v => $"Valid MIDI note: {v}",
    ///         onFailure: err => $"Error: {err}"
    ///     );
    /// </code>
    /// </remarks>
    public static Result<MidiNote, string> TryCreate(int value)
    {
        if (value is < _minValue or > _maxValue)
        {
            return Result<MidiNote, string>.Failure(
                $"MIDI note number must be between {_minValue} and {_maxValue}, got {value}");
        }

        return Result<MidiNote, string>.Success(new() { Value = value });
    }

    public static int CheckRange(int value) => ValueObjectUtils<MidiNote>.EnsureValueRange(value, _minValue, _maxValue);

    public static int CheckRange(int value, int minValue, int maxValue) =>
        ValueObjectUtils<MidiNote>.EnsureValueRange(value, minValue, maxValue);

    public static MidiNote Create(Octave octave, PitchClass pitchClass) =>
        FromValue((octave.Value - Octave.Min.Value) * 12 + pitchClass.Value);

    public static MidiNote operator ++(MidiNote midiNote) => FromValue(midiNote._value + 1);

    public static MidiNote operator --(MidiNote midiNote) => FromValue(midiNote._value - 1);

    public static MidiNote operator +(MidiNote midiNote, int increment) => FromValue(midiNote._value + increment);

    public static MidiNote operator -(MidiNote midiNote, int increment) => FromValue(midiNote._value - increment);

    public static implicit operator Pitch.Chromatic(MidiNote midiNote) => midiNote.ToChromaticPitch();

    public static implicit operator Pitch.Sharp(MidiNote midiNote) => midiNote.ToSharpPitch();

    public static implicit operator Pitch.Flat(MidiNote midiNote) => midiNote.ToFlatPitch();

    public override string ToString() => _value.ToString();

    public Pitch ToPitch() => ToSharpPitch();

    public Note.Chromatic ToChromaticNote() => PitchClass.ToChromaticNote();

    public Note.Sharp ToSharpNote() => PitchClass.ToSharpNote();

    public Note.Flat ToFlatNote() => PitchClass.ToFlatNote();

    public Pitch.Chromatic ToChromaticPitch() => new(ToChromaticNote(), Octave);

    public Pitch.Sharp ToSharpPitch() => new(ToSharpNote(), Octave);

    public Pitch.Flat ToFlatPitch() => new(ToFlatNote(), Octave);

    #region Relational members

    public int CompareTo(MidiNote other) => _value.CompareTo(other._value);

    public static bool operator <(MidiNote left, MidiNote right) => left.CompareTo(right) < 0;

    public static bool operator >(MidiNote left, MidiNote right) => left.CompareTo(right) > 0;

    public static bool operator <=(MidiNote left, MidiNote right) => left.CompareTo(right) <= 0;

    public static bool operator >=(MidiNote left, MidiNote right) => left.CompareTo(right) >= 0;

    #endregion
}
