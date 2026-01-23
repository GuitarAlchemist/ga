using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using GA.Business.ML.Embeddings;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using GaChatbot.Services;

namespace GaChatbot.Services.Infrastructure;

public class GaApiClientVectorIndex(HttpClient httpClient, ILogger<GaApiClientVectorIndex> logger) : IVectorIndex
{
    public IReadOnlyList<VoicingDocument> Documents => [];

    public IEnumerable<(VoicingDocument Doc, double Score)> Search(
        double[] queryVector, 
        int topK = 10)
    {
        // Synchronous wrapper over async API call - suboptimal but required by interface
        // In real app, interface should be async.
        return SearchAsync(queryVector, topK, null, null, null, null).GetAwaiter().GetResult();
    }
    
    public IEnumerable<(VoicingDocument Doc, double Score)> Search(
        double[] queryVector, 
        int topK,
        string? quality,
        string? extension,
        string? stackingType,
        int? noteCount)
    {
        return SearchAsync(queryVector, topK, quality, extension, stackingType, noteCount).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<(VoicingDocument Doc, double Score)>> SearchAsync(
        double[] queryVector, 
        int topK,
        string? quality,
        string? extension,
        string? stackingType,
        int? noteCount)
    {
        try
        {
            var request = new HybridSearchRequest(
                Query: "", // Ignored by server when Vector is provided
                Quality: quality,
                Extension: extension,
                StackingType: stackingType,
                NoteCount: noteCount,
                Limit: topK,
                Vector: queryVector
            );

            var response = await httpClient.PostAsJsonAsync("api/search/hybrid", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Remote search failed: {StatusCode} {Error}", response.StatusCode, error);
                return [];
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<ChordSearchResultDto>>();
            if (dtos == null) return [];

            return dtos.Select(dto => (MapToDocument(dto), dto.Score));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Remote search exception");
            return [];
        }
    }

    private VoicingDocument MapToDocument(ChordSearchResultDto dto)
    {
        return new VoicingDocument
        {
            Id = dto.Id.ToString(),
            ChordName = dto.Name,
            SearchableText = $"{dto.Name} {dto.Description} {dto.Quality} {dto.Extension}",
            SemanticTags = [dto.Quality, dto.Extension, dto.StackingType],
            Diagram = dto.Diagram, // Now populated
            MidiNotes = dto.MidiNotes, // Now populated
            Embedding = dto.Embedding, // Now populated
            
            // Required properties with dummies or mapped values
            PitchClasses = [], // Would need full doc or re-calculation
            PitchClassSet = "",
            IntervalClassVector = "",
            AnalysisEngine = "GaApi",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard", // Assumption
            PitchClassSetId = "",
            YamlAnalysis = "{}",
            PossibleKeys = [],
            StackingType = dto.StackingType,
            
             // Populate others as needed by explanation service
             Difficulty = "Intermediate" // Dummy if not returned
        };
    }

    public VoicingDocument? FindByIdentity(string identity)
    {
        // TODO: Implement FindByIdentity in GaApi or filter logic
        // For now returning null makes "Identity Lookup" fail, falling back to vector search, which is acceptable
        return null; 
    }

    public async Task<bool> IsStaleAsync(string currentSchemaVersion)
    {
        // TODO: Add staleness check to GaApi
        return false; 
    }
}

public record HybridSearchRequest(
    string Query,
    string? Quality = null,
    string? Extension = null,
    string? StackingType = null,
    int? NoteCount = null,
    int Limit = 10,
    int NumCandidates = 100,
    double[]? Vector = null
);

public class ChordSearchResultDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string StackingType { get; set; } = string.Empty;
    public int NoteCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
    public double[]? Embedding { get; set; }
    public int[] MidiNotes { get; set; } = [];
    public string Diagram { get; set; } = string.Empty;
}
