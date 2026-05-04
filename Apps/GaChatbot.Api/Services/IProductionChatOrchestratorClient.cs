namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Models;

public interface IProductionChatOrchestratorClient
{
    Task<ChatResponse> AnswerAsync(
        ChatExecutionRequest request,
        CancellationToken cancellationToken = default);
}
