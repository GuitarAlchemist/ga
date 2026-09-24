namespace GA.Business.ML.Tests.Unit;

using System.Text.RegularExpressions;

/// <summary>
/// Static-analysis pin, like <see cref="ProductionOrchestratorHookPlumbingTests"/>: the streaming
/// path must try the deterministic algebra fast path before semantic dispatch, as
/// <c>AnswerAsync</c> does. Without it, algebra prompts on the AG-UI stream needed a live
/// embedding endpoint. A runtime test would need most of the orchestrator's DI graph.
/// </summary>
[TestFixture]
public class ProductionOrchestratorStreamingParityTests
{
    private const string SourceRelativePath =
        "Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs";

    [TestCase("AnswerAsync")]
    [TestCase("AnswerStreamingAsync")]
    public void AlgebraFastPath_RunsBeforeSemanticDispatch(string method)
    {
        var body = MethodBody(LoadOrchestratorSource(), method);

        var fastPath = body.IndexOf("TryAnswerWithAlgebraFastPathAsync(", StringComparison.Ordinal);
        var semantic = new[] { "TryDispatchViaIntentAsync(", "intentRouter.RouteAsync(" }
            .Select(call => body.IndexOf(call, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        Assert.Multiple(() =>
        {
            Assert.That(fastPath, Is.GreaterThanOrEqualTo(0), $"{method} does not try the algebra fast path");
            Assert.That(semantic, Is.GreaterThan(fastPath), $"{method} must try the algebra fast path before semantic dispatch");
        });
    }

    private static string MethodBody(string source, string method)
    {
        // The declaration may carry modifiers between "public" and "async" (AnswerAsync is virtual).
        var start = Regex.Match(source, $@"public\s+(?:virtual\s+|override\s+|sealed\s+)*async\s+Task<ChatResponse>\s+{method}\s*\(");
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
