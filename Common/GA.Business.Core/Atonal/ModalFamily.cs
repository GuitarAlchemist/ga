namespace GA.Business.Core.Atonal;

using Primitives;

/// <summary>
/// Group of pitch class sets that share the same interval vector
/// </summary>
public class ModalFamily
{
    public static IEnumerable<ModalFamily> All => _lazyModalFamilies.Value.Values;
    public static IReadOnlySet<IntervalClassVector> ModalIntervalVectors => _lazyModalIntervalVectors.Value;
    public static bool TryGetValue(IntervalClassVector intervalVector, out ModalFamily? modalFamily) => _lazyModalFamilies.Value.TryGetValue(intervalVector, out modalFamily);

    static ModalFamily()
    {
        _lazyModalFamilies = new(() => ModalFamilyCollection.SharedInstance.ToDictionary(family => family.IntervalClassVector, family => family));
        _lazyModalIntervalVectors = new(() => _lazyModalFamilies.Value.Keys.ToImmutableHashSet());
    }

    private static readonly Lazy<Dictionary<IntervalClassVector, ModalFamily>> _lazyModalFamilies;
    private static readonly Lazy<IReadOnlySet<IntervalClassVector>> _lazyModalIntervalVectors;

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

    public override string ToString() => $"{NoteCount} notes - {IntervalClassVector} ({Modes.Count} items)";

    #region Inner classes

    private class ModalFamilyCollection : IEnumerable<ModalFamily>
    {
        public static readonly ModalFamilyCollection SharedInstance = new();

        public IEnumerator<ModalFamily> GetEnumerator()
        {
            var scaleSets = PitchClassSet.Objects.Where(pcs => PitchClassSetIdentity.ContainsRoot(pcs.Identity.Value));
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