using System.Collections;
using System.Collections.Immutable;
using GA.Business.Core.Intervals;

namespace GA.Business.Core.Tonal;

[PublicAPI]
[DiscriminatedUnion]
public abstract partial record DiatonicScale
{
    private static Interval.Chromatic T => Interval.Chromatic.Tone;
    private static Interval.Chromatic S => Interval.Chromatic.Semitone;

    /// <summary>
    /// Major scale
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Major_scale
    /// </remarks>
    public class Major : IReadOnlyCollection<Interval.Chromatic>
    {
        private static readonly IReadOnlyCollection<Interval.Chromatic> _intervals = new List<Interval.Chromatic> {T, T, S, T, T, T, S}.ToImmutableList();
        public IEnumerator<Interval.Chromatic> GetEnumerator() => _intervals.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _intervals.Count;
    }

    public class Minor : IReadOnlyCollection<Interval.Chromatic>
    {
        private static readonly IReadOnlyCollection<Interval.Chromatic> _intervals = new List<Interval.Chromatic> {T, S, T, T, S, T, T}.ToImmutableList();
        public IEnumerator<Interval.Chromatic> GetEnumerator() => _intervals.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _intervals.Count;
    }
}

