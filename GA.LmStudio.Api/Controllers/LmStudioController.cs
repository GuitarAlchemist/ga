using Microsoft.AspNetCore.Mvc;
using GA.Data.MongoDB.Services;
using GA.Business.Core.AI.Services.Embeddings;
using MongoDB.Bson;
using System.Text;

namespace GA.LmStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LmStudioController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<LmStudioController> _logger;

    public LmStudioController(
        MongoDbService mongoDbService,
        IEmbeddingService embeddingService,
        ILogger<LmStudioController> logger)
    {
        _mongoDbService = mongoDbService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves context for a query using vector search
    /// </summary>
    [HttpPost("context")]
    public async Task<IActionResult> GetContext([FromBody] QueryRequest request)
    {
        try
        {
            var context = await RetrieveContextAsync(request.Query, request.Limit);
            return Ok(new { context });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context for query: {Query}", request.Query);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request model for context retrieval
    /// </summary>
    public class QueryRequest
    {
        /// <summary>
        /// The user's query
        /// </summary>
        public string Query { get; set; } = string.Empty;
        
        /// <summary>
        /// Maximum number of results to return
        /// </summary>
        public int Limit { get; set; } = 5;
    }

    /// <summary>
    /// Retrieves context for a query using vector search
    /// </summary>
    private async Task<string> RetrieveContextAsync(string query, int limit = 5)
    {
        try
        {
            // Generate embedding for the query
            var embedding = await _embeddingService.GenerateEmbeddingAsync(query);
            
            // Retrieve relevant documents from MongoDB using vector search
            var results = await _mongoDbService.SearchVectorAsync("musical_objects", "vector_index", embedding, limit);
            
            // Format the results as context
            return FormatResultsAsContext(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context for query: {Query}", query);
            return string.Empty;
        }
    }

    /// <summary>
    /// Formats MongoDB documents as context for LM Studio
    /// </summary>
    private string FormatResultsAsContext(List<BsonDocument> results)
    {
        var sb = new StringBuilder();
        
        foreach (var result in results)
        {
            if (!result.Contains("type"))
                continue;
                
            string type = result["type"].AsString;
            
            switch (type)
            {
                case "chord":
                    FormatChordDocument(result, sb);
                    break;
                case "scale":
                    FormatScaleDocument(result, sb);
                    break;
                case "note":
                    FormatNoteDocument(result, sb);
                    break;
                case "position":
                    FormatPositionDocument(result, sb);
                    break;
                case "instrument":
                    FormatInstrumentDocument(result, sb);
                    break;
                default:
                    FormatGenericDocument(result, sb);
                    break;
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private void FormatChordDocument(BsonDocument doc, StringBuilder sb)
    {
        sb.AppendLine($"CHORD: {doc["name"].AsString}");
        
        if (doc.Contains("notes") && doc["notes"].IsBsonArray)
        {
            sb.Append("Notes: ");
            sb.AppendLine(string.Join(", ", doc["notes"].AsBsonArray.Select(n => n.AsString)));
        }
        
        if (doc.Contains("positions") && doc["positions"].IsBsonArray)
        {
            sb.AppendLine("Fretboard positions:");
            foreach (var pos in doc["positions"].AsBsonArray)
            {
                sb.AppendLine($"- {FormatPosition(pos.AsBsonDocument)}");
            }
        }
        
        if (doc.Contains("description") && !doc["description"].IsBsonNull)
        {
            sb.AppendLine($"Description: {doc["description"].AsString}");
        }
    }
    
    private void FormatScaleDocument(BsonDocument doc, StringBuilder sb)
    {
        sb.AppendLine($"SCALE: {doc["name"].AsString}");
        
        if (doc.Contains("notes") && doc["notes"].IsBsonArray)
        {
            sb.Append("Notes: ");
            sb.AppendLine(string.Join(", ", doc["notes"].AsBsonArray.Select(n => n.AsString)));
        }
        
        if (doc.Contains("intervals") && doc["intervals"].IsBsonArray)
        {
            sb.Append("Intervals: ");
            sb.AppendLine(string.Join(", ", doc["intervals"].AsBsonArray.Select(i => i.AsString)));
        }
        
        if (doc.Contains("positions") && doc["positions"].IsBsonArray)
        {
            sb.AppendLine("Fretboard positions:");
            foreach (var pos in doc["positions"].AsBsonArray)
            {
                sb.AppendLine($"- Position {pos["position"].AsInt32}: {FormatScalePosition(pos.AsBsonDocument)}");
            }
        }
        
        if (doc.Contains("description") && !doc["description"].IsBsonNull)
        {
            sb.AppendLine($"Description: {doc["description"].AsString}");
        }
    }
    
    private void FormatNoteDocument(BsonDocument doc, StringBuilder sb)
    {
        sb.AppendLine($"NOTE: {doc["name"].AsString}");
        
        if (doc.Contains("pitchClass") && !doc["pitchClass"].IsBsonNull)
        {
            sb.AppendLine($"Pitch Class: {doc["pitchClass"].AsInt32}");
        }
        
        if (doc.Contains("positions") && doc["positions"].IsBsonArray)
        {
            sb.AppendLine("Fretboard positions:");
            foreach (var pos in doc["positions"].AsBsonArray)
            {
                sb.AppendLine($"- {FormatPosition(pos.AsBsonDocument)}");
            }
        }
    }
    
    private void FormatPositionDocument(BsonDocument doc, StringBuilder sb)
    {
        sb.AppendLine("FRETBOARD POSITION:");
        
        if (doc.Contains("string") && doc.Contains("fret"))
        {
            sb.AppendLine($"String: {doc["string"].AsInt32}, Fret: {doc["fret"].AsInt32}");
        }
        
        if (doc.Contains("note") && !doc["note"].IsBsonNull)
        {
            sb.AppendLine($"Note: {doc["note"].AsString}");
        }
        
        if (doc.Contains("midiNote") && !doc["midiNote"].IsBsonNull)
        {
            sb.AppendLine($"MIDI Note: {doc["midiNote"].AsInt32}");
        }
    }
    
    private void FormatInstrumentDocument(BsonDocument doc, StringBuilder sb)
    {
        sb.AppendLine($"INSTRUMENT: {doc["name"].AsString}");
        
        if (doc.Contains("tunings") && doc["tunings"].IsBsonArray)
        {
            sb.AppendLine("Tunings:");
            foreach (var tuning in doc["tunings"].AsBsonArray)
            {
                sb.AppendLine($"- {tuning["name"].AsString}: {tuning["notes"].AsString}");
            }
        }
        
        if (doc.Contains("description") && !doc["description"].IsBsonNull)
        {
            sb.AppendLine($"Description: {doc["description"].AsString}");
        }
    }
    
    private void FormatGenericDocument(BsonDocument doc, StringBuilder sb)
    {
        if (doc.Contains("name"))
        {
            sb.AppendLine($"{doc["type"].AsString.ToUpper()}: {doc["name"].AsString}");
        }
        else
        {
            sb.AppendLine($"{doc["type"].AsString.ToUpper()}:");
        }
        
        foreach (var element in doc.Elements)
        {
            if (element.Name == "type" || element.Name == "_id" || element.Name == "embedding")
                continue;
                
            if (element.Value.IsBsonArray)
            {
                sb.AppendLine($"{element.Name}: {FormatArray(element.Value.AsBsonArray)}");
            }
            else if (element.Value.IsBsonDocument)
            {
                sb.AppendLine($"{element.Name}: {FormatDocument(element.Value.AsBsonDocument)}");
            }
            else if (!element.Value.IsBsonNull)
            {
                sb.AppendLine($"{element.Name}: {element.Value}");
            }
        }
    }
    
    private string FormatPosition(BsonDocument position)
    {
        if (position.Contains("string") && position.Contains("fret"))
        {
            return $"String {position["string"].AsInt32}, Fret {position["fret"].AsInt32}";
        }
        
        return position.ToString();
    }
    
    private string FormatScalePosition(BsonDocument position)
    {
        if (position.Contains("startFret"))
        {
            return $"Starting at fret {position["startFret"].AsInt32}";
        }
        
        return position.ToString();
    }
    
    private string FormatArray(BsonArray array)
    {
        return string.Join(", ", array.Select(item => 
            item.IsBsonDocument ? FormatDocument(item.AsBsonDocument) : 
            item.IsBsonArray ? FormatArray(item.AsBsonArray) : 
            item.ToString()));
    }
    
    private string FormatDocument(BsonDocument doc)
    {
        var properties = doc.Elements
            .Where(e => !e.Value.IsBsonNull)
            .Select(e => $"{e.Name}: {e.Value}");
            
        return $"{{{string.Join(", ", properties)}}}";
    }
    
    /// <summary>
    /// Generates a fretboard ASCII visualization
    /// </summary>
    private string GenerateFretboardAscii(BsonDocument position, int startFret = 0, int endFret = 5)
    {
        // Create a grid representing a section of the fretboard
        var fretboard = new string[6, endFret - startFret + 1];
        
        // Initialize with empty positions
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < endFret - startFret + 1; j++)
                fretboard[i, j] = "-";
        
        // Mark the positions from the document
        if (position.Contains("positions") && position["positions"].IsBsonArray)
        {
            foreach (var pos in position["positions"].AsBsonArray)
            {
                if (pos.IsBsonDocument && pos.AsBsonDocument.Contains("string") && pos.AsBsonDocument.Contains("fret"))
                {
                    int stringIndex = pos.AsBsonDocument["string"].AsInt32 - 1; // 0-based index
                    int fretIndex = pos.AsBsonDocument["fret"].AsInt32 - startFret;
                    
                    if (stringIndex >= 0 && stringIndex < 6 && fretIndex >= 0 && fretIndex < endFret - startFret + 1)
                    {
                        fretboard[stringIndex, fretIndex] = "O";
                    }
                }
            }
        }
        
        // Convert to ASCII representation
        var sb = new StringBuilder();
        sb.AppendLine("Fretboard position:");
        
        // Add fret numbers
        sb.Append("     ");
        for (int j = 0; j < endFret - startFret + 1; j++)
        {
            sb.Append($"{startFret + j,2} ");
        }
        sb.AppendLine();
        
        // Add separator
        sb.Append("    ");
        for (int j = 0; j < endFret - startFret + 1; j++)
        {
            sb.Append("---");
        }
        sb.AppendLine();
        
        // Add strings
        for (int i = 0; i < 6; i++)
        {
            sb.Append($"E{6-i}: ");
            for (int j = 0; j < endFret - startFret + 1; j++)
            {
                sb.Append($" {fretboard[i, j]} ");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
