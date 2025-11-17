namespace GA.Business.Core.Tests.TestBootstrap;

using System.IO;
using NUnit.Framework;

/// <summary>
/// Ensures configuration-backed services can locate their YAML/JSON inputs during tests
/// by normalizing the current directory to the repository root.
/// </summary>
[SetUpFixture]
public sealed class TestEnvironment
{
    [OneTimeSetUp]
    public void NormalizeWorkingDirectory()
    {
        // Enable test mode for config fallbacks (e.g., InstrumentsConfig)
        Environment.SetEnvironmentVariable("GA_TEST_MODE", "1");

        // Start from the test bin directory and walk up until we find a repo-root marker
        // such as AllProjects.slnx or the Common/GA.Business.Config folder.
        var dir = TestContext.CurrentContext.TestDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "AllProjects.slnx")) ||
                Directory.Exists(Path.Combine(dir, "Common", "GA.Business.Config")))
            {
                Directory.SetCurrentDirectory(dir);
                return;
            }

            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: keep the original working directory
    }
}
