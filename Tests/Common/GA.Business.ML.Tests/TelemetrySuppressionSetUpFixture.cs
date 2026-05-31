// Assembly-wide test setup (global namespace → applies to every fixture in
// GA.Business.ML.Tests). Suppresses routing telemetry for the whole test run.
//
// Why: SemanticIntentRouter.RouteAsync appends one RoutingTelemetryRecord per
// scored query (PR #383). Non-explicit tests (SemanticIntentRouterTests,
// AgentInfrastructureTests) and the [Explicit] RoutingEvalHarness drive that
// path with synthetic / fixed-corpus prompts — without this gate they write
// those records into the real repo state/telemetry/routing/ stream, which is
// exactly the eval-corpus contamination the telemetry sink exists to avoid
// (Codex P1 on #383). GA_ROUTING_NO_TELEMETRY=1 makes RoutingTelemetryLog.Append
// a no-op for the duration of the test process.
//
// RoutingTelemetryLogTests overrides this per-test (it sets GA_ROUTING_TELEMETRY_DIR
// to a temp dir and re-enables writes) so it can still exercise the sink directly.

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
    public void RestoreRoutingTelemetry()
    {
        Environment.SetEnvironmentVariable("GA_ROUTING_NO_TELEMETRY", _prior);
    }
}
