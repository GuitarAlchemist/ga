namespace GA.Business.Core.Orchestration.Services;

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
    public async Task<QueryFilters?> ExtractFiltersAsync(string userQuery, CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = prompter.BuildSystemPrompt();
            var fullPrompt   = $"{systemPrompt}\n\nUSER QUERY: {userQuery}";

            return await ollamaClient.GenerateStructuredAsync<QueryFilters>(fullPrompt, temperature: 0.1f, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[QueryUnderstanding] Failed to extract filters");
            return null;
        }
    }
}
