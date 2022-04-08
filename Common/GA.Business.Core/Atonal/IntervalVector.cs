namespace GA.Business.Core.Atonal;

using System.Collections.Immutable;
using Intervals.Primitives;
using Notes;

/// <summary>
///  Interval vector class.
/// </summary>
/// <remarks>
/// See Prime Form: https://www.youtube.com/watch?v=KFKMvFzobbw
/// </remarks>
public class IntervalVector
{
    #region Equality members

    public static bool operator ==(IntervalVector? left, IntervalVector? right) => Equals(left, right);
    public static bool operator !=(IntervalVector? left, IntervalVector? right) => !Equals(left, right);
    protected bool Equals(IntervalVector other) => Value == other.Value;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((IntervalVector) obj);
    }

    public override int GetHashCode() => Value;

    #endregion

    public IntervalVector(IReadOnlyCollection<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));

        var intervalHistogram = GetIntervalHistogram(notes);
        Value = GetValue(intervalHistogram);
    }

    public int Value { get; }

    public static implicit operator int(IntervalVector vector) => vector.Value;

    public override string ToString() => Value.ToString();

    private static IReadOnlyCollection<int> GetIntervalHistogram(IReadOnlyCollection<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));

        var histogram = new[] {0, 0, 0, 0, 0, 0};
        var maxSemitones = (Semitones) 6;
        foreach (var note1 in notes)
        {
            foreach (var note2 in notes)
            {
                if (note1 == note2) continue;
                var semitones = note1.GetInterval(note2).ToSemitones();
                if (semitones < 1) continue;
                if (semitones > maxSemitones) continue; // Beyond symmetry boundary, don't measure twice
                var i = (int) semitones - 1;
                if (semitones == maxSemitones) histogram[i] = 1; // On symmetry boundary, don't measure twice
                else histogram[i] += 1;
            }
        }

        var result = histogram.ToImmutableList();

        return result;
    }

    private static int GetValue(IEnumerable<int> intervalHistogram)
    {
        if (intervalHistogram == null) throw new ArgumentNullException(nameof(intervalHistogram));

        var result = 0;
        var weight = 1;
        var semitones = (Semitones) 1;
        foreach (var count in intervalHistogram.Reverse())
        {
            result += weight * count;
            semitones++;
            weight *= 10;
        }

        return result;
    }
}

