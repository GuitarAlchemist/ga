namespace GA.Business.ML.Tabs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Tabs;
using Embeddings;

/// <summary>
/// Harvests harmonic progressions from the raw Tab Corpus.
/// Part of Phase 6.3.1.
/// </summary>
public class ProgressionHarvestingService
{
    private readonly ITabCorpusRepository _tabRepo;
    private readonly IProgressionCorpusRepository _progRepo;
    private readonly TabAnalysisService _tabAnalyzer;

    public ProgressionHarvestingService(
        ITabCorpusRepository tabRepo,
        IProgressionCorpusRepository progRepo,
        TabAnalysisService tabAnalyzer)
    {
        _tabRepo = tabRepo;
        _progRepo = progRepo;
        _tabAnalyzer = tabAnalyzer;
    }

    /// <summary>
    /// Processes the tab corpus and extracts labeled progressions.
    /// </summary>
    public async Task HarvestAsync()
    {
        var tabs = await _tabRepo.GetAllAsync();
        
        foreach (var tab in tabs)
        {
            try
            {
                // 1. Analyze the ASCII tab to get chord sequence
                var result = await _tabAnalyzer.AnalyzeAsync(tab.Content);
                
                if (result.Events.Count < 4) continue; // Skip fragments

                // 2. Determine Style from metadata or tags
                string style = "Unknown";
                if (tab.Metadata.TryGetValue("Style", out var s)) style = s;
                else if (tab.Metadata.TryGetValue("Genre", out var g)) style = g;

                // 3. Create Progression Item
                var item = new ProgressionCorpusItem
                {
                    Id = Guid.NewGuid().ToString(),
                    StyleLabel = style,
                    Chords = result.Events.Select(e => e.Document).ToList(),
                    Source = tab.SourceId,
                    Metadata = tab.Metadata
                };

                // 4. Save to repository
                await _progRepo.SaveAsync(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error harvesting tab {tab.Id}: {ex.Message}");
            }
        }
    }
}
