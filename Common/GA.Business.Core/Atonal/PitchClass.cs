namespace GA.Business.Core.Atonal;

using Abstractions;
using GA.Business.Core.Intervals.Primitives;
using GA.Core.Combinatorics;
using Intervals;
using Notes;
using Primitives;

/// <summary>
/// Items pitches related to each other by octave, enharmonic equivalence, or both (<see href="https://en.wikipedia.org/wiki/Pitch_class"/>
/// </summary>
/// <remarks>
/// 0 => C; 1 => C# or Db; 2 => D; 3 => D# or Eb; 4 => E; 5 => F; 6 => F# or Gb; 7 => G; 8 => G# or Ab; 9 => A; T => A# or Bb; E => B<br/>
/// <br/>
/// Implements <see cref="IStaticValueObjectList{PitchClass}"/> | <see cref="IStaticPairIntervalClassNorm{TSelf}"/> | <see cref="IParsable{PitchClass}"/>
/// </remarks>
[PublicAPI]
public readonly record struct PitchClass : IStaticValueObjectList<PitchClass>,
                                           IParsable<PitchClass>, 
                                           IStaticPairIntervalClassNorm<PitchClass>
{
    #region IStaticValueObjectList<PitchClass> Members

    public static IReadOnlyCollection<PitchClass> Items => ValueObjectUtils<PitchClass>.Items;
    public static IReadOnlyList<int> Values => Items.ToValueList();
    
    /// <inheritdoc />
    public static PitchClass Min => FromValue(_minValue);
    
    /// <inheritdoc />
    public static PitchClass Max => FromValue(_maxValue);
  
    #endregion

    #region IPitchClass<PitchClass> Members
   
    /// <inheritdoc cref="IStaticPairIntervalClassNorm{TSelf}.GetNorm"/>
    public static IntervalClass GetPairNorm(PitchClass pitchClass1, PitchClass pitchClass2) => IStaticPairIntervalClassNorm<PitchClass>.GetNorm(pitchClass1, pitchClass2);
    
    #endregion

    #region IValueObject<PitchClass> Members

    public static implicit operator PitchClass(int value) => FromValue(value);
    public static implicit operator int(PitchClass octave) => octave.Value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClass FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static PitchClass FromSemitones(Semitones semitones) => FromValue(semitones.Value);
    
    public int Value
    {
        get => _value;
        init => _value = ValueObjectUtils<PitchClass>.EnsureValueRange(value, _minValue, _maxValue, true);
    }

    #endregion

    #region Relational members

    public static bool operator <(PitchClass left, PitchClass right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClass left, PitchClass right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClass left, PitchClass right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClass left, PitchClass right) => left.CompareTo(right) >= 0;
    
    /// <inheritdoc />
    public int CompareTo(PitchClass other) => _value.CompareTo(other._value);

    #endregion

    #region IParsable Members

    /// <inheritdoc />
    public static PitchClass Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out PitchClass result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var normalizedInput = s.Trim().ToUpperInvariant();
        if (int.TryParse(normalizedInput, out var i))
        {
            if (i is >= 0 and <= 9)
            {
                result = FromValue(i);
                return true;
            }
        }

        switch (normalizedInput)
        {
            case "T":
                result = FromValue(10);
                return true;
            case "E":
                result = FromValue(11);
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Operators

    public static implicit operator Note.Sharp(PitchClass pitchClass) => pitchClass.ToSharpNote();
    public static implicit operator Note.Flat(PitchClass pitchClass) => pitchClass.ToFlatNote();
    
    #endregion
    
    private const int _minValue = 0;
    private const int _maxValue = 11;
    private readonly int _value;

    /// <summary>
    /// Performs a normalized pitch class subtraction between two pitch classes
    /// </summary>
    /// <param name="pitchClass1">The first <see cref="PitchClass"/></param>
    /// <param name="pitchClass2">The second <see cref="PitchClass"/></param>
    /// <returns></returns>
    public static PitchClass operator -(PitchClass pitchClass1, PitchClass pitchClass2) => FastPitchClassCalculator.NormalizedSubtraction(pitchClass1, pitchClass2);

    public override string ToString() => _value switch
    {
        10 => "T", // Abbreviation for 10
        11 => "E", // Abbreviation for 11
        _ => _value.ToString()
    };

    public Note.Chromatic ToChromaticNote() => _chromaticNotes[_value];
    public Note.Sharp ToSharpNote() => _sharpNotes[_value];
    public Note.Flat ToFlatNote() => _flatNotes[_value];
    public Pitch.Chromatic ToChromaticPitch(Octave octave) => new(ToChromaticNote(), octave);
    public Pitch.Sharp ToSharpPitch(Octave octave) => new(ToSharpNote(), octave);
    public Pitch.Flat ToFlatPitch(Octave octave) => new(ToFlatNote(), octave);

    private static readonly ImmutableList<Note.Chromatic> _chromaticNotes =
        [
            Note.Chromatic.C,
            Note.Chromatic.CSharpOrDFlat,
            Note.Chromatic.D,
            Note.Chromatic.DSharpOrEFlat,
            Note.Chromatic.E,
            Note.Chromatic.F,
            Note.Chromatic.FSharpOrGFlat,
            Note.Chromatic.G,
            Note.Chromatic.GSharpOrAFlat,
            Note.Chromatic.A,
            Note.Chromatic.ASharpOrBFlat,
            Note.Chromatic.B
        ];

    private static readonly ImmutableList<Note.Sharp> _sharpNotes =
    [
        Note.Sharp.C, Note.Sharp.CSharp, Note.Sharp.D, Note.Sharp.DSharp, Note.Sharp.E, Note.Sharp.F,
        Note.Sharp.FSharp, Note.Sharp.G, Note.Sharp.GSharp, Note.Sharp.A, Note.Sharp.ASharp, Note.Sharp.B
    ];

    private static readonly ImmutableList<Note.Flat> _flatNotes =
    [
        Note.Flat.C, Note.Flat.DFlat, Note.Flat.D, Note.Flat.EFlat, Note.Flat.E, Note.Flat.F,
        Note.Flat.GFlat, Note.Flat.G, Note.Flat.AFlat, Note.Flat.A, Note.Flat.BFlat, Note.Flat.B
    ];

    #region Inner Classes

    /// <summary>
    /// Fast calculator class caching common pitch class operations
    /// </summary>
    /// <remarks>
    /// Internally caches all possible results from operations
    /// </remarks>
    private class FastPitchClassCalculator
    {
        public static PitchClass NormalizedSubtraction(PitchClass pitchClass1, PitchClass pitchClass2) => _lazySubtractionDictionary.Value[(pitchClass1.Value, pitchClass2.Value)];

        /// <summary>
        /// Pre-computes the normalized difference between all possible combinations of pitch class pairs
        /// </summary>
        private static readonly Lazy<ImmutableDictionary<(int, int), PitchClass>> _lazySubtractionDictionary = new(GetSubtractionDictionary);
        
        private static ImmutableDictionary<(int, int), PitchClass> GetSubtractionDictionary()
        {
            var builder = ImmutableDictionary.CreateBuilder<(int, int), PitchClass>();
            foreach (var (pcValue1, pcValue2) in new CartesianProduct<int>(Values))
            {
                builder.Add(
                    (pcValue1, pcValue2), 
                    FromValue((pcValue1 - pcValue2 + 12) % 12));
            }
            return builder.ToImmutable(); }

    }
    
    #endregion
}