namespace GA.Business.ML.Tests;

using GA.Business.Config;
using GA.Business.ML.Musical.Enrichment;

[TestFixture]
public class ModalSchemaServiceLocatorTests
{
    [Test]
    public void ResolvesModalEmbeddingFromTheRepositoryRoot()
    {
        // ModalEmbedding.yaml is not copied to the output directory, so it resolves through the
        // working-directory probe when the process runs from the repository root. The service itself
        // still loads 0 modes from it: the YAML moved to an Offset/Modes layout that ModalSchemaService's
        // *ScaleModes lists no longer match (pre-existing drift; the class has no callers).
        var path = ConfigFileLocator.findFileFrom(AppDomain.CurrentDomain.BaseDirectory, RepoRoot(),
            "ModalEmbedding.yaml")?.Value;

        Assert.That(path, Is.EqualTo(Path.Combine(RepoRoot(), "Common", "GA.Business.Config", "ModalEmbedding.yaml")));
    }

    [Test]
    public void LeavesTheSchemaEmpty_WhenTheYamlIsMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ga-missing-{Guid.NewGuid():N}.yaml");

        Assert.That(new ModalSchemaService(missing).TotalModes, Is.Zero);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Common", "GA.Business.Config")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repository root not found");
    }
}
