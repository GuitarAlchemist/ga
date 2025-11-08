namespace GA.Business.Core.Intervals;

using Atonal;
using GA.Core.Extensions;
using DiatonicInterval = Interval.Diatonic;

public class DiatonicIntervalCollection(IEnumerable<DiatonicInterval> intervals)
    : IParsable<DiatonicIntervalCollection>,
        IReadOnlyCollection<DiatonicInterval>
{
    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{DiatonicInterval}" />
    /// </summary>
    public PrintableReadOnlyCollection<DiatonicInterval> Intervals { get; } = intervals.ToImmutableList().AsPrintable();

    /// <summary>
    ///     Gets the <see cref="PitchClassSet" />
    /// </summary>
    public PitchClassSet PitchClassSet => GetPitchClassSet(Intervals);

    /// <inheritdoc />
    public override string ToString()
    {
        return Intervals.PrintOut;
    }

    private static PitchClassSet GetPitchClassSet(IEnumerable<DiatonicInterval> intervals)
    {
        var builder = ImmutableList.CreateBuilder<PitchClass>();
        foreach (var interval in intervals)
        {
            var pitchClass = PitchClass.FromSemitones(interval.Semitones);
            builder.Add(pitchClass);
        }

        return new(builder);
    }

    #region IParsable<DiatonicIntervalCollection>

    /// <inheritdoc />
    public static DiatonicIntervalCollection Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        }

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out DiatonicIntervalCollection result)
    {
        if (s == null)
        {
            result = null!;
            return false;
        }

        var builder = ImmutableList.CreateBuilder<DiatonicInterval>();
        var segments = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            // Failure
            result = null!;
            return false;
        }

        foreach (var segment in segments)
        {
            if (!DiatonicInterval.TryParse(segment, null, out var interval))
            {
                continue; // Skip
            }

            builder.Add(interval);
        }

        // Success
        result = new(builder.ToImmutable());
        return true;
    }

    #endregion

    #region IReadonlyCollection<Interval.Diatonic> Members

    public IEnumerator<DiatonicInterval> GetEnumerator()
    {
        return Intervals.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Intervals).GetEnumerator();
    }

    public int Count => Intervals.Count;

    #endregion
}
