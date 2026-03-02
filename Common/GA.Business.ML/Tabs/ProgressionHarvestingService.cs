namespace GA.Business.ML.Tabs;

using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Repositories;
using GA.Domain.Core.Theory.Harmony.Progressions;
using GA.Domain.Core.Theory.Tabs;

/// <summary>
/// Harvests harmonic progressions from the raw Tab Corpus.
/// Part of Phase 6.3.1.
/// </summary>
public class ProgressionHarvestingService(
    ITabCorpusRepository tabRepo,
    IProgressionCorpusRepository progRepo,
    TabAnalysisService tabAnalyzer)
{
    /// <summary>
    /// Processes the tab corpus and extracts labeled progressions.
    /// </summary>
    public async Task HarvestAsync()
    {
        var tabs = await tabRepo.GetAllAsync();
        
        foreach (var tab in tabs)
        {
            try
            {
                // 1. Analyze the ASCII tab to get chord sequence
                var result = await tabAnalyzer.AnalyzeAsync(tab.Content);
                
                if (result.Events.Count < 4) continue; // Skip fragments

                // 2. Determine Style from metadata or tags
                var style = "Unknown";
                if (tab.Metadata.TryGetValue("Style", out var s)) style = s;
                else if (tab.Metadata.TryGetValue("Genre", out var g)) style = g;

                // 3. Create Progression Item
                var now = DateTime.UtcNow;
                var item = new ProgressionCorpusItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = now,
                    UpdatedAt = now,
                    StyleLabel = style,
                    Chords = [.. result.Events.Select(e => new GA.Domain.Core.Instruments.Fretboard.Voicings.Core.ChordVoicingSnapshot {
                        Id = e.Document.Id,
                        ChordName = e.Document.ChordName,
                        MidiNotes = e.Document.MidiNotes,
                        Diagram = e.Document.Diagram,
                        VoicingType = e.Document.VoicingType,
                        Description = e.Document.SearchableText
                    })],
                    Source = tab.SourceId,
                    Metadata = new Dictionary<string, string>(tab.Metadata)
                };

                // 4. Save to repository
                await progRepo.SaveAsync(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error harvesting tab {tab.Id}: {ex.Message}");
            }
        }
    }
}
