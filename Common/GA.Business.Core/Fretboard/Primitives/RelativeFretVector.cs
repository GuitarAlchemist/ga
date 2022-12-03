namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core.Combinatorics;
using GA.Core.Collections;
using System.Collections.Generic;

/// <summary>
/// A vector of <see cref="RelativeFret"/> with translation equivalences.
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

    public override string ToString() => string.Join(" ", _relativeFretByStr.Values);

    /// <summary>
    /// A relative fret vector where the minimum relative fret is 0 (e.g. "0 2 2 2 0 0")
    /// </summary>
    [PublicAPI]
    public sealed class Normalized : RelativeFretVector
    {
        public Normalized(
            Variation<RelativeFret> variation,
            IReadOnlyCollection<Translated> translations) : base(variation)
        {
            Translations = translations;
        }

        /// <summary>
        /// Gets the <see cref="IReadOnlyCollection{Translated}"/>
        /// </summary>
        public IReadOnlyCollection<Translated> Translations { get; }

        public override string ToString() => Translations.Any() 
            ? $"{base.ToString()} (Normalized - {Translations.Count} translations)" 
            : $"{base.ToString()} (Normalized)";
    }

    /// <summary>
    /// A relative fret vector where the minimum relative fret is not 0, the vector can be normalized by translation (e.g. "1 3 3 3 1 1" => normalizable to "0 2 2 2 0 0")
    /// </summary>
    [PublicAPI]
    public sealed class Translated : RelativeFretVector
    {
        private readonly Func<Normalized> _normalizedVectorFactory;

        public Translated(
            Variation<RelativeFret> variation,
            Func<Normalized> normalizedVectorFactory) 
                : base(variation)
        {
            _normalizedVectorFactory = normalizedVectorFactory;
        }

        /// <summary>
        /// The translation amount.
        /// </summary>
        public int Value => this.Min().Value;

        /// <summary>
        /// Gets the <see cref="Normalized"/>.
        /// </summary>
        public Normalized Normalized => _normalizedVectorFactory.Invoke();

        public override string ToString() => $"{base.ToString()} (+ {Value} from {Normalized})";
    }
}