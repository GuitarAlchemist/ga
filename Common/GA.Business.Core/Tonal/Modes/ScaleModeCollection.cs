namespace GA.Business.Core.Tonal.Modes;

using System.Collections;
using System.Collections.Immutable;

using GA.Core;

public class ScaleModeCollection<TScaleModeDegree, TScaleMode> : IReadOnlyCollection<TScaleMode>, IIndexer<TScaleModeDegree, TScaleMode>
    where TScaleModeDegree : IValueObject
    where TScaleMode : ScaleMode<TScaleModeDegree>
{
    private readonly ImmutableDictionary<TScaleModeDegree, TScaleMode> _modeByDegree;
    private readonly ImmutableDictionary<int, TScaleMode> _modeByDegreeValue;

    public ScaleModeCollection(IReadOnlyCollection<TScaleMode> modes)
    {
        if (modes == null) throw new ArgumentNullException(nameof(modes));

        _modeByDegree = modes.ToImmutableDictionary(mode => mode.ParentScaleDegree);
        _modeByDegreeValue = modes.ToImmutableDictionary(mode => mode.ParentScaleDegree.Value);
    }

    public IEnumerator<TScaleMode> GetEnumerator() => _modeByDegree.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _modeByDegree.Count;
    public TScaleMode this[TScaleModeDegree degree] => _modeByDegree[degree];
    public TScaleMode this[int degree] => _modeByDegreeValue[degree];
}