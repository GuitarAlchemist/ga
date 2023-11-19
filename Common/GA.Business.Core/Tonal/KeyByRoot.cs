namespace GA.Business.Core.Tonal;

using GA.Business.Core.Notes.Primitives;
using GA.Core.Collections;

public class KeyByRoot() : LazyIndexerBase<NaturalNote, Key>(GetKeyByRoot())
{
    public static Key Get(NaturalNote naturalNote) => _instance[naturalNote];

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