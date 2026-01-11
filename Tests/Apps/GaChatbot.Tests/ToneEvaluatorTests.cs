namespace GaChatbot.Tests.Components;

using GaChatbot.Components;
using GA.Testing.Semantic;
using GA.Business.Core.AI.Services.Embeddings;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ToneEvaluatorTests
{
    private Mock<IEmbeddingService> _mockEmbedder = null!;
    private Mock<IJudgeService> _mockJudge = null!;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        _mockEmbedder = new Mock<IEmbeddingService>();
        _mockJudge = new Mock<IJudgeService>();
        
        // Mocking the "Encouraging" Basin
        _mockEmbedder.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken ct) => 
            {
                if (s.Contains("practicing", StringComparison.OrdinalIgnoreCase) || 
                    s.Contains("encouraging", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("stretches", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("great choice", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("mentor", StringComparison.OrdinalIgnoreCase))
                {
                    return new float[] { 1.0f, 0.0f, 0.0f }; // Concept A: Encouraging
                }
                
                if (s.Contains("boring", StringComparison.OrdinalIgnoreCase) || 
                    s.Contains("bored", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("uninspired", StringComparison.OrdinalIgnoreCase))
                {
                    return new float[] { 0.0f, 1.0f, 0.0f }; // Concept B: Bored
                }

                // Neutral/Other
                return new float[] { 0.0f, 0.0f, 1.0f }; 
            });

        // Mocking Level 1 Judge
        _mockJudge.Setup(j => j.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string prompt, string rubric, CancellationToken ct) => 
            {
                var isEncouraging = text.Contains("Keep practicing") || 
                                   text.Contains("progress") || 
                                   text.Contains("great choice") || 
                                   text.Contains("stretches");
                                   
                if (isEncouraging)
                {
                    return new JudgeResult(true, "The mentor sounds helpful and encouraging.", 0.95);
                }
                return new JudgeResult(false, "The tone is too clinical or missing encouragement.", 0.85);
            });

        AssertAi.Configure(_mockEmbedder.Object);
        AssertAi.ConfigureJudge(_mockJudge.Object);
    }

    [Test]
    public void EnhanceResponse_AddsEncouragingTone()
    {
        var evaluator = new ToneEvaluator();
        var raw = "The Cmaj7 chord uses the notes C, E, G, and B.";
        
        var enhanced = evaluator.EnhanceResponse(raw);
        
        // Semantic Assertion: The output must reside in the "Encouraging" basin.
        // This is much more robust than checking for specific string suffixes.
        AssertAi.InBasin(enhanced, "An encouraging and helpful musical mentor.");
    }

    [Test]
    public void EnhanceResponse_DoesNotSoundBored()
    {
        var evaluator = new ToneEvaluator();
        var enhanced = evaluator.EnhanceResponse("Play a G chord.");
        
        // Negative Semantic Assertion: Ensure we haven't drifted into a "Bored" tone.
        AssertAi.NotInBasin(enhanced, "A bored and uninspired tone.", threshold: 0.5);
    }

    [Test]
    public void EnhanceResponse_PassesReasoningRubric()
    {
        var evaluator = new ToneEvaluator();
        var raw = "The E7#9 chord is often called the Hendrix chord.";
        
        var enhanced = evaluator.EnhanceResponse(raw);
        
        // Level 1 Assertion: Using "LLM-as-Judge" (mocked) for qualitative reasoning.
        AssertAi.Judges.PassesRubric(
            enhanced, 
            "The response must be musically correct and maintain an encouraging, non-robotic tone."
        );
    }
}
