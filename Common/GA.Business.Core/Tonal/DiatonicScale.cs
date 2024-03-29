namespace GA.Business.Core.Tonal;

using Intervals;

[PublicAPI]
public abstract record DiatonicScale
{
    private static Interval.Chromatic T => Interval.Chromatic.Tone;
    private static Interval.Chromatic S => Interval.Chromatic.Semitone;

    /// <summary>
    /// MajorScaleMode scale
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Major_scale
    /// </remarks>
    public class Major : IReadOnlyCollection<Interval.Chromatic>
    {
        private static readonly IReadOnlyCollection<Interval.Chromatic> _intervals = [T, T, S, T, T, T, S];
        public IEnumerator<Interval.Chromatic> GetEnumerator() => _intervals.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _intervals.Count;
    }

    public class Minor : IReadOnlyCollection<Interval.Chromatic>
    {
        private static readonly IReadOnlyCollection<Interval.Chromatic> _intervals = [T, S, T, T, S, T, T];
        public IEnumerator<Interval.Chromatic> GetEnumerator() => _intervals.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _intervals.Count;
    }
}

