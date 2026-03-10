namespace GaMcpServer.Tools;

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
        var client = httpClientFactory.CreateClient("gaapi");
        var encoded = Uri.EscapeDataString(key);
        var response = await client.GetAsync($"/api/contextual-chords/keys/{encoded}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool]
    [Description("Get the chords for a specific scale by scale name and root note. Returns JSON with chord data.")]
    public async Task<string> GetScaleChords(
        [Description("The scale name, e.g. 'pentatonic', 'blues', 'harmonic minor'")] string scaleName,
        [Description("The root note, e.g. 'C', 'G#', 'Bb'")] string rootName,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var encodedScale = Uri.EscapeDataString(scaleName);
        var encodedRoot = Uri.EscapeDataString(rootName);
        var response = await client.GetAsync($"/api/contextual-chords/scales/{encodedScale}/{encodedRoot}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool]
    [Description("Get the chords for a specific mode by mode name and root note. Returns JSON with chord data.")]
    public async Task<string> GetModeChords(
        [Description("The mode name, e.g. 'dorian', 'phrygian', 'lydian'")] string modeName,
        [Description("The root note, e.g. 'D', 'E', 'F'")] string rootName,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var encodedMode = Uri.EscapeDataString(modeName);
        var encodedRoot = Uri.EscapeDataString(rootName);
        var response = await client.GetAsync($"/api/contextual-chords/modes/{encodedMode}/{encodedRoot}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool]
    [Description("Get borrowed chords available for a given key (chords borrowed from parallel modes). Returns JSON with borrowed chord data.")]
    public async Task<string> GetBorrowedChords(
        [Description("The key to get borrowed chords for, e.g. 'C major'")] string key,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var encoded = Uri.EscapeDataString(key);
        var response = await client.GetAsync($"/api/contextual-chords/borrowed/{encoded}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
