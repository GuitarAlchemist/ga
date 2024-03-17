namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core;
using GA.Core.Combinatorics;
using GA.Core.Collections;
using System.Collections.Generic;

/// <summary>
/// List of <see cref="RelativeFret"/> items, indexed by string.
/// </summary>
[PublicAPI]
public abstract class RelativeFretVector : IReadOnlyList<RelativeFret>,
                                           IIndexer<Str, RelativeFret>,
                                           IValueObject
{
    #region IIndexer<Str, RelativeFret> Members

    public RelativeFret this[Str str] => _relativeFretByStr[str];

    #endregion

    #region IReadOnlyCollection<RelativeFret> Members

    public RelativeFret this[int index] => _variation[index];
    public IEnumerator<RelativeFret> GetEnumerator() => _relativeFretByStr.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _relativeFretByStr.Count;

    #endregion

    public int Value => (int) _variation.Index;
    public abstract bool IsPrime { get; }

    private readonly Variation<RelativeFret> _variation;
    private readonly ImmutableSortedDictionary<Str, RelativeFret> _relativeFretByStr;

    protected RelativeFretVector(Variation<RelativeFret> variation)
    {
        _relativeFretByStr =
            variation.Select((rFret, i) => (RelativeFret: rFret, Str: Str.FromValue(i + 1)))
                     .ToImmutableSortedDictionary(tuple => tuple.Str, tuple => tuple.RelativeFret);
    }

    /// <summary>
    /// Create a fret vector.
    /// </summary>
    /// <param name="startFret">The start <see cref="Fret"/></param>
    /// <returns>The <see cref="FretVector"/>.</returns>
    public FretVector ToFretVector(Fret startFret) => 
        new(_relativeFretByStr.Values.Select(relativeFret => startFret + relativeFret));

    public override string ToString() => "+fret: " + string.Join(" ", _relativeFretByStr.Values);

    /// <summary>
    /// A prime form relative fret vector (e.g. "0 2 2 2 0 0")
    /// </summary>
    [PublicAPI]
    public sealed class PrimeForm(
            Variation<RelativeFret> variation,
            IReadOnlyCollection<Translation> translations)
        : RelativeFretVector(variation)
    {
        public override bool IsPrime => true;

        /// <summary>
        /// Gets the <see cref="IReadOnlyCollection{Translated}"/>
        /// </summary>
        public IReadOnlyCollection<Translation> Translations { get; } = translations;

        public override string ToString() => Translations.Any() 
            ? $"{base.ToString()} (Prime - {Translations.Count} translations)" 
            : $"{base.ToString()} (Prime)";
    }

    /// <summary>
    /// A non-prime relative fret vector (e.g. "1 3 3 3 1 1" => "0 2 2 2 0 0" prime form)
    /// </summary>
    [PublicAPI]
    public sealed class Translation(Variation<RelativeFret> variation, Func<PrimeForm> primeFormFactory) : RelativeFretVector(variation)
    {
        /// <summary>
        /// The translation displacement amount compared to the prime form.
        /// </summary>
        public int Increment => this.Min().Value;

        public override bool IsPrime => false;

        /// <summary>
        /// Gets the <see cref="PrimeForm"/>.
        /// </summary>
        public new PrimeForm PrimeForm => primeFormFactory.Invoke();

        public override string ToString() => $"{base.ToString()} (+ {Increment} from {PrimeForm})";
    }
}