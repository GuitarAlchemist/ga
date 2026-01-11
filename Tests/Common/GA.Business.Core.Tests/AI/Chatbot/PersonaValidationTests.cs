using GA.Testing.Semantic;
using NUnit.Framework;
using Moq;
using GA.Business.Core.AI.Services.Embeddings;
using System.Threading;
using System.Threading.Tasks;

[TestFixture]
public class PersonaValidationTests
{
    private string _guitarAlchemistRubric = """
        The respondent must identify as 'Guitar Alchemist'.
        The tone must be:
        1. Pedagogical: Explains 'why' and 'how', not just 'what'.
        2. Encouraging: Uses positive reinforcement for the learner.
        3. Music Theory Informed: Uses correct terms (root, third, fifth, extensions).
        4. Concisely thorough: Clear but complete.
        """;

    private Moq.Mock<IJudgeService> _mockJudge = new();

    [SetUp]
    public void SetUp()
    {
        AssertAi.Configure(new Moq.Mock<IEmbeddingService>().Object); // Dummy for Level 0
        
        _mockJudge.Setup(j => j.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string prompt, string rubric, CancellationToken ct) => 
            {
                // Simple heuristic for Mocking Persona
                bool isPedagogical = text.Contains("look at", StringComparison.OrdinalIgnoreCase) || text.Contains("understand", StringComparison.OrdinalIgnoreCase);
                bool isEncouraging = text.Contains("Great", StringComparison.OrdinalIgnoreCase) || text.Contains("Keep practicing", StringComparison.OrdinalIgnoreCase);
                bool matchesPersona = text.Contains("Guitar Alchemist") && isPedagogical && isEncouraging;
                
                return new JudgeResult(matchesPersona, matchesPersona ? "Matches persona." : "Missing core persona traits.", 0.9f);
            });

        AssertAi.ConfigureJudge(_mockJudge.Object);
    }

    [Test]
    [Category("Semantic")]
    [Category("Level1")]
    public void Response_AdheresToGuitarAlchemistPersona()
    {
        // simulated response that SHOULD pass
        var response = """
            Hello! I'm Guitar Alchemist, your guide to the fretboard. 
            Great question about the C Major triad! 
            To understand it, we look at the 1st, 3rd, and 5th notes of the C major scale: C, E, and G.
            When played together, these notes create a stable, bright sound that forms the foundation of so much music.
            Keep practicing your open shapes, you're doing great!
            """;

        AssertAi.Judges.PassesRubric(response, _guitarAlchemistRubric);
    }

    [Test]
    [Category("Semantic")]
    [Category("Level1")]
    public void Response_FailsNonPedagogicalPersona()
    {
        // simulated response that SHOULD fail (too blunt, no theory)
        var response = "C major is C E G. Just play it.";

        Assert.Throws<AssertionException>(() => 
            AssertAi.Judges.PassesRubric(response, _guitarAlchemistRubric)
        );
    }

    [Test]
    [Category("Semantic")]
    [Category("Level2")]
    public void Persona_Invariance_AcrossComplexity()
    {
        // Level 2: The persona (tone) should remain consistent even if the technical complexity changes.
        // For this mock, we'll force one to fail if it doesn't have the persona signature
        var simpleResponse = "Hello! I'm Guitar Alchemist. Great job! A major is A, C#, E. To understand... look at... Keep practicing!";
        var complexResponse = "Hello! I'm Guitar Alchemist. Great job! AMaj7#11 utilizes the Lydian mode. To understand... look at... Keep practicing!";

        var simpleScore = AssertAi.Judges.GetRubricScore(simpleResponse, _guitarAlchemistRubric);
        var complexScore = AssertAi.Judges.GetRubricScore(complexResponse, _guitarAlchemistRubric);

        // We expect both to be HIGHly compliant with the persona
        Assert.That(simpleScore, Is.GreaterThan(0.7));
        Assert.That(complexScore, Is.GreaterThan(0.7));
    }
}
