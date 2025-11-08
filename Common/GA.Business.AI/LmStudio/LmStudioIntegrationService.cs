namespace GA.Business.AI.LmStudio;

using MongoDB.Bson;
using GA.Business.Intelligence.SemanticIndexing;

/// <summary>
/// Service for integrating with LM Studio using MongoDB and embeddings
/// </summary>
public class LmStudioIntegrationService(
    MongoDbService mongoDbService,
    SemanticSearchService.IEmbeddingService embeddingService,
    ILogger<LmStudioIntegrationService> logger)
{
    private readonly MongoDbService _mongoDbService = mongoDbService;

    /// <summary>
    /// Retrieves context for a query using vector search
    /// </summary>
    /// <param name="query">The user's query</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>Formatted context string for LM Studio</returns>
    public async Task<string> RetrieveContextAsync(string query, int limit = 5)
    {
        try
        {
            // Generate embedding for the query
            var embedding = await embeddingService.GenerateEmbeddingAsync(query);

            // Retrieve relevant documents from MongoDB using vector search
            // var results = await _mongoDbService.SearchVectorAsync("musical_objects", "vector_index", embedding, limit);

            // Format the results as context
            //return FormatResultsAsContext(results);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving context for query: {Query}", query);
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

            var type = result["type"].AsString;

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
            if (element.Name is "type" or "_id" or "embedding")
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
    public string GenerateFretboardAscii(BsonDocument position, int startFret = 0, int endFret = 5)
    {
        // Create a grid representing a section of the fretboard
        var fretboard = new string[6, endFret - startFret + 1];

        // Initialize with empty positions
        for (var i = 0; i < 6; i++)
            for (var j = 0; j < endFret - startFret + 1; j++)
                fretboard[i, j] = "-";

        // Mark the positions from the document
        if (position.Contains("positions") && position["positions"].IsBsonArray)
        {
            foreach (var pos in position["positions"].AsBsonArray)
            {
                if (pos.IsBsonDocument && pos.AsBsonDocument.Contains("string") && pos.AsBsonDocument.Contains("fret"))
                {
                    var stringIndex = pos.AsBsonDocument["string"].AsInt32 - 1; // 0-based index
                    var fretIndex = pos.AsBsonDocument["fret"].AsInt32 - startFret;

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
        for (var j = 0; j < endFret - startFret + 1; j++)
        {
            sb.Append($"{startFret + j,2} ");
        }
        sb.AppendLine();

        // Add separator
        sb.Append("    ");
        for (var j = 0; j < endFret - startFret + 1; j++)
        {
            sb.Append("---");
        }
        sb.AppendLine();

        // Add strings
        for (var i = 0; i < 6; i++)
        {
            sb.Append($"E{6-i}: ");
            for (var j = 0; j < endFret - startFret + 1; j++)
            {
                sb.Append($" {fretboard[i, j]} ");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
