namespace GA.Business.Core.Intervals;

using Atonal;
using DiatonicInterval = Interval.Diatonic;

public class DiatonicIntervalCollection(IEnumerable<DiatonicInterval> intervals) : IParsable<DiatonicIntervalCollection>, 
                                                                                   IReadOnlyCollection<DiatonicInterval>
{
    #region IParsable<DiatonicIntervalCollection>

    /// <inheritdoc />
    public static DiatonicIntervalCollection Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out DiatonicIntervalCollection result)
    {
        var builder = ImmutableList.CreateBuilder<Interval.Diatonic>();
        var segments = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            // Failure
            result = null!;
            return false;
        }

        foreach (var segment in segments)
        {
            if (!Interval.Diatonic.TryParse(segment, null, out var interval)) continue; // Skip
            builder.Add(interval);
        }

        // Success
        result = new(builder.ToImmutable());
        return true;
    }    

    #endregion

    #region IReadonlyCollection<Interval.Diatonic> Members

    public IEnumerator<Interval.Diatonic> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    #endregion

    /// <summary>
    /// Gets the <see cref="PrintableReadOnlyCollection{DiatonicInterval}"/>
    /// </summary>
    public PrintableReadOnlyCollection<DiatonicInterval> Intervals { get; } = intervals.ToImmutableList().AsPrintable();

    /// <summary>
    /// Gets the <see cref="PitchClassSet"/>
    /// </summary>
    public PitchClassSet PitchClassSet =>  GetPitchClassSet(Intervals);

    /// <inheritdoc />
    public override string ToString() => Intervals.PrintOut;

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
}