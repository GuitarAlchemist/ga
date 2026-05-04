namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;

public sealed class ProductionChatOrchestratorClient(IServiceProvider serviceProvider) : IProductionChatOrchestratorClient
{
    public Task<ChatResponse> AnswerAsync(
        ChatExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var orchestrator = serviceProvider.GetRequiredService<ProductionOrchestrator>();
        return orchestrator.AnswerAsync(
            new ChatRequest(
                request.Message,
                SessionId: null,
                History: request.History),
            cancellationToken);
    }
}
