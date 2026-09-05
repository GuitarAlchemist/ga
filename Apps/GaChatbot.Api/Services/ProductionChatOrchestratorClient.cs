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
        return orchestrator.AnswerAsync(ToOrchestratorRequest(request), cancellationToken);
    }

    /// <summary>
    /// Maps this host's <see cref="ChatExecutionRequest"/> onto the
    /// orchestrator's <see cref="ChatRequest"/>.
    /// </summary>
    /// <remarks>
    /// Session identity MUST survive this hop. <c>ProductionOrchestrator</c>
    /// substitutes a fresh <c>Guid</c> whenever it receives a null SessionId,
    /// so dropping it here doesn't fail loudly — it silently gives every turn
    /// its own memory partition, which reads as "the chatbot has amnesia" and
    /// makes <c>Memory:EnrichOnRetrieve</c> a no-op on this host.
    /// Exposed as a pure function so that invariant is directly testable
    /// without standing up the orchestrator.
    /// </remarks>
    public static ChatRequest ToOrchestratorRequest(ChatExecutionRequest request) =>
        new(
            request.Message,
            SessionId: request.SessionId,
            History: request.History);
}
