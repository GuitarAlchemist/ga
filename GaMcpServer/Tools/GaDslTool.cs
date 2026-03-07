namespace GaMcpServer.Tools;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using ModelContextProtocol.Server;
using System.Text.Json;

// F# module aliases — the dotted module name becomes the C# namespace, the
// last segment becomes the class name.
using GaReg = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;
using GaCat = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureCategory;

/// <summary>
/// MCP tools that invoke GA DSL closures in-process via <see cref="GaReg.Global"/>.
/// No GaApi server required — the F# module initializers register all builtins
/// automatically when the assembly is first loaded.
/// </summary>
[McpServerToolType]
public static class GaDslTool
{
    // ── MCP closure allowlist ─────────────────────────────────────────────────
    // io.*, agent.*, and tab.* closures have side-effects (file I/O, outbound
    // HTTP) and must not be reachable from unauthenticated MCP clients via the
    // generic GaInvokeClosure escape hatch.
    // tab.* is blocked here because tab.fetchUrl makes unchecked HTTP requests
    // to caller-supplied URLs (SSRF). Use the dedicated GaSearchTabs tool for
    // tab searching — it calls tab.fetch with a fixed query parameter only.

    private static bool IsPermittedForMcp(string name) =>
        !name.StartsWith("io.", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("agent.", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("tab.", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("pipeline.", StringComparison.OrdinalIgnoreCase);

    // ── Bridge ────────────────────────────────────────────────────────────────

    private static async Task<string> InvokeAsync(
        string closureName,
        params (string Key, object Value)[] inputs)
    {
        try
        {
            var map = MapModule.OfSeq(
                inputs.Select(kv => Tuple.Create(kv.Key, kv.Value)));

            var result = await FSharpAsync.StartAsTask(
                GaReg.Global.Invoke(closureName, map),
                FSharpOption<TaskCreationOptions>.None,
                FSharpOption<CancellationToken>.None);

            return result.IsOk
                ? FormatResult(result.ResultValue)
                : $"Error: {result.ErrorValue}";
        }
        catch (Exception ex)
        {
            return $"Exception in {closureName}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static async Task<string> InvokeJsonAsync(string closureName, string paramsJson)
    {
        List<Tuple<string, object>> pairs;
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            pairs = doc.RootElement.EnumerateObject()
                .Select(p => Tuple.Create(p.Name, (object)(p.Value.GetString() ?? "")))
                .ToList();
        }
        catch (JsonException ex)
        {
            return $"Error parsing params JSON: {ex.Message}";
        }

        try
        {
            var map = MapModule.OfSeq(pairs);

            var result = await FSharpAsync.StartAsTask(
                GaReg.Global.Invoke(closureName, map),
                FSharpOption<TaskCreationOptions>.None,
                FSharpOption<CancellationToken>.None);

            return result.IsOk
                ? FormatResult(result.ResultValue)
                : $"Error: {result.ErrorValue}";
        }
        catch (Exception ex)
        {
            return $"Exception in {closureName}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string FormatResult(object? value) =>
        value switch
        {
            string[] arr => string.Join(", ", arr),
            object[] arr => string.Join(", ", arr.Select(x => x?.ToString() ?? "")),
            null         => "(no result)",
            _            => value.ToString() ?? "(no result)"
        };

    // ── Music-theory tools ────────────────────────────────────────────────────

    [McpServerTool]
    [Description(
        "Parse a chord symbol and return its structure as JSON " +
        "(root note, quality, extensions, bass note). " +
        "Examples: Am7 → minor triad + minor 7th; Cmaj9 → major triad + major 7th + major 9th.")]
    public static Task<string> GaParseChord(
        [Description("Chord symbol, e.g. 'Am7', 'Cmaj9', 'G7b9/D', 'Bdim7'")] string symbol)
        => InvokeAsync("domain.parseChord", ("symbol", symbol));

    [McpServerTool]
    [Description(
        "Return the interval names (P1, m3, P5, m7 …) contained in a chord. " +
        "Useful for understanding chord colour and voice-leading possibilities.")]
    public static Task<string> GaChordIntervals(
        [Description("Chord symbol, e.g. 'Am7', 'Cmaj9'")] string symbol)
        => InvokeAsync("domain.chordIntervals", ("symbol", symbol));

    [McpServerTool]
    [Description(
        "Transpose a chord symbol by N semitones. " +
        "Positive = up, negative = down. " +
        "Flat-key spelling is preserved (Bb stays Bb, not A#).")]
    public static Task<string> GaTransposeChord(
        [Description("Chord symbol to transpose, e.g. 'Am7'")] string symbol,
        [Description("Semitones to shift — positive = up, negative = down")] int semitones)
        => InvokeAsync("domain.transposeChord", ("symbol", (object)symbol), ("semitones", (object)semitones));

    [McpServerTool]
    [Description(
        "Return the 7 diatonic triads for a key as chord symbols. " +
        "Example: G major → G, Am, Bm, C, D, Em, F#dim.")]
    public static Task<string> GaDiatonicChords(
        [Description("Root note, e.g. 'G', 'Bb', 'F#'")] string root,
        [Description("Scale type: 'major' or 'minor'")] string scale)
        => InvokeAsync("domain.diatonicChords", ("root", root), ("scale", scale));

    [McpServerTool]
    [Description(
        "Return the relative major or minor key for a given root and scale. " +
        "Example: A minor → C major; G major → E minor.")]
    public static Task<string> GaRelativeKey(
        [Description("Root note, e.g. 'A', 'G'")] string root,
        [Description("Scale type: 'major' or 'minor'")] string scale)
        => InvokeAsync("domain.relativeKey", ("root", root), ("scale", scale));

    [McpServerTool]
    [Description(
        "Infer the key of a chord progression and annotate each chord with a Roman numeral. " +
        "Pass chords as a space-separated string. " +
        "Example: 'G D Em C' → Key: G major, I V vi IV.")]
    public static Task<string> GaAnalyzeProgression(
        [Description("Space-separated chord symbols, e.g. 'G D Em C' or 'Am F C G'")] string chords)
        => InvokeAsync("domain.analyzeProgression", ("chords", chords));

    [McpServerTool]
    [Description(
        "Find notes shared between two chords — useful for pivot-chord and voice-leading analysis. " +
        "Example: G7 and Cmaj7 share G (P1/P5) and B (M3/M7).")]
    public static Task<string> GaCommonTones(
        [Description("First chord symbol, e.g. 'G7'")] string chord1,
        [Description("Second chord symbol, e.g. 'Cmaj7'")] string chord2)
        => InvokeAsync("domain.commonTones", ("chord1", chord1), ("chord2", chord2));

    [McpServerTool]
    [Description(
        "Suggest chord substitutions: diatonic swaps ranked by common tones, " +
        "plus tritone sub for dominant 7th chords. " +
        "Example: Am in key of C major → C (★★★, relative major), Em (★★, shared E/B), F (★, shared A).")]
    public static Task<string> GaChordSubstitutions(
        [Description("Chord symbol to find substitutions for, e.g. 'Am', 'G7', 'Cmaj7'")] string symbol,
        [Description("Key root, e.g. 'C', 'G'. Defaults to chord root if omitted.")] string key = "",
        [Description("Scale type: 'major' or 'minor'. Defaults to 'major'.")] string scale = "major")
    {
        var inputs = new List<(string, object)> { ("symbol", symbol) };
        if (!string.IsNullOrEmpty(key)) inputs.Add(("key", key));
        if (!string.IsNullOrEmpty(scale)) inputs.Add(("scale", scale));
        return InvokeAsync("domain.chordSubstitutions", [.. inputs]);
    }

    // ── Tab tools ─────────────────────────────────────────────────────────────

    [McpServerTool]
    [Description(
        "Search free tab sources (Archive.org, GitHub) for guitar tabs matching an artist or song. " +
        "Returns direct URLs to text-format tabs. No authentication required.")]
    public static Task<string> GaSearchTabs(
        [Description("Search terms — artist name, song title, or both. E.g. 'metallica nothing else matters'")] string query)
        => InvokeAsync("tab.fetch", ("query", query));

    // ── Generic fallback ──────────────────────────────────────────────────────

    [McpServerTool]
    [Description(
        "Invoke any GA DSL closure by name with a JSON params object. " +
        "Use GaListClosures to discover available names and their InputSchema. " +
        "Example: name='domain.queryChords', params='{\"key\":\"G\",\"scale\":\"major\",\"quality\":\"minor\"}'.")]
    public static Task<string> GaInvokeClosure(
        [Description("Closure name, e.g. 'domain.queryChords', 'tab.parseAscii', 'domain.projectChord'")] string name,
        [Description("JSON object of input parameters, e.g. '{\"key\":\"G\",\"scale\":\"major\"}'")]
        string paramsJson)
    {
        if (!IsPermittedForMcp(name))
            return Task.FromResult($"Error: closure '{name}' is not accessible via MCP (side-effect categories are restricted).");
        return InvokeJsonAsync(name, paramsJson);
    }

    [McpServerTool]
    [Description(
        "List all available GA DSL closures with their names, categories, input schemas, and descriptions. " +
        "Useful before calling GaInvokeClosure to discover parameter names.")]
    public static string GaListClosures(
        [Description("Optional category filter: 'domain', 'pipeline', 'agent', or 'io'. Leave empty for all.")] string? category = null)
    {
        var closures = GaReg.Global.List(FSharpOption<GaCat>.None);

        var filtered = closures.Where(c =>
            string.IsNullOrEmpty(category) ||
            c.Category.ToString().Equals(category, StringComparison.OrdinalIgnoreCase));

        var lines = filtered
            .OrderBy(c => c.Category.ToString())
            .ThenBy(c => c.Name)
            .Select(c =>
            {
                var schema = c.InputSchema.IsEmpty
                    ? "(no inputs)"
                    : string.Join(", ", c.InputSchema.Select(kv => $"{kv.Key}: {kv.Value}"));
                return $"[{c.Category}] {c.Name}\n  {c.Description}\n  Inputs: {schema}\n  Returns: {c.OutputType}";
            });

        return string.Join("\n\n", lines);
    }
}
