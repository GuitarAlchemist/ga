namespace GA.Business.ML.Tabs;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Domain.Repositories;
using GA.Domain.Core.Theory.Tabs;

public class TabCorpusService(ITabCorpusRepository repository, HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task IngestAllConfiguredSourcesAsync()
    {
        // Call F# loader
        var sources = Config.TabSourcesConfig.Load();
        
        foreach (var source in sources)
        {
            Console.WriteLine($"Processing source: {source.Name} ({source.Id})");
            // In a real scenario, this would dispatch to specific strategies (Zip, Git, Scrape)
            // For now, we verify we can read the config and access the repo.
            
            // Create a placeholder entry to verify persistence
            var corpusItem = new TabCorpusItem
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SourceId = source.Id,
                ExternalId = "metadata-check",
                Content = $"Source Placeholder for {source.Url}",
                Format = "Metadata",
                Metadata = new Dictionary<string, string> 
                { 
                    { "Url", source.Url },
                    { "Description", source.Description } 
                }
            };

            await repository.SaveAsync(corpusItem);
            Console.WriteLine($"Saved metadata placeholder for {source.Id}.");
        }
    }

    public async Task<long> GetCorpusSizeAsync() => await repository.CountAsync();
}
