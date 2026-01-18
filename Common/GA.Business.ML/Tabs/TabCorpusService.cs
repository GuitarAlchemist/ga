namespace GA.Business.ML.Tabs;

using GA.Business.Core.Tabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class TabCorpusService
{
    private readonly ITabCorpusRepository _repository;
    private readonly HttpClient _httpClient;

    public TabCorpusService(ITabCorpusRepository repository, HttpClient httpClient)
    {
        _repository = repository;
        _httpClient = httpClient;
    }

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

            await _repository.SaveAsync(corpusItem);
            Console.WriteLine($"Saved metadata placeholder for {source.Id}.");
        }
    }
    
    public async Task<long> GetCorpusSizeAsync()
    {
        return await _repository.CountAsync();
    }
}
