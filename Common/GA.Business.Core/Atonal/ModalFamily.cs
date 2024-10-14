namespace GA.Business.Core.Atonal;

using Primitives;

/// <summary>
/// Group of pitch class sets representing a scale that share the same interval vector
/// </summary>
public class ModalFamily : IStaticReadonlyCollection<ModalFamily>
{
    #region IStaticReadonlyCollection<ModalFamily> Members

    public static IReadOnlyCollection<ModalFamily> Items => _lazyModalFamilies.Value.Values;

    #endregion
    
    /// <summary>
    /// Gets the <see cref="IReadOnlySet{IntervalClassVector}"/>
    /// </summary>
    public static IReadOnlySet<IntervalClassVector> ModalIntervalVectors => _lazyModalIntervalVectors.Value;
    
    /// <summary>
    /// Attempts to retrieve a modal family given a <see cref="IntervalClassVector"/>
    /// </summary>
    /// <param name="intervalVector">The <see cref="IntervalClassVector"/></param>
    /// <param name="modalFamily">The <see cref="ModalFamily"/> if any, null otherwise</param>
    /// <returns>True if a modal family is found, false otherwise</returns>
    public static bool TryGetValue(
        IntervalClassVector intervalVector, 
        [MaybeNullWhen(false)]
        out ModalFamily modalFamily) => _lazyModalFamilies.Value.TryGetValue(intervalVector, out modalFamily);

    static ModalFamily()
    {
        _lazyModalFamilies = new(() => ModalFamilyCollection.SharedInstance.ToDictionary(family => family.IntervalClassVector, family => family));
        _lazyModalIntervalVectors = new(() => _lazyModalFamilies.Value.Keys.ToImmutableHashSet());
    }

    private static readonly Lazy<Dictionary<IntervalClassVector, ModalFamily>> _lazyModalFamilies;
    private static readonly Lazy<IReadOnlySet<IntervalClassVector>> _lazyModalIntervalVectors;

    /// <summary>
    /// Constructs a modal family
    /// </summary>
    /// <param name="noteCount">The number of notes</param>
    /// <param name="intervalClassVector">The <see cref="IntervalClassVector"/> shared my the members of the modal family</param>
    /// <param name="modes"></param>
    internal ModalFamily(
        int noteCount,
        IntervalClassVector intervalClassVector, 
        ImmutableList<PitchClassSet> modes)
    {
        NoteCount = noteCount;
        IntervalClassVector = intervalClassVector;
        Modes = modes;
        ModeIds = modes.Select(set => set.Id).ToImmutableList();
        PrimeMode = modes.MinBy(set => set.Id.Value)!;
    }

    /// <summary>
    /// Gets the number of notes in the modal family
    /// </summary>
    public int NoteCount { get; }
    
    /// <summary>
    /// Gets the <see cref="IntervalClassVector"/> shared by the members of the modal family
    /// </summary>
    public IntervalClassVector IntervalClassVector { get; }
    
    /// <summary>
    /// Gets modes <see cref="IReadOnlyCollection{PitchClassSet}"/> for the modal family
    /// </summary>
    public ImmutableList<PitchClassSet> Modes { get; }

    public ImmutableList<PitchClassSetId> ModeIds { get; }
    
    /// <summary>
    /// Gets the prime mode <see cref="PitchClassSet"/> for the modal family
    /// </summary>
    public PitchClassSet PrimeMode { get; }

    /// <inheritdoc />
    public override string ToString() => $"{NoteCount} notes - {IntervalClassVector} ({Modes.Count} items)";

    #region Inner classes

    private class ModalFamilyCollection : IEnumerable<ModalFamily>
    {
        public static readonly ModalFamilyCollection SharedInstance = new();

        public IEnumerator<ModalFamily> GetEnumerator()
        {
            var scaleSets = PitchClassSet.Items.Where(pcs => pcs.Contains(0));
            var scaleSetsByCount = scaleSets.ToLookup(set => set.Count);
            foreach (var countGrouping in scaleSetsByCount)
            {
                var noteCount = countGrouping.Key;
                var orderedSets = countGrouping.OrderBy(set => set.IntervalClassVector.Id);
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