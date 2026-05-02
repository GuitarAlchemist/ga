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
}
