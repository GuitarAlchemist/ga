namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;

[TestFixture]
public sealed class GroundedPromptBuilderNotationTests
{
    [Test]
    public void Build_IncludesVexTabOutputContract()
    {
        var builder = new GroundedPromptBuilder();
        var candidate = new CandidateVoicing(
            "c-major-open",
            "C major open",
            "x-3-2-0-1-0",
            0.98,
            new VoicingExplanationDto("Open C shape.", [], [], [], null),
            "Open C shape.");

        var prompt = builder.Build("Show me C major", [candidate]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("fenced `vextab` block"));
            Assert.That(prompt, Does.Contain("string 6 = low E"));
            Assert.That(prompt, Does.Contain("Fingering: x-3-2-0-1-0"));
        });
    }

    // Out-of-scope gate (2026-05-31): this RAG narrator prompt is the production
    // responder for queries that fall through routing (Mode=full, fallback off),
    // so a non-music query reaches it with an empty manifest. The SCOPE guardrail
    // must be present on BOTH the empty- and populated-manifest paths and must
    // permit in-scope music questions (so it declines "weather", not "Dorian").
    [TestCase("what's the weather in Paris")]      // out of scope
    [TestCase("voicings for Cmaj7")]               // in scope, will have a manifest
    public void Build_AlwaysIncludesScopeGuardrail(string query)
    {
        var builder = new GroundedPromptBuilder();
        // Empty candidate list is the OOS path; the guardrail must be present
        // regardless of whether the manifest has data.
        var prompt = builder.Build(query, []);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("SCOPE:"),
                "the out-of-scope guardrail must be present in every grounded prompt");
            Assert.That(prompt, Does.Contain("politely decline"),
                "the guardrail must instruct a graceful decline for clearly-unrelated queries");
            Assert.That(prompt, Does.Contain("proceed normally"),
                "the guardrail must explicitly allow in-scope music questions through (false-decline guard)");
        });
    }
}
