namespace GA.Business.ML.Tests.Unit;

using System.Text.RegularExpressions;

/// <summary>
/// Static-analysis pin, like <see cref="ProductionOrchestratorHookPlumbingTests"/>: both entry
/// points of <c>ProductionOrchestrator</c> delegate to one routing cascade, whose stage order is
/// pinned here. The two paths used to be hand-written copies that drifted (the streaming path
/// skipped the skill hooks and ran the voicing guard after semantic routing). Runtime parity of
/// the two paths is covered by <c>OrchestratorCascadeParityTests</c> in GaChatbot.Api.Tests.
/// </summary>
[TestFixture]
public class ProductionOrchestratorStreamingParityTests
{
    private const string SourceRelativePath =
        "Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs";

    private const string Cascade = "RunCascadeAsync";

    [TestCase("AnswerAsync")]
    [TestCase("AnswerStreamingAsync")]
    public void EntryPoint_DelegatesToTheSingleCascade(string method)
    {
        var body = MethodBody(LoadOrchestratorSource(), method);

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain($"{Cascade}("), $"{method} must run the shared cascade");
            Assert.That(body, Does.Not.Contain("RouteAsync("), $"{method} must not route on its own");
        });
    }

    // Voicing guard and algebra fast path are high-precision deterministic checks that the
    // semantic router can mis-rank ("Drop 2 voicings of Cmaj7" lost to the modes intent), so
    // they run first. ga#745: a request naming only invalid chords scores below every intent;
    // it is declined after semantic dispatch and before the LLM agent router, whose answer
    // (or the chat fallback behind it) invented theory about "Hm Q7".
    [Test]
    public void Cascade_RunsItsStagesInOrder()
    {
        var body = MethodBody(LoadOrchestratorSource(), Cascade);
        string[] stages =
        [
            "OnRequestReceived(",
            "TrySelectDeterministicAgent(",
            "TryAnswerWithAlgebraFastPathAsync(",
            "TryDispatchViaIntentAsync(",
            "DeclineInvalidChordNames(",
            "AnswerWithAgentRouterAsync(",
        ];

        var positions = stages.Select(stage => body.IndexOf(stage, StringComparison.Ordinal)).ToArray();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < stages.Length; i++)
            {
                Assert.That(positions[i], Is.GreaterThanOrEqualTo(0), $"the cascade does not call {stages[i]}");
                if (i > 0)
                    Assert.That(positions[i], Is.GreaterThan(positions[i - 1]), $"{stages[i]} must run after {stages[i - 1]}");
            }
        });
    }

    private static string MethodBody(string source, string method)
    {
        // The declaration may carry modifiers (AnswerAsync is virtual) and need not be async.
        var start = Regex.Match(source, $@"(?:public|private)\s+(?:virtual\s+|override\s+|sealed\s+)*(?:async\s+)?Task<ChatResponse>\s+{method}\s*\(");
        Assert.That(start.Success, Is.True, $"{method} not found in ProductionOrchestrator.cs");
        var next = Regex.Match(source[(start.Index + start.Length)..], @"\n    (public|private|internal|protected)\s");
        return next.Success ? source.Substring(start.Index, start.Length + next.Index) : source[start.Index..];
    }

    private static string LoadOrchestratorSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.That(dir, Is.Not.Null, "Could not find repo root (AllProjects.slnx)");
        return File.ReadAllText(Path.Combine(dir!.FullName, SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
