namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for chord progression template features
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ChordProgressionTests : ChatbotTestBase
{
    [Test]
    public async Task ChordProgression_ShouldShowJazzProgressions()
    {
        // Arrange
        var query = "Show me jazz chord progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("jazz").Or.Contain("ii-v-i"),
            "Response should contain jazz progressions");
    }

    [Test]
    public async Task ChordProgression_ShouldSearchByMood()
    {
        // Arrange
        var query = "Find me a sad chord progression";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("progression").Or.Contain("chord"),
            "Response should contain progression information");
    }

    [Test]
    public async Task ChordProgression_ShouldListAvailableGenres()
    {
        // Arrange
        var query = "What genres do you have progressions for?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("genre").Or.Contain("jazz").Or.Contain("blues"),
            "Response should list genres");
    }

    [Test]
    public async Task ChordProgression_ShouldShowPopProgressions()
    {
        // Arrange
        var query = "Show me popular pop chord progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("i-v-vi-iv").Or.Contain("axis"),
            "Response should contain popular progressions");
    }

    [Test]
    public async Task ChordProgression_ShouldShowBluesProgressions()
    {
        // Arrange
        var query = "What are some blues progressions?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("blues").Or.Contain("12-bar"),
            "Response should contain blues progressions");
    }

    [Test]
    public async Task ChordProgression_ShouldIncludeRomanNumerals()
    {
        // Arrange
        var query = "Show me the I-V-vi-IV progression";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        // Response should contain roman numerals or chord names
        var hasRomanNumerals = response.Contains("I") || response.Contains("V") || response.Contains("vi");
        var hasChordNames = response.ToLower().Contains("chord");

        Assert.That(hasRomanNumerals || hasChordNames, Is.True,
            "Response should include roman numerals or chord information");
    }

    [Test]
    public async Task ChordProgression_ShouldSearchByEmotion()
    {
        // Arrange
        var query = "I want to write an uplifting song, what progression should I use?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("progression").Or.Contain("uplifting").Or.Contain("major"),
            "Response should suggest uplifting progressions");
    }

    [Test]
    public async Task ChordProgression_ShouldProvideMultipleOptions()
    {
        // Arrange
        var query = "Give me some rock chord progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");

        // Check if response contains multiple progressions (look for multiple chord sequences)
        var hasMultipleProgressions = response.Split("→").Length > 2 ||
                                      response.Split("\n").Length > 5;

        Assert.That(hasMultipleProgressions, Is.True,
            "Response should provide multiple progression options");
    }

    [Test]
    public async Task ChordProgression_ShouldExplainUsage()
    {
        // Arrange
        var query = "Tell me about the ii-V-I progression";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("jazz").Or.Contain("progression").Or.Contain("ii-v-i"),
            "Response should explain the progression");
    }

    [Test]
    public async Task ChordProgression_ShouldFormatNicely()
    {
        // Arrange
        var query = "Show me all progression templates";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");

        // Check for formatting elements
        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var hasFormatting = await messageElement.Locator("strong, em, ul, ol").CountAsync() > 0;

        Assert.That(hasFormatting, Is.True, "Response should be well-formatted");
    }

    [Test]
    public async Task ChordProgression_ShouldHandleGenreFilter()
    {
        // Arrange
        var query = "Show me only jazz progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("jazz"),
            "Response should be filtered to jazz");
    }

    [Test]
    public async Task ChordProgression_ShouldProvideContext()
    {
        // Arrange
        var query = "What's a good progression for a ballad?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("ballad").Or.Contain("emotional").Or.Contain("slow"),
            "Response should provide contextual suggestions");
    }

    [Test]
    public async Task ChordProgression_ShouldBeAccessibleToBeginners()
    {
        // Arrange
        var query = "I'm a beginner, what's an easy chord progression to start with?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("beginner").Or.Contain("easy").Or.Contain("simple"),
            "Response should be beginner-friendly");
    }

    [Test]
    public async Task ChordProgression_ShouldLinkToSongs()
    {
        // Arrange
        var query = "What songs use the I-V-vi-IV progression?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        // Response might mention famous songs or provide examples
        var mentionsSongs = response.ToLower().Contains("song") ||
                            response.ToLower().Contains("example") ||
                            response.ToLower().Contains("used in");

        Assert.That(mentionsSongs, Is.True,
            "Response should reference songs or examples");
    }

    [Test]
    public async Task ChordProgression_ShouldPersistInContext()
    {
        // Arrange & Act - First message
        await SendMessageAsync("Show me the I-V-vi-IV progression");
        await WaitForResponseAsync();

        // Second message referencing the first
        await SendMessageAsync("What key would that be in C?");
        var response2 = await WaitForResponseAsync();

        // Assert
        Assert.That(response2, Is.Not.Empty, "Should maintain context");
        Assert.That(response2.ToLower(), Does.Contain("c").Or.Contain("major"),
            "Response should reference C major context");
    }
}
