namespace GA.AI.Service.Controllers;

using GA.Business.Core.AI.Benchmarks;
using GA.Business.ML.AI.Benchmarks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

#pragma warning disable SKEXP0001
[ApiController]
[Route("api/[controller]")]
public class BenchmarkController(BenchmarkRunner benchmarkRunner) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BenchmarkResult>>> GetAll()
    {
        var results = await benchmarkRunner.RunAllAsync();
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BenchmarkResult>> GetById(string id)
    {
        // Use GetByNameAsync to avoid re-running if cached
        var result = await benchmarkRunner.GetByNameAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("run/{id}")]
    public async Task<ActionResult<BenchmarkResult>> Run(string id)
    {
        var result = await benchmarkRunner.RunByNameAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }
    [HttpPost("report")]
    public ActionResult Report([FromBody] BenchmarkResult result)
    {
        benchmarkRunner.ReportResult(result);
        return Ok();
    }
}
