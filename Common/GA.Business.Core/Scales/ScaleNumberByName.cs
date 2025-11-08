namespace GA.Business.Core.Scales;

using Atonal.Primitives;

public class ScaleNumberByName() : LazyIndexerBase<string, PitchClassSetId>(GetScaleNumberByName())
{
    private static readonly ScaleNumberByName _instance = new();

    public static int Get(string name)
    {
        return _instance[name];
    }

    public static IReadOnlyCollection<PitchClassSetId> Find(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return ImmutableList<PitchClassSetId>.Empty;
        }

        var result =
            _instance.Dictionary
                .Where(pair => pair.Key.Contains(name))
                .Select(pair => pair.Value)
                .ToImmutableList();
        return result;
    }

    private static IReadOnlyDictionary<string, PitchClassSetId> GetScaleNumberByName()
    {
        return ScaleNameById.Instance.Dictionary.ToImmutableDictionary(pair => pair.Value, pair => pair.Key);
    }
}
