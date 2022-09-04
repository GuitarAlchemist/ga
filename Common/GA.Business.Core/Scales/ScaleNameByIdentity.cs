namespace GA.Business.Core.Scales;

using Atonal;
using GA.Core;

public class ScaleNameByIdentity : LazyIndexerBase<PitchClassSetIdentity, string>
{
    public static bool IsValidScaleNumber(PitchClassSetIdentity pitchClassSetIdentity) => Instance.Dictionary.ContainsKey(pitchClassSetIdentity);
    public static IReadOnlyList<PitchClassSetIdentity> ValidScaleNumbers => Instance.Dictionary.Keys.ToImmutableList();
    public static string Get(PitchClassSetIdentity pitchClassSetIdentity)
    {
        if (!IsValidScaleNumber(pitchClassSetIdentity)) return string.Empty;
        return Instance[pitchClassSetIdentity];
    }

    public ScaleNameByIdentity() : base(GetScaleNameByNumber()) { }

    internal static readonly ScaleNameByIdentity Instance = new();
    private static IReadOnlyDictionary<PitchClassSetIdentity, string> GetScaleNameByNumber()
    {
        // TODO: Populate? (Cannot use copyrighted resource from Ian Ring's website)
        var dict = new Dictionary<PitchClassSetIdentity, string>
        {
        };
        // ReSharper restore StringLiteralTypo

        var result = dict.ToImmutableDictionary();

        return result;
    }
}