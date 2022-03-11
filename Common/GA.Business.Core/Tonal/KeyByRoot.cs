namespace GA.Business.Core.Tonal;

using System.Collections.Immutable;

using GA.Business.Core.Notes.Primitives;
using GA.Core;

public class KeyByRoot : LazyIndexerBase<NaturalNote, Key>
{
    public static Key Get(NaturalNote naturalNote) => _instance[naturalNote];

    public KeyByRoot() : base(GetKeyByRoot()) { }

    private static readonly KeyByRoot _instance = new();
    private static IReadOnlyDictionary<NaturalNote, Key> GetKeyByRoot()
    {
        var result =
            Key.Major
                .GetAll()
                .Cast<Key>()
                .Where(key => !key.Root.Accidental.HasValue)
                .ToImmutableDictionary(key => key.Root.NaturalNote);

        return result;
    }
}