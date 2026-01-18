namespace GaChatbot.Tests;

using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using GaChatbot.Services;
using GaChatbot.Models;
using GaChatbot.Abstractions;
using GA.Business.ML.Musical.Explanation;
using GaChatbot.Tests.Mocks;

[TestFixture]
public class GuardrailTests
{
    private MockGroundedNarrator _narrator;
    private GroundedPromptBuilder _promptBuilder;
    private ResponseValidator _validator;

    [SetUp]
    public void Setup()
    {
        _promptBuilder = new GroundedPromptBuilder();
        _validator = new ResponseValidator();
        _narrator = new MockGroundedNarrator(_promptBuilder, _validator);
    }

    [Test]
    public async Task Narrate_WithGroundedResponse_Passes()
    {
        var candidates = new List<CandidateVoicing>
        {
            new CandidateVoicing("1", "C Major", "x32010", 1.0, new VoicingExplanation { Summary = "C Major triad", Tags = new List<string>{"maj"} }, "Summary")
        };

        var response = await _narrator.NarrateAsync("Show me C Major", candidates, simulateHallucination: false);

        Assert.That(response, Does.Not.Contain("[WARNING]"), "Grounded response should not trigger warning");
        Assert.That(response, Contains.Substring("C Major"), "Response should mention the allowed chord");
    }

    [Test]
    public async Task Narrate_WithHallucinatedChord_TriggersWarning()
    {
        var candidates = new List<CandidateVoicing>
        {
            new CandidateVoicing("1", "C Major", "x32010", 1.0, new VoicingExplanation { Summary = "C Major triad", Tags = new List<string>{"maj"} }, "Summary")
        };

        // This will simulate mentioning F13 which is not in candidates
        var response = await _narrator.NarrateAsync("Show me C Major", candidates, simulateHallucination: true);

        Assert.That(response, Contains.Substring("[WARNING: This response mentioned chords not found in the verified database.]"), 
            "Hallucinated chord should trigger validation warning");
    }
}
