namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

[ApiController]
[Route("api/[controller]")]
public class ChatController(ProductionOrchestrator orchestrator) : ControllerBase
{
    /// <summary>
    /// Processes a natural language query using the Spectral RAG chatbot.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required.");

        var response = await orchestrator.AnswerAsync(request);
        return Ok(response);
    }
}
