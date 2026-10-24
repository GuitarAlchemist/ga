namespace GA.Domain.Core.Instruments;

using Core.Primitives.Notes;
using Design.Attributes;
using GA.Core.Collections.Abstractions;
using Primitives;

/// <summary>
///     Represent a fretted instrument tuning
/// </summary>
/// <remarks>
///     References:
///     https://www.guitarworld.com/lessons/11-alternate-tunings-every-guitarist-should-know
///     https://www.stringsbymail.com/TuningChart.pdf
/// </remarks>
[PublicAPI]
[DomainInvariant("Tuning must have at least one string", "StringCount > 0")]
public sealed class Tuning : IIndexer<Str, Pitch>
{
    /// <summary>
    ///     The default tuning (Guitar - E2 A2 D3 G3 B3 E4)
    /// </summary>
    public static readonly Tuning Default = new(PitchCollection.Parse("E2 A2 D3 G3 B3 E4"));

    /// <summary>
    ///     Standard Ukulele tuning (G4 C4 E4 A4) - Soprano/Concert/Tenor
    /// </summary>
    public static readonly Tuning Ukulele = new(PitchCollection.Parse("G4 C4 E4 A4"));

    /// <summary>
    ///     Standard Bass Guitar tuning (E1 A1 D2 G2)
    /// </summary>
    public static readonly Tuning Bass = new(PitchCollection.Parse("E1 A1 D2 G2"));

    /// <summary>
    ///     7-String Guitar tuning (B1 E2 A2 D3 G3 B3 E4)
    /// </summary>
    public static readonly Tuning Guitar7String = new(PitchCollection.Parse("B1 E2 A2 D3 G3 B3 E4"));

    private readonly Pitch[] _pitches;

    /// <summary>
    ///     Constructs a <see cref="Tuning" /> instance.
    /// </summary>
    public Tuning(PitchCollection pitchCollection)
    {
        ArgumentNullException.ThrowIfNull(pitchCollection);

        PitchCollection = pitchCollection;
        _pitches = BuildPitchArray(pitchCollection);
    }

    /// <summary>
    ///     Gets the <see cref="PitchCollection" /> backing this tuning.
    /// </summary>
    public PitchCollection PitchCollection { get; }

    /// <summary>
    ///     Number of strings defined in this tuning.
    /// </summary>
    public int StringCount => _pitches.Length;

    /// <summary>
    ///     Gets the pitch associated with the specified open string.
    /// </summary>
    public Pitch this[Str str]
    {
        get
        {
            var index = str.Value - 1;
            if ((uint)index >= (uint)_pitches.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(str),
                    $"String {str.Value} is not defined for tuning with {_pitches.Length} strings.");
            }

            return _pitches[index];
        }
    }

    /// <summary>
    ///     Exposes the pitches as a contiguous span (highest string first).
    /// </summary>
    public ReadOnlySpan<Pitch> AsSpan() => _pitches;

    private static Pitch[] BuildPitchArray(IReadOnlyCollection<Pitch> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var buffer = items as Pitch[] ?? [.. items];
        var result = new Pitch[buffer.Length];

        if (IsWrittenFromTheBassSide(buffer))
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                result[i] = buffer[buffer.Length - 1 - i];
            }
        }
        else
        {
            Array.Copy(buffer, result, buffer.Length);
        }

        return result;
    }

    /// <summary>
    ///     True when the pitches are written from the bass side of the neck (last string first), so the
    ///     array must be reversed to put string 1 first.
    /// </summary>
    /// <remarks>
    ///     Comparing only the first pitch with the last fails on re-entrant tunings: a 5-string banjo is
    ///     written G4 D3 G3 B3 D4, its drone (string 5) higher than string 1. Counting the rising and
    ///     falling steps between adjacent strings follows the direction the list is actually written in;
    ///     on a tie (e.g. octave courses) the first-vs-last comparison decides, as before.
    /// </remarks>
    private static bool IsWrittenFromTheBassSide(Pitch[] pitches)
    {
        var rising = 0;
        var falling = 0;
        for (var i = 1; i < pitches.Length; i++)
        {
            if (pitches[i - 1] < pitches[i])
            {
                rising++;
            }
            else if (pitches[i] < pitches[i - 1])
            {
                falling++;
            }
        }

        return rising != falling ? rising > falling : pitches[0] < pitches[^1];
    }

    /// <inheritdoc />
    public override string ToString() => PitchCollection.ToString();
}
