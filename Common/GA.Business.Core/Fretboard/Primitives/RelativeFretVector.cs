namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core.Combinatorics;
using GA.Core.Collections;
using System.Collections.Generic;

/// <summary>
/// List of <see cref="RelativeFret"/> items, indexed by string.
/// </summary>
[PublicAPI]
public abstract class RelativeFretVector : IReadOnlyList<RelativeFret>,
                                           IIndexer<Str, RelativeFret>
{
    #region IIndexer<Str, RelativeFret> Members

    public RelativeFret this[Str key] => throw new NotImplementedException();
    public BigInteger Index => _variation.Index;

    #endregion

    #region IReadOnlyCollection<RelativeFret> Members

    public RelativeFret this[int index] => _variation[index];
    public IEnumerator<RelativeFret> GetEnumerator() => _relativeFretByStr.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _relativeFretByStr.Count;

    #endregion

    private readonly Variation<RelativeFret> _variation;
    private readonly ImmutableDictionary<Str, RelativeFret> _relativeFretByStr;

    protected RelativeFretVector(Variation<RelativeFret> variation)
    {
        _relativeFretByStr =
            variation.Select((rFret, i) => (RelativeFret: rFret, Str: Str.FromValue(i + 1)))
                     .ToImmutableDictionary(tuple => tuple.Str, tuple => tuple.RelativeFret);
    }

    public override string ToString() => "+fret: " + string.Join(" ", _relativeFretByStr.Values);

    /// <summary>
    /// A prime form relative fret vector (e.g. "0 2 2 2 0 0")
    /// </summary>
    [PublicAPI]
    public sealed class PrimeForm : RelativeFretVector
    {
        public PrimeForm(
            Variation<RelativeFret> variation,
            IReadOnlyCollection<Translation> translations) 
                : base(variation)
        {
            Translations = translations;
        }

        /// <summary>
        /// Gets the <see cref="IReadOnlyCollection{Translated}"/>
        /// </summary>
        public IReadOnlyCollection<Translation> Translations { get; }

        public override string ToString() => Translations.Any() 
            ? $"{base.ToString()} (Prime - {Translations.Count} translations)" 
            : $"{base.ToString()} (Prime)";
    }

    /// <summary>
    /// A non-prime relative fret vector (e.g. "1 3 3 3 1 1" => "0 2 2 2 0 0" prime form)
    /// </summary>
    [PublicAPI]
    public sealed class Translation : RelativeFretVector
    {
        private readonly Func<PrimeForm> _primeFormFactory;

        public Translation(
            Variation<RelativeFret> variation,
            Func<PrimeForm> primeFormFactory) 
                : base(variation)
        {
            _primeFormFactory = primeFormFactory;
        }

        /// <summary>
        /// The translation amount.
        /// </summary>
        public int Value => this.Min().Value;

        /// <summary>
        /// Gets the <see cref="PrimeForm"/>.
        /// </summary>
        public PrimeForm PrimeForm => _primeFormFactory.Invoke();

        public override string ToString() => $"{base.ToString()} (+ {Value} from {PrimeForm})";
    }
}