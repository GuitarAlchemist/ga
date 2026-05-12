namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Pins the load-bearing contract that <see cref="OrchestratorSkillIntent"/>
/// forwards <see cref="AgentResponse.Data"/> all the way through the
/// adapter map to <see cref="IntentResult.Data"/>. Without this forward,
/// any skill that emits a structured payload (e.g. RememberThisSkill's
/// <c>MemoryWriteRequest</c>) silently loses it during dispatch, and
/// downstream <c>OnResponseSent</c> hooks never see it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Production bug fix lock-in (2026-05-12).</b> Discovered while
/// designing the live-orchestrator e2e: the route through
/// <see cref="OrchestratorSkillIntent.ExecuteAsync"/> dropped
/// <see cref="AgentResponse.Data"/>, so MemoryWriteHook's
/// <c>ctx.Response?.Data is MemoryWriteRequest</c> guard never matched
/// in real chats. The component-level test in PR #180 bypassed the
/// orchestrator entirely — calling MemoryWriteHook.OnResponseSent
/// directly with a hand-built ChatHookContext — so it did not catch
/// the gap.
/// </para>
/// <para>
/// The fix is three lines: <see cref="IntentResult"/> gained a
/// <c>Data</c> field, <see cref="OrchestratorSkillIntent"/> forwards
/// <c>response.Data</c> into it, and <c>ProductionOrchestrator</c>
/// surfaces <c>intentResult.Data</c> on the synthesized hook
/// AgentResponse. This test pins all three.
/// </para>
/// </remarks>
[TestFixture]
public class OrchestratorSkillIntentDataPropagationTests
{
    [Test]
    public async Task SkillData_FlowsThroughAdapter_IntoIntentResult()
    {
        // Skill emits a structured payload — same shape as RememberThisSkill
        // emitting a MemoryWriteRequest, but with a tiny custom record so
        // the test doesn't depend on the memory subsystem.
        var payload = new TestPayload("preference", "drop-2 voicings");
        var skill   = new EmitsPayloadSkill(payload);
        var adapter = new OrchestratorSkillIntent(skill);

        var result = await adapter.ExecuteAsync("hi");

        Assert.That(result.Data, Is.Not.Null,
            "IntentResult.Data must be populated — without this, OnResponseSent " +
            "hooks pattern-matching on ctx.Response.Data never fire.");
        Assert.That(result.Data, Is.InstanceOf<TestPayload>());
        Assert.That(((TestPayload)result.Data!).Type, Is.EqualTo("preference"));
        Assert.That(((TestPayload)result.Data).Content, Is.EqualTo("drop-2 voicings"));
    }

    [Test]
    public async Task SkillWithoutData_AdapterPreservesNull()
    {
        // Skill emits AgentResponse with no Data (the common case for
        // most music-theory skills). Adapter must propagate null rather
        // than synthesize something.
        var skill   = new EmitsPayloadSkill(payload: null);
        var adapter = new OrchestratorSkillIntent(skill);

        var result = await adapter.ExecuteAsync("hi");

        Assert.That(result.Data, Is.Null,
            "Null Data must remain null through the adapter — synthesizing " +
            "a non-null value here would fire hooks that aren't supposed to fire.");
    }

    // ─── Stubs ──────────────────────────────────────────────────────────

    private sealed record TestPayload(string Type, string Content);

    private sealed class EmitsPayloadSkill(object? payload) : IOrchestratorSkill
    {
        public string Name        => "EmitsPayload";
        public string Description => "Test skill — emits a payload on AgentResponse.Data";
        public IReadOnlyList<string> ExamplePrompts => ["test"];
        public bool CanHandle(string message) => false;

        public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentResponse
            {
                AgentId    = "emits-payload",
                Result     = "ok",
                Confidence = 1.0f,
                Data       = payload,
            });
    }
}
