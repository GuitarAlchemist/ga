#pragma warning disable SKEXP0001
namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Tabs;

[ApiController]
[Route("api")]
public class TabAnalysisController(TabAnalysisService tabAnalyzer, IVectorIndex vectorIndex, MusicalEmbeddingGenerator embeddingGenerator) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult> AnalyzeTab([FromBody] TabAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Tab))
        {
            return BadRequest(new { error = "Tab content is required." });
        }

        try
        {
            var analysis = await tabAnalyzer.AnalyzeAsync(request.Tab);
            
            var eventsToSave = new List<ChordVoicingRagDocument>();
            foreach (var e in analysis.Events)
            {
                var doc = e.Document;
                if (doc.Embedding == null || doc.Embedding.Length == 0)
                {
                    var embedding = await embeddingGenerator.GenerateEmbeddingAsync(doc);
                    doc = doc with { Embedding = embedding };
                }
                eventsToSave.Add(doc);
            }

            if (eventsToSave.Count != 0)
            {
                await vectorIndex.IndexAsync(eventsToSave);
            }
            
            // Format the output specifically for n8n structured analysis
            var result = new 
            {
                eventsCount = analysis.Events.Count,
                key = analysis.Events.FirstOrDefault()?.Document.PossibleKeys.FirstOrDefault() ?? "Unknown",
                events = analysis.Events.Select(e => new 
                {
                    chordName = e.Document.ChordName,
                    fretSpan = e.Document.MaxFret - e.Document.MinFret,
                    frets = e.Document.SearchableText
                }),
                rawAnalysis = analysis
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class TabAnalysisRequest
{
    public string Tab { get; set; } = string.Empty;
}
