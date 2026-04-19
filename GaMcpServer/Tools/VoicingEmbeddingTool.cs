namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class VoicingEmbeddingTool
{
    private static readonly Lazy<MusicalEmbeddingGenerator> Generator = new(() =>
        new MusicalEmbeddingGenerator(
            new IdentityVectorService(),
            new TheoryVectorService(),
            new MorphologyVectorService(),
            new ContextVectorService(),
            new SymbolicVectorService(),
            new ModalVectorService(),
            new PhaseSphereService(),
            new RootVectorService()));

    /// <summary>
    ///     HttpClient for calling GaApi's voicing-retrieve endpoint. Base address is read from
    ///     the <c>GA_API_URL</c> env var (default <c>http://localhost:5232</c>).
    /// </summary>
    private static readonly Lazy<HttpClient> GaApiClient = new(() =>
    {
        var baseUrl = Environment.GetEnvironmentVariable("GA_API_URL") ?? "http://localhost:5232";
        return new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
    });

    // Open string MIDI notes per instrument (high string to low string, 1-based index)
    private static readonly Dictionary<string, int[]> OpenStringMidi = new()
    {
        ["guitar"] = [64, 59, 55, 50, 45, 40],   // E4 B3 G3 D3 A2 E2
        ["bass"] = [43, 38, 33, 28],               // G2 D2 A1 E1
        ["ukulele"] = [69, 64, 60, 67],            // A4 E4 C4 G4
    };

    [McpServerTool]
    [Description("Generate a 228-dim OPTIC-K embedding vector for a chord voicing. Returns JSON with embedding array and metadata.")]
    public static async Task<string> GaGenerateVoicingEmbedding(
        [Description("Voicing diagram (e.g. 'x-3-2-0-1-0' for Cmaj7)")] string diagram,
        [Description("Instrument: guitar, bass, or ukulele")] string instrument)
    {
        var voicing = ParseDiagram(diagram, instrument.ToLowerInvariant());
        var analysis = VoicingAnalyzer.Analyze(voicing);
        var doc = VoicingDocumentFactory.FromAnalysis(voicing, analysis, tuningId: instrument);
        var embedding = await Generator.Value.GenerateEmbeddingAsync(doc);

        return JsonSerializer.Serialize(new
        {
            dimension = EmbeddingSchema.TotalDimension,
            schema = EmbeddingSchema.Version,
            embedding,
            metadata = new
            {
                diagram,
                instrument,
                chordName = analysis.ChordId.ChordName,
                midiNotes = analysis.MidiNotes,
                dropVoicing = analysis.VoicingCharacteristics.DropVoicing,
                isRootless = analysis.VoicingCharacteristics.IsRootless
            }
        });
    }

    [McpServerTool]
    [Description("Get the OPTIC-K embedding schema info: partitions, dimensions, weights.")]
    public static string GaGetEmbeddingSchema()
    {
        return JsonSerializer.Serialize(new
        {
            version = EmbeddingSchema.Version,
            totalDimension = EmbeddingSchema.TotalDimension,
            partitions = new object[]
            {
                new { name = "IDENTITY", offset = EmbeddingSchema.IdentityOffset, dim = EmbeddingSchema.IdentityDim, weight = 0.0, role = "filter" },
                new { name = "STRUCTURE", offset = EmbeddingSchema.StructureOffset, dim = EmbeddingSchema.StructureDim, weight = EmbeddingSchema.StructureWeight },
                new { name = "MORPHOLOGY", offset = EmbeddingSchema.MorphologyOffset, dim = EmbeddingSchema.MorphologyDim, weight = EmbeddingSchema.MorphologyWeight },
                new { name = "CONTEXT", offset = EmbeddingSchema.ContextOffset, dim = EmbeddingSchema.ContextDim, weight = EmbeddingSchema.ContextWeight },
                new { name = "SYMBOLIC", offset = EmbeddingSchema.SymbolicOffset, dim = EmbeddingSchema.SymbolicDim, weight = EmbeddingSchema.SymbolicWeight },
                new { name = "EXTENSIONS", offset = EmbeddingSchema.ExtensionsOffset, dim = EmbeddingSchema.ExtensionsDim, weight = 0.0, role = "info" },
                new { name = "SPECTRAL", offset = EmbeddingSchema.SpectralOffset, dim = EmbeddingSchema.SpectralDim, weight = 0.0, role = "info" },
                new { name = "MODAL", offset = EmbeddingSchema.ModalOffset, dim = EmbeddingSchema.ModalDim, weight = 0.10 },
                new { name = "HIERARCHY", offset = EmbeddingSchema.HierarchyOffset, dim = EmbeddingSchema.HierarchyDim, weight = 0.0, role = "info" },
                new { name = "ATONAL_MODAL", offset = EmbeddingSchema.AtonalModalOffset, dim = EmbeddingSchema.AtonalModalDim, weight = 0.0, role = "info" }
            }
        });
    }

    [McpServerTool]
    [Description("Search OPTIC-K voicing index by text query. Returns top-K voicings grounded in the 313k-voicing embedded corpus via Ollama text embedding. Requires GaApi running (default http://localhost:5232; override with GA_API_URL env var).")]
    public static async Task<string> GaSearchVoicingsByQuery(
        [Description("Free-text query, e.g. 'warm jazz Cmaj7 voicing' or 'drop 2 minor seventh'")] string query,
        [Description("Number of voicings to return (1-50, default 10)")] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { error = "query is required" });
        }

        var clamped = Math.Clamp(limit, 1, 50);

        try
        {
            var response = await GaApiClient.Value.PostAsJsonAsync(
                "/api/voicings/retrieve",
                new { query, limit = clamped });

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"GaApi returned {(int)response.StatusCode} {response.StatusCode}",
                    body
                });
            }
            return body;
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Could not reach GaApi. Is it running?",
                details = ex.Message,
                gaApiUrl = GaApiClient.Value.BaseAddress?.ToString()
            });
        }
    }

    private static Voicing ParseDiagram(string diagram, string instrument)
    {
        var parts = diagram.Split('-');

        if (!OpenStringMidi.TryGetValue(instrument, out var openMidi))
            throw new ArgumentException($"Unknown instrument: {instrument}. Expected guitar, bass, or ukulele.");

        if (parts.Length != openMidi.Length)
            throw new ArgumentException($"Diagram has {parts.Length} strings but {instrument} expects {openMidi.Length}");

        var positions = new List<Position>();
        var notes = new List<MidiNote>();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            var str = new Str(i + 1);

            if (part is "x" or "X")
            {
                positions.Add(new Position.Muted(str));
            }
            else if (int.TryParse(part, out var fretValue))
            {
                var fret = new Fret(fretValue);
                var location = new PositionLocation(str, fret);
                var midiNoteValue = openMidi[i] + fretValue;
                var midiNote = new MidiNote(midiNoteValue);

                positions.Add(new Position.Played(location, midiNote));
                notes.Add(midiNote);
            }
            else
            {
                throw new ArgumentException($"Invalid fret value: '{part}' at string {i + 1}");
            }
        }

        return new Voicing([.. positions], [.. notes]);
    }
}
