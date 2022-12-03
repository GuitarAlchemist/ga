namespace GA.Business.Core.Fretboard.Positions;

using GA.Core.Extensions;
using GA.Core.Combinatorics;
using Primitives;
using static Primitives.RelativeFretVector;

/// <summary>
/// Collection of <see cref="RelativeFretVector"/>
/// </summary>
[PublicAPI]
public class RelativeFretVectorCollection : IReadOnlyCollection<RelativeFretVector>
{
    #region IReadOnlyCollection{RelativeFretVector} Members

    public IEnumerator<RelativeFretVector> GetEnumerator() => _factory.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count { get; }

    #endregion

    private readonly VariationsRelativeFretVectorFactory _factory;

    /// <summary>
    /// Creates a <see cref="RelativeFretVectorCollection"/> instance.
    /// </summary>
    /// <param name="strCount">The number of strings (Defaulted to 6 string0)</param>
    public RelativeFretVectorCollection(int strCount = 6)
    {
        var variations = new VariationsWithRepetitions<RelativeFret>(RelativeFret.Items, strCount);
        Count = (int) variations.Count;
        _factory = new(variations);
        Normalized = this.OfType<Normalized>().ToLazyCollection();
        Translated = this.OfType<Translated>().ToLazyCollection();
    }

    /// <summary>
    /// Gets the <see cref="VariationEquivalenceCollection.Translation{RelativeFret}"/>
    /// </summary>
    public VariationEquivalenceCollection.Translation<RelativeFret> Equivalences => _factory.Equivalences;

    /// <summary>
    /// Gets the <see cref="RelativeFretVector.Normalized"/>
    /// </summary>
    public IReadOnlyCollection<Normalized> Normalized { get; }

    /// <summary>
    /// Gets the <see cref="RelativeFretVector.Translated"/>
    /// </summary>
    public IReadOnlyCollection<Translated> Translated { get; }
}