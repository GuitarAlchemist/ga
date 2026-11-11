namespace GeoTiles;

using System.Web;

/// <summary>Where tiles come from. Everything is returned as bytes in memory: nothing here writes a tile to disk,
/// which Google's terms forbid.</summary>
public interface ITileSource
{
    /// <summary>The URL of the root tileset.</summary>
    Uri Root { get; }

    Task<byte[]> GetAsync(Uri uri, CancellationToken cancel = default);
}

/// <summary>Google's Photorealistic 3D Tiles (Map Tiles API). The root request carries the API key; the tilesets
/// it returns carry a <c>session</c> in their URIs, and every later request must send both.</summary>
public sealed class GoogleTileSource(HttpClient http, string apiKey) : ITileSource
{
    private string? _session;

    public Uri Root { get; } = new("https://tile.googleapis.com/v1/3dtiles/root.json");

    /// <summary>The requests made so far: the API bills by root request and caps the tile requests per day.</summary>
    public int Requests { get; private set; }

    public async Task<byte[]> GetAsync(Uri uri, CancellationToken cancel = default)
    {
        var request = Sign(uri);
        Requests++;
        using var response = await http.GetAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
        {
            // Never the request URL: it carries the key.
            throw new HttpRequestException(
                $"{TilesetParser.Redact(uri)}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancel);
    }

    /// <summary>The URL to request for <paramref name="uri"/>: with the key, and with the session once a tileset
    /// has given one.</summary>
    public Uri Sign(Uri uri)
    {
        if (!string.Equals(uri.Host, Root.Host, StringComparison.OrdinalIgnoreCase) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"{TilesetParser.Redact(uri)}: not on {Root.Host}; the key is not sent elsewhere");
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        if (query["session"] is { Length: > 0 } session)
        {
            _session = session;
        }
        else if (_session is not null)
        {
            query["session"] = _session;
        }

        query["key"] = apiKey;
        return new UriBuilder(uri) { Query = query.ToString() }.Uri;
    }
}
