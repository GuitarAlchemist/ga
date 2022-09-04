namespace GA.Business.Core.Atonal;

using Intervals.Primitives;
using Notes;

/// <summary>
/// Interval vector class
/// </summary>
/// <remarks>
/// See https://en.wikipedia.org/wiki/Interval_vector,  https://musictheory.pugetsound.edu/mt21c/IntervalVector.html
/// See Prime Form: https://www.youtube.com/watch?v=KFKMvFzobbw
/// 
/// All major scale modes share the same interval vector - Example:
/// - Major scale => 254361
/// - Dorian      => 254361
/// </remarks>
public class IntervalVector : IReadOnlyCollection<int>
{
    private readonly IReadOnlyCollection<int> _intervalVector;

    #region Equality members

    public static bool operator ==(IntervalVector? left, IntervalVector? right) => Equals(left, right);
    public static bool operator !=(IntervalVector? left, IntervalVector? right) => !Equals(left, right);
    protected bool Equals(IntervalVector other) => Value == other.Value;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((IntervalVector) obj);
    }

    public override int GetHashCode() => Value;

    #endregion

    #region Enumerable members

    public IEnumerator<int> GetEnumerator() => _intervalVector.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _intervalVector).GetEnumerator();
    public int Count => _intervalVector.Count;

    #endregion

    public IntervalVector(IReadOnlyCollection<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));

        var intervalVector = GetIntervalVector(notes);
        _intervalVector = intervalVector;
        Value = GetValue(intervalVector);

        static ImmutableList<int> GetIntervalVector(IReadOnlyCollection<Note> notes)
        {
            if (notes == null) throw new ArgumentNullException(nameof(notes));

            var vector = new[] {0, 0, 0, 0, 0, 0};
            var notePairs = new HashSet<(Note, Note)>();
            foreach (var note1 in notes)
            {
                foreach (var note2 in notes)
                {
                    if (note1 == note2) continue;
                    if (notePairs.Contains((note2, note1))) continue; // Exclude note pairs already counted 
                    var intervalClass = note1.GetIntervalClass(note2);
                    if (intervalClass == Semitones.None) continue;
                    var index = (int) intervalClass - 1;
                    vector[index] += 1;
                    notePairs.Add((note1, note2));
                }
            }

            return vector.ToImmutableList();
        }

        static int GetValue(IEnumerable<int> intervalVector)
        {
            if (intervalVector == null) throw new ArgumentNullException(nameof(intervalVector));

            var result = 0;
            var weight = 1;
            foreach (var count in intervalVector.Reverse())
            {
                result += weight * count;
                weight *= 10;
            }

            return result;
        }
    }

    public int Value { get; }
    public static implicit operator int(IntervalVector vector) => vector.Value;

    public override string ToString() => Description();

    public string Description()
    {
        var sb = new StringBuilder();
        sb.Append("<");
        sb.Append(string.Join(",", _intervalVector));
        sb.Append(">");
        return sb.ToString();
    }
}
