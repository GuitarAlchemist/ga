namespace GA.Business.Core.Tests.Design;

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
        var outputPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../../docs/DOMAIN_SCHEMA.md");
        File.WriteAllText(outputPath, markdown);
        TestContext.WriteLine($"Generated schema doc at: {Path.GetFullPath(outputPath)}");
    }
}
