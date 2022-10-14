namespace GA.Business.Core.Scales;



using GA.Core;
using Atonal.Primitives;

public class ScaleNameByIdentity : LazyIndexerBase<PitchClassSetIdentity, string>
{
    public static bool IsValidScaleNumber(PitchClassSetIdentity pitchClassSetIdentity) => Instance.Dictionary.ContainsKey(pitchClassSetIdentity);
    public static IReadOnlyList<PitchClassSetIdentity> ValidScaleNumbers => Instance.Dictionary.Keys.ToImmutableList();
    public static string Get(PitchClassSetIdentity pitchClassSetIdentity)
    {
        if (!IsValidScaleNumber(pitchClassSetIdentity)) return string.Empty;
        return Instance[pitchClassSetIdentity];
    }

    public ScaleNameByIdentity() : base(GetScaleNameByIdentity()) { }

    internal static readonly ScaleNameByIdentity Instance = new();

    // ReSharper disable once InconsistentNaming
    private static IReadOnlyDictionary<PitchClassSetIdentity, string> GetScaleNameByIdentity()
    {
        // TODO: Populate? (Cannot use copyrighted resource from Ian Ring's website)
        var dict = new Dictionary<PitchClassSetIdentity, string>
        {
            [1365] = "Whole Tone",
            [1387] = "Locrian",
            [1451] = "Phrygian",
            [1453] = "Aeolian",
            [1709] = "Dorian",
            [1717] = "Mixolydian",
            [2741] = "Major",
            [2773] = "Lydian",
        };
        // ReSharper restore StringLiteralTypo

        var result = dict.ToImmutableDictionary();

        return result;
    }
}