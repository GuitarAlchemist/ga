namespace GA.Business.Core.Orchestration.Services;

using GaClosureRegistry = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Startup check that asserts the F# closure registry is populated with the
/// canary closures the chatbot's Path B skills route to via ga_dsl_eval.
/// Logs an error (does not throw) when a canary is missing — orchestration
/// runs even with a degraded registry, so the surface stays up while the
/// signal is loud enough to catch in observability.
/// </summary>
/// <remarks>
/// Diagnosed 2026-05-07: F# module do-bindings are lazy, so without an
/// explicit <c>GaClosureBootstrap.init()</c> call (now in
/// <c>AddChatbotOrchestration</c>) the registry is empty at request time and
/// the LLM responses degrade to "closure not exposed" apologies. This check
/// turns the next regression from "user-visible apology" into "log line at
/// startup the operator can grep for".
/// </remarks>
internal sealed class ClosureRegistryStartupCheck(ILogger<ClosureRegistryStartupCheck> logger) : IHostedService
{
    private static readonly string[] CanaryClosures =
    [
        "domain.transposeChord",
        "domain.commonTones",
        "domain.diatonicChords",
    ];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (var name in CanaryClosures)
        {
            var found = GaClosureRegistry.Global.TryGet(name);
            if (Microsoft.FSharp.Core.FSharpOption<GA.Business.DSL.Closures.GaClosureRegistry.GaClosure>.get_IsNone(found))
            {
                missing.Add(name);
            }
        }

        if (missing.Count == 0)
        {
            logger.LogInformation(
                "[ClosureRegistry] OK — all {Count} canary closures resolved: {Closures}",
                CanaryClosures.Length, string.Join(", ", CanaryClosures));
        }
        else
        {
            logger.LogError(
                "[ClosureRegistry] MISSING {MissingCount}/{TotalCount} canary closures: {Missing}. " +
                "Path B SKILL.md skills (transpose / common-tones / diatonic-chords) will return apology " +
                "responses. Verify GaClosureBootstrap.init() ran during AddChatbotOrchestration().",
                missing.Count, CanaryClosures.Length, string.Join(", ", missing));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
