namespace GaCLI.Plugins;

using System.Net.Http.Json;
using AllProjects.ServiceDefaults;

public sealed record PluginMetadata(
    string Id,
    string Name,
    string Description,
    string Version,
    string Author,
    string[] Tags);

public class PluginMarketplaceService(string apiBaseUrl)
{
    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri(apiBaseUrl) };

    public async Task<List<PluginMetadata>> SearchPluginsAsync(string query, int limit = 10)
    {
        try
        {
            // For the MVP, we simulate a plugin search by calling the GaApi search endpoint
            // and mapping the results to plugin-like metadata.
            // In a real scenario, this would call a dedicated /api/plugins/search endpoint.
            
            var request = new { Query = query, Limit = limit };
            var response = await _httpClient.PostAsJsonAsync("/api/search/hybrid", request);
            
            if (response.IsSuccessStatusCode)
            {
                // This is a dummy mapping just to show connectivity
                var results = await response.Content.ReadFromJsonAsync<List<dynamic>>();
                return results?.Select(r => new PluginMetadata(
                    Id: $"ga.plugin.{Guid.NewGuid().ToString()[..8]}",
                    Name: $"Theory Agent - {query}",
                    Description: $"AI Agent specialized in {query} concepts.",
                    Version: "1.0.0",
                    Author: "Guitar Alchemist",
                    Tags: ["theory", "ai", query.ToLower()]
                )).ToList() ?? [];
            }
        }
        catch
        {
            // Fallback to local mock if API is down
        }

        return (List<PluginMetadata>) [.. new List<PluginMetadata>
        {
            new("ga.theory.major", "Major Scale Pro", "Advanced analysis for major scale modes.", "1.2.0", "GaTeam", ["theory", "major"]),
            new("ga.agent.jazz", "Jazz Master", "Interactive jazz improvisation coach.", "0.9.0", "BlueNotes", ["agent", "jazz"])
        }.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                     p.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
         .Take(limit)];
    }
}
