namespace GA.Business.Core.Tests.Design;

using System.Runtime.CompilerServices;
using Domain.Core.Theory.Atonal;
using GA.Infrastructure.Documentation;

[TestFixture]
public class SchemaDocumentationTests
{
    [Test]
    public void GenerateDomainSchemaDocument()
    {
        var generator = new SchemaDocumentationGenerator();
        var markdown = generator.GenerateMarkdown(typeof(PitchClassSet).Assembly);
        var outputPath = Path.Combine(RepositoryRoot(), "docs", "DOMAIN_SCHEMA.md");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, markdown);
        TestContext.WriteLine($"Generated schema doc at: {Path.GetFullPath(outputPath)}");
    }

    private static string RepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)
            ?? throw new InvalidOperationException("Source file path is unavailable."));

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AllProjects.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Repository root could not be located from {sourceFile}.");
    }
}
