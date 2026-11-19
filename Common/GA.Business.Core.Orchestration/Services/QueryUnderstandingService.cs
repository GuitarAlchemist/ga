namespace GA.Business.Core.Orchestration.Services;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extracts structured search constraints from a natural language user query via LLM.
/// </summary>
public class QueryUnderstandingService(
    DomainMetadataPrompter prompter,
    OllamaGenerateClient ollamaClient,
    ILogger<QueryUnderstandingService> logger)
{
    // Every filter is a scalar: without the schema, a model may answer "Quality": ["Minor"] and
    // deserialization then drops every filter. Ollama enforces the schema while decoding; the chat
    // mode writes it into the prompt.
    private static readonly JsonNode FiltersSchema =
        JsonSerializerOptions.Default.GetJsonSchemaAsNode(
            typeof(QueryFilters), new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });

    public async Task<QueryFilters?> ExtractFiltersAsync(string userQuery, CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = prompter.BuildSystemPrompt();
            var fullPrompt   = $"{systemPrompt}\n\nUSER QUERY: {userQuery}";

            return await ollamaClient.GenerateStructuredAsync<QueryFilters>(fullPrompt, FiltersSchema, temperature: 0.1f, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[QueryUnderstanding] Failed to extract filters");
            return null;
        }
    }
}
