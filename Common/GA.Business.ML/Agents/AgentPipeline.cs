namespace GA.Business.ML.Agents;

/// <summary>
/// Fluent builder for composable agent processing pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Inspired by F# computation expressions ("agent { draft; critique; refine }"),
/// this provides the same composable, step-by-step pipeline semantics in C#.
/// Each step receives the previous step's output and transforms it.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var response = await AgentPipeline
///     .For(request)
///     .Via(theoryAgent)           // primary agent processes the request
///     .Critique(criticAgent)       // critic evaluates and annotates
///     .WithFeedback(feedback, routedAgentId)   // apply routing feedback
///     .RunAsync(cancellationToken);
/// </code>
/// </para>
/// </remarks>
public static class AgentPipeline
{
    /// <summary>Starts a new pipeline for the given request.</summary>
    public static AgentPipelineBuilder For(AgentRequest request) => new(request);
}

/// <summary>
/// Fluent pipeline builder — chain steps, execute lazily with <see cref="RunAsync"/>.
/// </summary>
public sealed class AgentPipelineBuilder(AgentRequest request)
{
    private readonly List<Func<AgentContext, CancellationToken, Task<AgentContext>>> _steps = [];

    /// <summary>
    /// Sends the request to <paramref name="agent"/> and stores its response as the current result.
    /// </summary>
    public AgentPipelineBuilder Via(GuitarAlchemistAgentBase agent)
    {
        _steps.Add(async (ctx, ct) =>
        {
            var response = await agent.ProcessAsync(ctx.Request, ct);
            return ctx with { Response = response, RoutedAgentId = agent.AgentId };
        });
        return this;
    }

    /// <summary>
    /// Passes the current response to <paramref name="critic"/> for evaluation.
    /// Merges evidence and raises confidence when the critique agrees.
    /// </summary>
    public AgentPipelineBuilder Critique(GuitarAlchemistAgentBase critic)
    {
        _steps.Add(async (ctx, ct) =>
        {
            if (ctx.Response == null)
                return ctx;

            var critiqueRequest = ctx.Request with
            {
                Query = $"""
                    Evaluate this music theory response for accuracy and completeness.

                    ORIGINAL QUESTION: {ctx.Request.Query}
                    RESPONSE TO EVALUATE: {ctx.Response.Result}
                    """,
                Context = "Provide a structured critique with quality score and any corrections."
            };

            var critique = await critic.ProcessAsync(critiqueRequest, ct);

            // Merge: combine evidence, adjust confidence upward only if critique agrees
            var mergedEvidence = ctx.Response.Evidence.Concat(
                critique.Evidence.Select(e => $"[Critique] {e}")).ToList();

            var adjustedConfidence = critique.Confidence > 0.7f
                ? Math.Min(ctx.Response.Confidence * 1.05f, 1.0f)   // small boost when critique passes
                : ctx.Response.Confidence * 0.9f;                     // small penalty when critique flags issues

            return ctx with
            {
                Response = ctx.Response with
                {
                    Confidence = adjustedConfidence,
                    Evidence = mergedEvidence
                },
                CritiqueResponse = critique
            };
        });
        return this;
    }

    /// <summary>
    /// Records a routing feedback correction if the pipeline was routed to the wrong agent.
    /// Call this after <see cref="Via"/> when the caller knows the correct agent.
    /// </summary>
    public AgentPipelineBuilder WithFeedback(IRoutingFeedback feedback, string? correctAgentId)
    {
        _steps.Add(async (ctx, ct) =>
        {
            if (ctx.RoutedAgentId != null
                && correctAgentId != null
                && ctx.RoutedAgentId != correctAgentId)
            {
                await feedback.RecordCorrectionAsync(
                    ctx.Request.Query,
                    ctx.RoutedAgentId,
                    correctAgentId,
                    ct);
            }
            return ctx;
        });
        return this;
    }

    /// <summary>
    /// Post-processes the current response by scanning <see cref="AgentResponse.Result"/> for chord
    /// name mentions and appending a markdown "Chord Diagrams" section with
    /// <c>[diagram: ChordName]</c> markers the React frontend can render as chord diagram components.
    /// </summary>
    /// <remarks>
    /// No-ops silently when no response is present or no chords are detected.
    /// </remarks>
    public AgentPipelineBuilder WithChordDiagrams()
    {
        _steps.Add((ctx, _) =>
        {
            if (ctx.Response is null)
                return Task.FromResult(ctx);

            var chords = ChordDiagramRenderer.ExtractChordNames(ctx.Response.Result);
            if (chords.Count == 0)
                return Task.FromResult(ctx);

            var updatedResult = ChordDiagramRenderer.AppendDiagrams(ctx.Response.Result, chords);
            return Task.FromResult(ctx with { Response = ctx.Response with { Result = updatedResult } });
        });
        return this;
    }

    /// <summary>
    /// Adds a custom transformation step.
    /// </summary>
    public AgentPipelineBuilder Then(Func<AgentContext, CancellationToken, Task<AgentContext>> step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Executes all pipeline steps in sequence and returns the final response.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="Via"/> step produced a response.
    /// </exception>
    public async Task<AgentResponse> RunAsync(CancellationToken cancellationToken = default)
    {
        var context = new AgentContext(Request: request);

        foreach (var step in _steps)
        {
            context = await step(context, cancellationToken);
        }

        return context.Response
            ?? throw new InvalidOperationException(
                "Pipeline produced no response. Add at least one Via(agent) step.");
    }
}

/// <summary>
/// Mutable context threaded through each pipeline step.
/// </summary>
public record AgentContext(
    AgentRequest Request,
    AgentResponse? Response = null,
    AgentResponse? CritiqueResponse = null,
    string? RoutedAgentId = null);
