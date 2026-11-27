namespace GaApi.Tests.Deploy;

using System.Text.RegularExpressions;

/// <summary>
/// Production embeds with the ONNX models <c>Scripts/deploy-gaapi.ps1</c> downloads, and CI
/// evaluates the chatbot with the models <c>.github/workflows/chatbot-eval.yml</c> downloads.
/// The two must pin the same Hugging Face revisions, or the evaluation measures embeddings
/// production does not use (the router's thresholds are calibrated on one of them).
/// </summary>
[TestFixture]
public class DeployGaApiOnnxPinTests
{
    [TestCase("BAAI/bge-large-en-v1.5", "BGE")]
    [TestCase("nomic-ai/nomic-embed-text-v1.5", "NOMIC")]
    public void DeployScript_PinsTheRevisionTheEvalWorkflowEvaluates(string repo, string workflowPrefix)
    {
        var script = File.ReadAllText(TestPaths.RepositoryPath("Scripts", "deploy-gaapi.ps1"));
        var workflow = File.ReadAllText(TestPaths.RepositoryPath(".github", "workflows", "chatbot-eval.yml"));

        var scriptPin = Regex.Match(script, $@"Repo = '{Regex.Escape(repo)}'; Rev = '([0-9a-f]{{40}})'");
        var workflowRepo = Regex.Match(workflow, $@"{workflowPrefix}_REPO: (\S+)");
        var workflowRev = Regex.Match(workflow, $@"{workflowPrefix}_REV: ([0-9a-f]{{40}})");

        Assert.Multiple(() =>
        {
            Assert.That(scriptPin.Success, Is.True, $"deploy-gaapi.ps1 no longer pins {repo}");
            Assert.That(workflowRepo.Groups[1].Value, Is.EqualTo(repo), $"chatbot-eval.yml {workflowPrefix}_REPO");
            Assert.That(scriptPin.Groups[1].Value, Is.EqualTo(workflowRev.Groups[1].Value),
                $"{repo}: the deploy and the eval workflow pin different revisions");
        });
    }
}
