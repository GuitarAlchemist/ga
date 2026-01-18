namespace GA.AI.Service.Controllers;

using GA.Business.Core.AI.Benchmarks;
using GA.AI.Service.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AIAnalysisController(IOllamaService ollamaService) : ControllerBase
{
    [HttpPost("analyze-benchmark")]
    public async Task<ActionResult<AIAnalysisResponse>> AnalyzeBenchmark([FromBody] BenchmarkAnalysisRequest request)
    {
        var analysis = await ollamaService.AnalyzeBenchmarkAsync(request.Name, request.Data);
        return Ok(new AIAnalysisResponse { Analysis = analysis });
    }

    [HttpPost("explain-voicing")]
    public async Task<ActionResult<AIAnalysisResponse>> ExplainVoicing([FromBody] VoicingExplanationRequest request)
    {
        var analysis = await ollamaService.ExplainVoicingAsync(request.Name, request.Data);
        return Ok(new AIAnalysisResponse { Analysis = analysis });
    }
}

public class BenchmarkAnalysisRequest
{
    public string Name { get; set; } = string.Empty;
    public object Data { get; set; } = new();
}

public class VoicingExplanationRequest
{
    public string Name { get; set; } = string.Empty;
    public object Data { get; set; } = new();
}

public class AIAnalysisResponse
{
    public string Analysis { get; set; } = string.Empty;
}
