namespace GaMcpServer.Tools;

using ModelContextProtocol;
using ModelContextProtocol.Server;

[McpServerToolType]
public class ContextualChordsTool(IHttpClientFactory httpClientFactory)
{
    [McpServerTool]
    [Description("Get the diatonic chords for a given key (e.g. 'C major', 'A minor'). Returns JSON with chord names, degrees, and functions.")]
    public async Task<string> GetDiatonicChords(
        [Description("The key to get diatonic chords for, e.g. 'C major' or 'G minor'")] string key,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(key);
        return await GetFromGaApiAsync($"/api/contextual-chords/keys/{encoded}", cancellationToken);
    }

    [McpServerTool]
    [Description("Get the chords for a specific scale by scale name and root note. Returns JSON with chord data.")]
    public async Task<string> GetScaleChords(
        [Description("The scale name, e.g. 'pentatonic', 'blues', 'harmonic minor'")] string scaleName,
        [Description("The root note, e.g. 'C', 'G#', 'Bb'")] string rootName,
        CancellationToken cancellationToken = default)
    {
        var encodedScale = Uri.EscapeDataString(scaleName);
        var encodedRoot = Uri.EscapeDataString(rootName);
        return await GetFromGaApiAsync($"/api/contextual-chords/scales/{encodedScale}/{encodedRoot}", cancellationToken);
    }

    [McpServerTool]
    [Description("Get the chords for a specific mode by mode name and root note. Returns JSON with chord data.")]
    public async Task<string> GetModeChords(
        [Description("The mode name, e.g. 'dorian', 'phrygian', 'lydian'")] string modeName,
        [Description("The root note, e.g. 'D', 'E', 'F'")] string rootName,
        CancellationToken cancellationToken = default)
    {
        var encodedMode = Uri.EscapeDataString(modeName);
        var encodedRoot = Uri.EscapeDataString(rootName);
        return await GetFromGaApiAsync($"/api/contextual-chords/modes/{encodedMode}/{encodedRoot}", cancellationToken);
    }

    [McpServerTool]
    [Description("Get guitar voicings for a chord symbol, sorted by playability. Returns JSON array of {chordName, frets, difficulty} objects where frets is a 6-element array (low-E to high-e): -1=muted, 0=open, 1-12=fret number.")]
    public async Task<string> GetChordVoicings(
        [Description("The chord symbol, e.g. 'Am7', 'Cmaj7', 'G', 'F#m'")] string chord,
        [Description("Optional max difficulty filter: 1=beginner, 2=intermediate, 3=advanced")] int? maxDifficulty = null,
        [Description("Optional minimum fret position")] int? minFret = null,
        [Description("Optional maximum fret position")] int? maxFret = null,
        [Description("Exclude voicings with open strings (default false)")] bool noOpenStrings = false,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(chord);
        var query = new System.Text.StringBuilder($"/api/contextual-chords/voicings/{encoded}?noOpenStrings={noOpenStrings}");
        if (maxDifficulty.HasValue) query.Append($"&maxDifficulty={maxDifficulty}");
        if (minFret.HasValue)       query.Append($"&minFret={minFret}");
        if (maxFret.HasValue)       query.Append($"&maxFret={maxFret}");
        return await GetFromGaApiAsync(query.ToString(), cancellationToken);
    }

    [McpServerTool]
    [Description("Get borrowed chords available for a given key (chords borrowed from parallel modes). Returns JSON with borrowed chord data.")]
    public async Task<string> GetBorrowedChords(
        [Description("The key to get borrowed chords for, e.g. 'C major'")] string key,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(key);
        return await GetFromGaApiAsync($"/api/contextual-chords/borrowed/{encoded}", cancellationToken);
    }

    /// <summary>
    /// GETs a GaApi resource and returns its body. Failures become an <see cref="McpException"/>
    /// with the status and GaApi's message: any other exception reaches the client as the SDK's
    /// bare "An error occurred invoking '…'", which hid whether GaApi was down or rejected the call.
    /// </summary>
    private async Task<string> GetFromGaApiAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(pathAndQuery, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"GaApi is unreachable at {client.BaseAddress} ({ex.Message}). Is GaApi running?", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new McpException($"GaApi at {client.BaseAddress} did not answer within {client.Timeout.TotalSeconds:0} s.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new McpException(
                    $"GaApi returned {(int)response.StatusCode} {response.ReasonPhrase} for {pathAndQuery}: {body}");
            return body;
        }
    }
}
