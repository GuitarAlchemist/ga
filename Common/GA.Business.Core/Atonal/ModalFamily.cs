namespace GA.Business.Core.Atonal;

using Notes;

/// <summary>
/// Group of pitch class sets that share the same interval vector
/// </summary>
public class ModalFamily
{
    private static readonly Lazy<Dictionary<IntervalVector, ModalFamily>> _lazyModalFamilies;
    private static readonly Lazy<IReadOnlySet<IntervalVector>> _lazyModalIntervalVectors;
    public static IEnumerable<ModalFamily> All => _lazyModalFamilies.Value.Values;
    public static IReadOnlySet<IntervalVector> ModalIntervalVectors => _lazyModalIntervalVectors.Value;
    public static bool TryGetValue(IntervalVector intervalVector, out ModalFamily? modalFamily) => _lazyModalFamilies.Value.TryGetValue(intervalVector, out modalFamily);

    static ModalFamily()
    {
        _lazyModalFamilies = new(ModalFamilyByIntervalVector);
        _lazyModalIntervalVectors = new(ModalIntervalVectors);

        static Dictionary<IntervalVector, ModalFamily> ModalFamilyByIntervalVector() =>
            ModalFamilyCollection.SharedInstance
                .ToDictionary(
                    family => family.IntervalVector, 
                    family => family);

        static ImmutableHashSet<IntervalVector> ModalIntervalVectors() =>
            _lazyModalFamilies.Value.Keys.ToImmutableHashSet();
    }

    internal ModalFamily(
        int noteCount,
        IntervalVector intervalVector, 
        IReadOnlyCollection<PitchClassSet> modes)
    {
        NoteCount = noteCount;
        IntervalVector = intervalVector;
        Modes = modes;
        PrimeMember = modes.MinBy(set => set.Identity.Value)!;
    }

    public int NoteCount { get; }
    public IntervalVector IntervalVector { get; }
    public IReadOnlyCollection<PitchClassSet> Modes { get; }
    public PitchClassSet PrimeMember { get; }

    public override string ToString() => $"{NoteCount} notes - {IntervalVector.Description()} ({Modes.Count} items)";

    #region Inner classes

    private class ModalFamilyCollection : IEnumerable<ModalFamily>
    {
        public static readonly ModalFamilyCollection SharedInstance = new();

        public IEnumerator<ModalFamily> GetEnumerator()
        {
            var sets = PitchClassSet.Enumerate().ToImmutableList();
            var setsByCount = sets.ToLookup(set => set.Count);
            foreach (var countGrouping in setsByCount)
            {
                var noteCount = countGrouping.Key;
                var orderedSets = countGrouping.OrderBy(set => set.IntervalVector.Value);
                var setsByIntervalVector = orderedSets.ToLookup(set => set.IntervalVector);
                var modalGroups = setsByIntervalVector.Where(grouping => grouping.Count() > 1);

                foreach (var modalGroup in modalGroups)
                {
                    var intervalVector = modalGroup.Key;
                    var members = modalGroup.ToImmutableList();
                    var modalFamily = new ModalFamily(noteCount, intervalVector, members);
                    yield return modalFamily;
                }           
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }    

    #endregion

}