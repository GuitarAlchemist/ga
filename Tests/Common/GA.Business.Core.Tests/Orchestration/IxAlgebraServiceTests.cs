namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class IxAlgebraServiceTests
{
    private IAlgebraPromptClassifier _classifier = null!;

    [SetUp]
    public void Setup() => _classifier = new KeywordAlgebraPromptClassifier();

    [Test]
    public void Classifier_Recognizes_AlgebraPrompt()
    {
        Assert.That(_classifier.IsAlgebraPrompt("What is the prime form of [0,1,4,6]?"), Is.True);
        Assert.That(_classifier.IsAlgebraPrompt("Show me some Cmaj7 voicings"), Is.False);
    }

    [Test]
    public async Task PrimeForm_Query_Falls_Back_To_Internal_GA_Algebra()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["IX:Revision"] = "7b02a56",
            ["IX:Source"] = "ix-compatible",
            ["IX:External:Enabled"] = "false"
        });

        var answer = await service.TryAnswerAsync("What is the prime form of [0,1,4,6]?");

        Assert.That(answer, Is.Not.Null);
        Assert.That(answer!.QueryType, Is.EqualTo("prime-form"));
        Assert.That(answer.Facts["primeForm"], Is.EqualTo("[0,1,4,6]"));
        Assert.That(answer.Grounding.Source, Is.EqualTo("ix-compatible"));
        Assert.That(answer.Grounding.Revision, Is.EqualTo("7b02a56"));
    }

    [Test]
    public async Task ZRelation_Query_Detects_Known_ZPair()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["IX:Revision"] = "7b02a56",
            ["IX:Source"] = "ix-compatible",
            ["IX:External:Enabled"] = "false"
        });

        var answer = await service.TryAnswerAsync("Are 0146 and 0137 z-related?");

        Assert.That(answer, Is.Not.Null);
        Assert.That(answer!.QueryType, Is.EqualTo("z-relation"));
        Assert.That(answer.Facts["zRelated"], Is.EqualTo(bool.TrueString));
        Assert.That(answer.NaturalLanguageAnswer, Does.Contain("share ICV"));
    }

    [Test]
    public async Task ZRelation_Query_Accepts_CommaSeparated_Set_Without_Brackets()
    {
        // Regression guard (2026-06-16): "0,1,4,6" (comma-separated, no
        // brackets) is the most natural way a user types a pitch-class set, but
        // ExtractPitchClassSets used to split it into single rejected digits, so
        // the deterministic engine never ran and the chatbot fell through to the
        // LLM and timed out. This locks the comma/space-separated extraction.
        var service = CreateService(new Dictionary<string, string?>
        {
            ["IX:Revision"] = "7b02a56",
            ["IX:Source"] = "ix-compatible",
            ["IX:External:Enabled"] = "false"
        });

        var answer = await service.TryAnswerAsync(
            "Is the pitch class set 0,1,4,6 Z-related to another set class?");

        Assert.That(answer, Is.Not.Null, "comma-separated set must be extracted and answered deterministically");
        Assert.That(answer!.QueryType, Is.EqualTo("z-relation"));
        Assert.That(answer.Facts["isZRelated"], Is.EqualTo(bool.TrueString));
        // The all-interval tetrachord {0,1,4,6} has a single Z-partner {0,1,3,7}.
        Assert.That(answer.Facts["partner"], Is.EqualTo("[0,1,3,7]"));
        Assert.That(answer.Grounding.Source, Is.EqualTo("ix-compatible"));
    }

    [Test]
    public async Task ForteLabel_ReverseLookup_Returns_SetClass()
    {
        // Feature (2026-06-16): "what is Forte number 4-Z29" is a reverse lookup
        // (label → set class). 4-Z29 is one of the two all-interval tetrachords;
        // its prime form is [0,1,3,7]. Must NOT be mis-parsed as the set {2,9}.
        var service = CreateService(new Dictionary<string, string?>
        {
            ["IX:Revision"] = "7b02a56",
            ["IX:Source"] = "ix-compatible",
            ["IX:External:Enabled"] = "false"
        });

        var answer = await service.TryAnswerAsync("What is Forte number 4-Z29?");

        Assert.That(answer, Is.Not.Null);
        Assert.That(answer!.QueryType, Is.EqualTo("forte-lookup"));
        Assert.That(answer.Facts["primeForm"], Is.EqualTo("[0,1,3,7]"));
        Assert.That(answer.NaturalLanguageAnswer, Does.Contain("[0,1,3,7]"));
    }

    [Test]
    public async Task SetClass_Query_Accepts_SpaceSeparated_Set()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["IX:Revision"] = "7b02a56",
            ["IX:Source"] = "ix-compatible",
            ["IX:External:Enabled"] = "false"
        });

        var answer = await service.TryAnswerAsync("set class summary for 0 2 4 7");

        Assert.That(answer, Is.Not.Null, "space-separated set must be extracted");
        Assert.That(answer!.QueryType, Is.EqualTo("set-class-summary"));
    }

    [Test]
    public async Task External_Process_Response_Takes_Precedence_When_Configured()
    {
        var scriptPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"ix-algebra-{Guid.NewGuid():N}.ps1");
        try
        {
            var pwshPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell",
                "7",
                "pwsh.exe");
            Assume.That(File.Exists(pwshPath), $"Expected PowerShell 7 at {pwshPath}");

            File.WriteAllText(
                scriptPath,
                """
                $inputJson = [Console]::In.ReadToEnd()
                $request = $inputJson | ConvertFrom-Json
                $response = @{
                  naturalLanguageAnswer = "IX external answered: $($request.Query)"
                  queryType = "prime-form"
                  facts = @{
                    input = "[0,1,4,6]"
                    primeForm = "[0,1,4,6]"
                  }
                  source = "ix-external"
                  revision = "7b02a56"
                }
                $response | ConvertTo-Json -Compress
                """);

            var service = CreateService(new Dictionary<string, string?>
            {
                ["IX:Revision"] = "internal-ga",
                ["IX:Source"] = "ix-compatible",
                ["IX:External:Enabled"] = "true",
                ["IX:External:ExecutablePath"] = pwshPath,
                ["IX:External:Arguments:0"] = "-NoProfile",
                ["IX:External:Arguments:1"] = "-File",
                ["IX:External:Arguments:2"] = scriptPath
            });

            var answer = await service.TryAnswerAsync("What is the prime form of [0,1,4,6]?");

            Assert.That(answer, Is.Not.Null);
            Assert.That(answer!.NaturalLanguageAnswer, Does.StartWith("IX external answered:"));
            Assert.That(answer.Grounding.Source, Is.EqualTo("ix-external"));
            Assert.That(answer.Grounding.Revision, Is.EqualTo("7b02a56"));
        }
        finally
        {
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }
        }
    }

    [Test]
    public async Task Missing_External_Process_Falls_Back_To_Internal_GA_Algebra()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["IX:Revision"] = "7b02a56",
            ["IX:Source"] = "ix-compatible",
            ["IX:External:Enabled"] = "true",
            ["IX:External:ExecutablePath"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "missing-ix-algebra.exe")
        });

        var answer = await service.TryAnswerAsync("What is the prime form of [0,1,4,6]?");

        Assert.That(answer, Is.Not.Null);
        Assert.That(answer!.Grounding.Source, Is.EqualTo("ix-compatible"));
        Assert.That(answer.Grounding.Revision, Is.EqualTo("7b02a56"));
    }

    private static IIxAlgebraService CreateService(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new IxAlgebraService(configuration, NullLogger<IxAlgebraService>.Instance);
    }
}
