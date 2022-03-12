namespace GA.Business.Core.Scales;

using System.Collections.Immutable;
using GA.Core;

public class ScaleNumberByName : LazyIndexerBase<string, ScaleNumber>
{
    public static int Get(string name) => _instance[name];

    public static IReadOnlyCollection<ScaleNumber> Find(string? name)
    {
        if (string.IsNullOrEmpty(name)) return ImmutableList<ScaleNumber>.Empty;

        var result =
            _instance.Dictionary
                .Where(pair => pair.Key.Contains(name))
                .Select(pair => pair.Value)
                .ToImmutableList();
        return result;
    }

    private static readonly ScaleNumberByName _instance = new();

    public ScaleNumberByName() : base(GetScaleNumberByName()) { }

    private static IReadOnlyDictionary<string, ScaleNumber> GetScaleNumberByName() => ScaleNameByNumber.Instance.Dictionary.ToImmutableDictionary(pair => pair.Value, pair => pair.Key);
}