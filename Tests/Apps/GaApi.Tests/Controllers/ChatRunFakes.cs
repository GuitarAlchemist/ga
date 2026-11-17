namespace GaApi.Tests.Controllers;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Core.Functional;
using Moq;

/// <summary>
/// Turns a canned intake outcome into the event stream <see cref="IChatIntake.RunAsync"/>
/// yields, for transport tests that mock the intake.
/// </summary>
internal static class ChatRunFakes
{
    public static IAsyncEnumerable<ChatEvent> Events(ChatResponse response) =>
        Events(Result<ChatResponse, ChatIntakeError>.Success(response));

    /// <summary>
    /// A success yields Routing, the whole answer as one TextDelta, then Completed; a
    /// rejection yields a lone Failed.
    /// </summary>
    public static async IAsyncEnumerable<ChatEvent> Events(Result<ChatResponse, ChatIntakeError> outcome)
    {
        await Task.Yield();
        const string runId = "test-run";
        if (outcome.IsFailure)
        {
            var rejection = outcome.GetErrorOrThrow();
            yield return new ChatEvent.Failed(runId, rejection.ToString(), rejection);
            yield break;
        }

        var response = outcome.GetValueOrThrow();
        var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
        yield return new ChatEvent.Routing(runId, routing.AgentId, routing.Confidence, routing.RoutingMethod, response.Grounding);
        if (!string.IsNullOrEmpty(response.NaturalLanguageAnswer))
            yield return new ChatEvent.TextDelta(runId, response.NaturalLanguageAnswer);
        yield return new ChatEvent.Completed(runId, response);
    }

    /// <summary>
    /// Makes the mock's <see cref="IChatIntake.RunAsync"/> stream whatever its
    /// <see cref="IChatIntake.IntakeAsync"/> setups return, so one setup drives every transport.
    /// </summary>
    public static Mock<IChatIntake> RunsLikeIntake(this Mock<IChatIntake> intake)
    {
        intake.Setup(value => value.RunAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ChatIntakeRequest request, CancellationToken cancellationToken) =>
                FromIntake(intake.Object, request, cancellationToken));
        return intake;
    }

    private static async IAsyncEnumerable<ChatEvent> FromIntake(
        IChatIntake intake, ChatIntakeRequest request, CancellationToken cancellationToken)
    {
        var outcome = await intake.IntakeAsync(request, cancellationToken);
        await foreach (var chatEvent in Events(outcome)) yield return chatEvent;
    }
}
