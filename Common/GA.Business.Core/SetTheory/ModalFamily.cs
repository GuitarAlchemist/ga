namespace GA.Business.Core.SetTheory;

/// <summary>
/// Group of pitch class sets that share the same interval vector
/// </summary>
public class ModalFamily
{
    private static readonly Lazy<Dictionary<IntervalClassVector, ModalFamily>> _lazyModalFamilies;
    private static readonly Lazy<IReadOnlySet<IntervalClassVector>> _lazyModalIntervalVectors;
    public static IEnumerable<ModalFamily> All => _lazyModalFamilies.Value.Values;
    public static IReadOnlySet<IntervalClassVector> ModalIntervalVectors => _lazyModalIntervalVectors.Value;
    public static bool TryGetValue(IntervalClassVector intervalVector, out ModalFamily? modalFamily) => _lazyModalFamilies.Value.TryGetValue(intervalVector, out modalFamily);

    static ModalFamily()
    {
        _lazyModalFamilies = new(ModalFamilyByIntervalVector);
        _lazyModalIntervalVectors = new(ModalIntervalVectors);

        static Dictionary<IntervalClassVector, ModalFamily> ModalFamilyByIntervalVector() =>
            ModalFamilyCollection.SharedInstance
                .ToDictionary(
                    family => family.IntervalClassVector, 
                    family => family);

        static ImmutableHashSet<IntervalClassVector> ModalIntervalVectors() =>
            _lazyModalFamilies.Value.Keys.ToImmutableHashSet();
    }

    internal ModalFamily(
        int noteCount,
        IntervalClassVector intervalClassVector, 
        IReadOnlyCollection<PitchClassSet> modes)
    {
        NoteCount = noteCount;
        IntervalClassVector = intervalClassVector;
        Modes = modes;
        PrimeMode = modes.MinBy(set => set.Identity.Value)!;
    }

    public int NoteCount { get; }
    public IntervalClassVector IntervalClassVector { get; }
    public IReadOnlyCollection<PitchClassSet> Modes { get; }
    public PitchClassSet PrimeMode { get; }

    public override string ToString() => $"{NoteCount} notes - {IntervalClassVector.Description()} ({Modes.Count} items)";

    #region Inner classes

    private class ModalFamilyCollection : IEnumerable<ModalFamily>
    {
        public static readonly ModalFamilyCollection SharedInstance = new();

        public IEnumerator<ModalFamily> GetEnumerator()
        {
            var scaleSets = PitchClassSet.Objects.Where(set => PitchClassSetIdentity.ContainsRoot(set.Identity.Value));
            var scaleSetsByCount = scaleSets.ToLookup(set => set.Count);
            foreach (var countGrouping in scaleSetsByCount)
            {
                var noteCount = countGrouping.Key;
                var orderedSets = countGrouping.OrderBy(set => set.IntervalClassVector.Value);
                var setsByIntervalClassVector = orderedSets.ToLookup(set => set.IntervalClassVector);
                var modalGroups = setsByIntervalClassVector.Where(grouping => grouping.Count() > 1);

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