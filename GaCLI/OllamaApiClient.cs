namespace GaCLI;

using DTOs;

public class OllamaApiClient(HttpClient httpClient)
{
    public static async Task<ImmutableList<string>> GetAvailableModelsAsync(Uri ollamaBaseUri)
    {
        var httpClient = new HttpClient { BaseAddress = ollamaBaseUri };
        var apiClient = new OllamaApiClient(httpClient);
        return await apiClient.GetAvailableModelsInternalAsync();
    }

    private async Task<ImmutableList<string>> GetAvailableModelsInternalAsync()
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
            if (response == null)
            {
                Console.WriteLine("API response was null.");
                return ImmutableList<string>.Empty;
            }

            return response.Models.Select(m => m.Name).ToImmutableList();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Request Error: {ex.Message}");
            Console.WriteLine($"Status Code: {ex.StatusCode}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw;
        }
    }
}