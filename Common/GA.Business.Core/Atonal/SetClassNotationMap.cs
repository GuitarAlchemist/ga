namespace GA.Business.Core.Atonal;

using System.Text.Json;

/// <summary>
///     Lazy loader for set-class notation mapping data (Forte/Rahn indices) keyed by canonical prime-form string.
/// </summary>
[PublicAPI]
internal static class SetClassNotationMap
{
    private static readonly Lazy<MappingData> _lazy = new(Load, isThreadSafe: true);

    public static IReadOnlyDictionary<string, int> RahnIndexByPrimeForm => _lazy.Value.RahnIndexByPrimeForm;
    public static IReadOnlyDictionary<string, int> ForteIndexByPrimeForm => _lazy.Value.ForteIndexByPrimeForm;
    public static IReadOnlyDictionary<(int cardinality, int index), string> PrimeFormByRahnNumber => _lazy.Value.PrimeFormByRahnNumber;
    public static IReadOnlyDictionary<(int cardinality, int index), string> PrimeFormByForteNumber => _lazy.Value.PrimeFormByForteNumber;

    private static MappingData Load()
    {
        // Find the embedded resource whose name ends with the JSON filename
        const string fileName = "SetClassNotationMap.json";
        var assembly = typeof(SetClassNotationMap).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            // In absence of data, return an empty map; UI will display placeholders for Rahn
            return new MappingData(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<(int cardinality, int index), string>(),
                new Dictionary<(int cardinality, int index), string>());
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new MappingData(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<(int cardinality, int index), string>(),
                new Dictionary<(int cardinality, int index), string>());
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<Row>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<Row>();

            // Build maps
            var rahnByPrime = new Dictionary<string, int>(StringComparer.Ordinal);
            var forteByPrime = new Dictionary<string, int>(StringComparer.Ordinal);
            var primeByRahn = new Dictionary<(int cardinality, int index), string>();
            var primeByForte = new Dictionary<(int cardinality, int index), string>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.PrimeForm)) continue;

                // Optional: lenient validation of cardinality
                if (row.Cardinality is < 0 or > 12) continue;

                // Forte index
                if (row.ForteIndex > 0 && !forteByPrime.ContainsKey(row.PrimeForm))
                {
                    forteByPrime[row.PrimeForm] = row.ForteIndex;
                    var key = (row.Cardinality, row.ForteIndex);
                    if (!primeByForte.ContainsKey(key))
                    {
                        primeByForte[key] = row.PrimeForm;
                    }
                }

                // Rahn index
                if (row.RahnIndex > 0 && !rahnByPrime.ContainsKey(row.PrimeForm))
                {
                    rahnByPrime[row.PrimeForm] = row.RahnIndex;
                    var key = (row.Cardinality, row.RahnIndex);
                    if (!primeByRahn.ContainsKey(key))
                    {
                        primeByRahn[key] = row.PrimeForm;
                    }
                }
            }

            return new MappingData(
                rahnByPrime,
                forteByPrime,
                primeByRahn,
                primeByForte);
        }
        catch
        {
            // Be robust: return empty mapping on any error
            return new MappingData(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<(int cardinality, int index), string>(),
                new Dictionary<(int cardinality, int index), string>());
        }
    }

    private sealed record MappingData(
        IReadOnlyDictionary<string, int> RahnIndexByPrimeForm,
        IReadOnlyDictionary<string, int> ForteIndexByPrimeForm,
        IReadOnlyDictionary<(int cardinality, int index), string> PrimeFormByRahnNumber,
        IReadOnlyDictionary<(int cardinality, int index), string> PrimeFormByForteNumber);

    private sealed record Row(int Cardinality, string PrimeForm, int ForteIndex, int RahnIndex);
}
