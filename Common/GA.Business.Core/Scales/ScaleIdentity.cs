namespace GA.Business.Core.Scales;

using System.Collections.Immutable;

using PCRE;

public class ScaleIdentity
{
    public static IReadOnlyList<ScaleIdentity> GetAll()
    {
        return ScaleNameByNumber.ValidScaleNumbers
            .Select(scaleNumber => new ScaleIdentity(scaleNumber))
            .ToImmutableList();
    }

    public static bool TryCreate(
        int scaleNumber,
        out ScaleIdentity scaleIdentity)
    {
        scaleIdentity = null!;
        if (!ScaleNameByNumber.IsValidScaleNumber(scaleNumber)) return false;
        scaleIdentity = new(scaleNumber);
        return true;
    }

    public ScaleIdentity(int scaleNumber)
    {
        if (!ScaleNameByNumber.IsValidScaleNumber(scaleNumber)) throw new InvalidOperationException($"Invalid scale number: {scaleNumber}");
        ScaleNumber = new(scaleNumber);
    }

    public ScaleNumber ScaleNumber { get; }
    public string ScaleName => ScaleNameByNumber.Get(ScaleNumber.Value);
    public string ScaleVideoUrl => ScaleVideoUrlByNumber.Get(ScaleNumber.Value);
    public string IanRingSiteUrl => $"https://ianring.com/musictheory/scales/{ScaleNumber.Value}";

    //language=regex
    private const string _regexPattern = @"""(?'youtubeurl'https:\/\/www\.youtube\.com\/embed\/[^""]*)""";

    public async Task<string?> GetYouTubeUrlAsync()
    {
        var httpClient = new HttpClient();
        var content = await httpClient.GetStringAsync(IanRingSiteUrl);
        var regEx = new PcreRegex(_regexPattern);
        var match = regEx.Match(content);
        if (!match.Success) return null;
        var result = match.Groups["youtubeurl"].Value;

        return result;
    }

    public override string ToString() => ScaleName;
}