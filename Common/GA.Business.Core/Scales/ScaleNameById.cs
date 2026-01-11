namespace GA.Business.Core.Scales;

using System.Collections.Generic;
using System.Collections.Immutable;
using Atonal.Primitives;
using GA.Core.Collections;

public class ScaleNameById() : LazyIndexerBase<PitchClassSetId, string>(GetScaleNameById())
{
    public static bool IsValidScaleNumber(PitchClassSetId id)
    {
        return Instance.Dictionary.ContainsKey(id);
    }

    public static IReadOnlyList<PitchClassSetId> ValidScaleNumbers => [.. Instance.Dictionary.Keys];

    public static string Get(PitchClassSetId id)
    {
        if (!IsValidScaleNumber(id))
        {
            return string.Empty;
        }

        return Instance[id];
    }

    internal static readonly ScaleNameById Instance = new();

    // ReSharper disable once InconsistentNaming
    private static IReadOnlyDictionary<PitchClassSetId, string> GetScaleNameById()
    {
        // TODO: Populate? (Cannot use copyrighted resource from Ian Ring's website)
        var dict = new Dictionary<PitchClassSetId, string>
        {
            [1365] = "Whole Tone",
            [1387] = "Locrian",
            [1451] = "Phrygian",
            [1453] = "Aeolian",
            [1709] = "Dorian",
            [1717] = "Mixolydian",
            [2741] = "Major",
            [2773] = "Lydian"
        };
        // ReSharper restore StringLiteralTypo

        var result = dict.ToImmutableDictionary();

        return result;
    }
}
