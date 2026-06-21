// Assembly-wide test setup (global namespace → applies to every fixture in
// GaChatbot.Api.Tests). Suppresses routing telemetry for the whole test run.
//
// Why: the in-process orchestrator host (TestWebApplicationFactory) drives
// SemanticIntentRouter.RouteAsync for every corpus prompt (PromptCorpusTests),
// which appends a RoutingTelemetryRecord (PR #383). Without this gate those
// fixed-corpus prompts would land in the real repo state/telemetry/routing/
// stream and contaminate the held-out eval set the sink exists to create
// (Codex P1 on #383). GA_ROUTING_NO_TELEMETRY=1 makes RoutingTelemetryLog.Append
// a no-op (it re-reads the env var on every call).

using NUnit.Framework;

[SetUpFixture]
public sealed class TelemetrySuppressionSetUpFixture
{
    private string? _prior;

    [OneTimeSetUp]
    public void DisableRoutingTelemetry()
    {
        _prior = Environment.GetEnvironmentVariable("GA_ROUTING_NO_TELEMETRY");
        Environment.SetEnvironmentVariable("GA_ROUTING_NO_TELEMETRY", "1");
    }

    [OneTimeTearDown]
    public void RestoreRoutingTelemetry() => Environment.SetEnvironmentVariable("GA_ROUTING_NO_TELEMETRY", _prior);
}
