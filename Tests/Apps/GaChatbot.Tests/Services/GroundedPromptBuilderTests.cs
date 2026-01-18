namespace GaChatbot.Tests.Services;

using NUnit.Framework;
using GaChatbot.Services;
using GaChatbot.Models;
using GA.Business.ML.Musical.Explanation;
using System.Collections.Generic;

[TestFixture]
public class GroundedPromptBuilderTests
{
    private GroundedPromptBuilder _builder;

    [SetUp]
    public void Setup()
    {
        _builder = new GroundedPromptBuilder();
    }

    [Test]
    public void Build_WithJazzKeywords_InjectsJazzPersona()
    {
        // Arrange
        var query = "Give me a jazzier version of C major";
        var candidates = new List<CandidateVoicing>();

        // Act
        var result = _builder.Build(query, candidates);

        // Assert
        Assert.That(result, Does.Contain("PERSONA: Act as a Jazz Harmony Professor"));
        Assert.That(result, Does.Contain("extensions (9ths, 13ths)"));
    }

    [Test]
    public void Build_WithMetalKeywords_InjectsMetalPersona()
    {
        // Arrange
        var query = "Is this heavy metal?";
        var candidates = new List<CandidateVoicing>();

        // Act
        var result = _builder.Build(query, candidates);

        // Assert
        Assert.That(result, Does.Contain("PERSONA: Act as a Modern Metal Producer"));
        Assert.That(result, Does.Contain("gain staging"));
    }

    [Test]
    public void Build_WithTheoryKeywords_InjectsTheoryPersona()
    {
        // Arrange
        var query = "Explain why tritone subs work";
        var candidates = new List<CandidateVoicing>();

        // Act
        var result = _builder.Build(query, candidates);

        // Assert
        Assert.That(result, Does.Contain("PERSONA: Act as a Music Theorist"));
        Assert.That(result, Does.Contain("geometric relationships"));
    }

    [Test]
    public void Build_WithNeutralQuery_DoesNotInjectPersona()
    {
        // Arrange
        var query = "Play a C major chord";
        var candidates = new List<CandidateVoicing>();

        // Act
        var result = _builder.Build(query, candidates);

        // Assert
        Assert.That(result, Does.Not.Contain("PERSONA:"));
    }
}
