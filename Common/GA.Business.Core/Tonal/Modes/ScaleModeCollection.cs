namespace GA.Business.Core.Tonal.Modes;

public class ScaleModeCollection<TScaleModeDegree, TScaleMode> : IReadOnlyCollection<TScaleMode>,
    IIndexer<TScaleModeDegree, TScaleMode>
    where TScaleModeDegree : struct, IRangeValueObject<TScaleModeDegree>
    where TScaleMode : ScaleMode<TScaleModeDegree>
{
    private readonly ImmutableDictionary<TScaleModeDegree, TScaleMode> _modeByDegree;
    private readonly ImmutableDictionary<int, TScaleMode> _modeByDegreeValue;

    public ScaleModeCollection(IReadOnlyCollection<TScaleMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);

        _modeByDegree = modes.ToImmutableDictionary(mode => mode.ParentScaleDegree);
        _modeByDegreeValue = modes.ToImmutableDictionary(mode => mode.ParentScaleDegree.Value);
    }

    public TScaleMode this[int degree] => _modeByDegreeValue[degree];
    public TScaleMode this[TScaleModeDegree degree] => _modeByDegree[degree];

    public IEnumerator<TScaleMode> GetEnumerator()
    {
        return _modeByDegree.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => _modeByDegree.Count;
}
