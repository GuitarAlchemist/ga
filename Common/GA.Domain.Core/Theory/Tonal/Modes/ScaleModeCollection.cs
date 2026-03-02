namespace GA.Domain.Core.Theory.Tonal.Modes;

using GA.Core.Abstractions;
using GA.Core.Collections.Abstractions;

[CollectionBuilder(typeof(ScaleModeCollectionBuilder), nameof(ScaleModeCollectionBuilder.Create))]
public class ScaleModeCollection<TScaleModeDegree, TScaleMode>(IReadOnlyCollection<TScaleMode> modes)
    : IReadOnlyCollection<TScaleMode>,
        IIndexer<TScaleModeDegree, TScaleMode>
    where TScaleModeDegree : struct, IRangeValueObject<TScaleModeDegree>
    where TScaleMode : ScaleMode<TScaleModeDegree>
{
    private readonly ImmutableDictionary<TScaleModeDegree, TScaleMode> _modeByDegree =
        modes.ToImmutableDictionary(mode => mode.ParentScaleDegree);

    private readonly ImmutableDictionary<int, TScaleMode> _modeByDegreeValue =
        modes.ToImmutableDictionary(mode => mode.ParentScaleDegree.Value);

    public TScaleMode this[int degree] => _modeByDegreeValue[degree];
    public TScaleMode this[TScaleModeDegree degree] => _modeByDegree[degree];

    public IEnumerator<TScaleMode> GetEnumerator() => _modeByDegree.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _modeByDegree.Count;
}

public static class ScaleModeCollectionBuilder
{
    public static ScaleModeCollection<TScaleModeDegree, TScaleMode> Create<TScaleModeDegree, TScaleMode>(
        ReadOnlySpan<TScaleMode> modes)
        where TScaleModeDegree : struct, IRangeValueObject<TScaleModeDegree>
        where TScaleMode : ScaleMode<TScaleModeDegree>
        => new(modes.ToArray());
}
