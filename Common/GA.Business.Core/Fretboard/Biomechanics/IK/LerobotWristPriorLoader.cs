namespace GA.Business.Core.Fretboard.Biomechanics.IK;

using System.Text.Json;

internal static class LerobotWristPriorLoader
{
    private const string ResourceName = "GA.Business.Core.Fretboard.Biomechanics.IK.Data.lerobot-wrist-priors.json";
    private static WristPrior? _cached;
    private static bool _attempted;

    public static WristPrior? LoadDefaultPrior()
    {
        if (_attempted)
        {
            return _cached;
        }

        _attempted = true;

        if (TryLoadEmbedded(out var prior))
        {
            _cached = prior;
            return _cached;
        }

        if (TryLoadRemote(out prior))
        {
            _cached = prior;
            return _cached;
        }

        return null;
    }

    private static bool TryLoadEmbedded(out WristPrior prior)
    {
        prior = default;

        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return false;
            }

            return TryParse(stream, out prior);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLoadRemote(out WristPrior prior)
    {
        prior = default;
        var url = Environment.GetEnvironmentVariable("LEROBOT_PRIOR_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var response = client.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var stream = response.Content.ReadAsStream();
            return TryParse(stream, out prior);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParse(Stream stream, out WristPrior prior)
    {
        prior = default;

        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("priors", out var priorsElement))
        {
            return false;
        }

        var enumerator = priorsElement.EnumerateArray();
        if (!enumerator.MoveNext())
        {
            return false;
        }

        var priorElement = enumerator.Current;
        var mean = ReadVector(priorElement, "mean");
        var std = ReadVector(priorElement, "stddev");

        prior = new WristPrior(mean, std);
        return true;
    }

    private static Vector3 ReadVector(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values))
        {
            return Vector3.Zero;
        }

        var enumerator = values.EnumerateArray();

        float ReadComponent()
        {
            if (!enumerator.MoveNext())
            {
                return 0f;
            }

            return (float)enumerator.Current.GetDouble();
        }

        var x = ReadComponent();
        var y = ReadComponent();
        var z = ReadComponent();

        return new Vector3(x, y, z);
    }
}
