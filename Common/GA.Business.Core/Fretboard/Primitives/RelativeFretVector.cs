namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core.Combinatorics;

/// <summary>
///     List of <see cref="RelativeFret" /> items, indexed by string.
/// </summary>
[PublicAPI]
public abstract class RelativeFretVector : IEnumerable
{
    private readonly ImmutableSortedDictionary<Str, RelativeFret> _relativeFretByStr;
    private readonly Variation<RelativeFret> _variation;

    protected RelativeFretVector(Variation<RelativeFret> variation)
    {
        _variation = variation;
        _relativeFretByStr = variation
            .Select((rFret, i) => (RelativeFret: rFret, Str: Str.FromValue(i + 1)))
            .ToImmutableSortedDictionary(tuple => tuple.Str, tuple => tuple.RelativeFret);
    }

    #region IIndexer<Str, RelativeFret> Members

    public RelativeFret this[Str str] => _relativeFretByStr[str];

    #endregion

    public int Value => (int)_variation.Index;
    public abstract bool IsPrime { get; }

    /// <summary>
    ///     Create a fret vector.
    /// </summary>
    /// <param name="startFret">The start <see cref="Fret" /></param>
    /// <returns>The <see cref="FretVector" />.</returns>
    public FretVector ToFretVector(Fret startFret)
    {
        return new FretVector(_relativeFretByStr.Values.Select(relativeFret => startFret + relativeFret));
    }

    public override string ToString()
    {
        return "+fret: " + string.Join(" ", _relativeFretByStr.Values);
    }

    /// <summary>
    ///     A prime form relative fret vector (e.g. "0 2 2 2 0 0")
    /// </summary>
    [PublicAPI]
    public sealed class PrimeForm(
        Variation<RelativeFret> variation,
        IReadOnlyCollection<Translation> translations)
        : RelativeFretVector(variation)
    {
        public override bool IsPrime => true;

        /// <summary>
        ///     Gets the <see cref="IReadOnlyCollection{Translated}" />
        /// </summary>
        public IReadOnlyCollection<Translation> Translations { get; } = translations;

        public override string ToString()
        {
            return Translations.Any()
                ? $"{base.ToString()} (Prime - {Translations.Count} translations)"
                : $"{base.ToString()} (Prime)";
        }
    }

    /// <summary>
    ///     A non-prime relative fret vector (e.g. "1 3 3 3 1 1" => "0 2 2 2 0 0" prime form)
    /// </summary>
    [PublicAPI]
    public sealed class Translation(
        Variation<RelativeFret> variation,
        Func<PrimeForm> primeFormFactory)
        : RelativeFretVector(variation)
    {
        public int Increment => _relativeFretByStr.Values.Min(rf => rf.Value);

        public override bool IsPrime => false;

        /// <summary>
        ///     Gets the prime form of this translation.
        /// </summary>
        public PrimeForm PrimeFormValue => primeFormFactory.Invoke();

        public override string ToString()
        {
            return $"{base.ToString()} (+ {Increment} from {PrimeFormValue})";
        }
    }

    #region IReadOnlyCollection<RelativeFret> Members

    public RelativeFret this[int index] => _variation[index];

    public IEnumerator<RelativeFret> GetEnumerator()
    {
        return _relativeFretByStr.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => _relativeFretByStr.Count;

    #endregion
}
