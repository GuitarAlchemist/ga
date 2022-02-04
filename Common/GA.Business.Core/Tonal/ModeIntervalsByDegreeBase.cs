namespace GA.Business.Core.Tonal;

using Primitives;
using Intervals;
using Scales;
using GA.Core;

public abstract class ModeIntervalsByDegreeBase : IIndexer<ModalScaleDegree, IReadOnlyCollection<Interval.Simple>>
{
    protected ModeIntervalsByDegreeBase(Scale scale) => _intervalsByMode = new(scale);
    private readonly IntervalsByMode _intervalsByMode;

    public IReadOnlyCollection<Interval.Simple> this[ModalScaleDegree degree] => _intervalsByMode[degree.Value - 1];
}