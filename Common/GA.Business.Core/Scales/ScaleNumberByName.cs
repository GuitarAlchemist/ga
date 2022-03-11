namespace GA.Business.Core.Scales;

using System.Collections.Immutable;
using GA.Core;

public class ScaleNumberByName : LazyIndexerBase<string, int>
{
    public static int Get(string name) => _instance[name];

    public static IReadOnlyCollection<int> Find(string? name)
    {
        if (string.IsNullOrEmpty(name)) return ImmutableList<int>.Empty;

        var result =
            _instance.Dictionary
                .Where(pair => pair.Key.Contains(name))
                .Select(pair => pair.Value)
                .ToImmutableList();
        return result;
    }

    private static readonly ScaleNumberByName _instance = new();

    public ScaleNumberByName() : base(GetScaleNumberByName()) { }

    private static IReadOnlyDictionary<string, int> GetScaleNumberByName() => ScaleNameByNumber.Instance.Dictionary.ToImmutableDictionary(pair => pair.Value, pair => pair.Key);
}