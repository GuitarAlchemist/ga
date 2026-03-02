namespace GA.Domain.Core.Theory.Tonal.Scales;

using Atonal;
using GA.Core.Collections;

public class ScaleNumberByName() : LazyIndexerBase<string, PitchClassSetId>(GetScaleNumberByName())
{
    private static readonly ScaleNumberByName _instance = new();

    public static int Get(string name) => _instance[name];

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

    private static IReadOnlyDictionary<string, PitchClassSetId> GetScaleNumberByName() =>
        ScaleNameById.Instance.Dictionary.ToImmutableDictionary(pair => pair.Value, pair => pair.Key);
}
