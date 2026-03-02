namespace GA.Business.Core.Tests.Session;

using Core.Session;
using GA.Business.Core.Context;

/// <summary>
///     Unit tests for MusicalSessionContext domain model
/// </summary>
[TestFixture]
public class MusicalSessionContextTests
{
    [Test]
    public void Default_CreatesValidContext()
    {
        // Act
        var context = MusicalSessionContext.Default();

        // Assert
        Assert.That(context.Tuning, Is.Not.Null);
        Assert.That(context.NotationStyle, Is.EqualTo(NotationStyle.Auto));
        Assert.That(context.EnharmonicPreference, Is.EqualTo(EnharmonicPreference.Context));
        Assert.That(context.CurrentKey, Is.Null);
        Assert.That(context.SkillLevel, Is.Null);
    }

    [Test]
    public void WithSkillLevel_UpdatesSkillLevelCorrectly()
    {
        // Arrange
        var context = MusicalSessionContext.Default();

        // Act
        var updated = context.WithSkillLevel(SkillLevel.Advanced);

        // Assert
        Assert.That(updated.SkillLevel, Is.EqualTo(SkillLevel.Advanced));
        Assert.That(context.SkillLevel, Is.Null); // Original unchanged
        Assert.That(updated, Is.Not.SameAs(context)); // Immutable
    }

    [Test]
    public void WithGenre_UpdatesGenreCorrectly()
    {
        // Arrange
        var context = MusicalSessionContext.Default();

        // Act
        var updated = context.WithGenre(MusicalGenre.Jazz);

        // Assert
        Assert.That(updated.CurrentGenre, Is.EqualTo(MusicalGenre.Jazz));
    }

    [Test]
    public void WithRange_UpdatesFretboardRangeCorrectly()
    {
        // Arrange
        var context = MusicalSessionContext.Default();
        var range = FretboardRange.OpenPosition();

        // Act
        var updated = context.WithRange(range);

        // Assert
        Assert.That(updated.ActiveRange, Is.EqualTo(range));
        Assert.That(updated.ActiveRange!.MinFret, Is.EqualTo(0));
        Assert.That(updated.ActiveRange.MaxFret, Is.EqualTo(3));
    }

    [Test]
    public void FluentUpdates_ChainCorrectly()
    {
        // Arrange
        var context = MusicalSessionContext.Default();

        // Act
        var updated = context
            .WithSkillLevel(SkillLevel.Intermediate)
            .WithGenre(MusicalGenre.Rock)
            .WithRange(FretboardRange.FullNeck());

        // Assert
        Assert.That(updated.SkillLevel, Is.EqualTo(SkillLevel.Intermediate));
        Assert.That(updated.CurrentGenre, Is.EqualTo(MusicalGenre.Rock));
        Assert.That(updated.ActiveRange, Is.Not.Null);
    }

    [Test]
    public void WithMasteredTechnique_AddsToSet()
    {
        // Arrange
        var context = MusicalSessionContext.Default();

        // Act
        var updated = context
            .WithMasteredTechnique("Bending")
            .WithMasteredTechnique("Vibrato");

        // Assert
        Assert.That(updated.MasteredTechniques.Count, Is.EqualTo(2));
        Assert.That(updated.MasteredTechniques, Does.Contain("Bending"));
        Assert.That(updated.MasteredTechniques, Does.Contain("Vibrato"));
    }
}

/// <summary>
///     Unit tests for FretboardRange value object
/// </summary>
[TestFixture]
public class FretboardRangeTests
{
    [Test]
    public void Create_WithValidParameters_CreatesRange()
    {
        // Act
        var range = FretboardRange.Create(0, 12, 6);

        // Assert
        Assert.That(range.MinFret, Is.EqualTo(0));
        Assert.That(range.MaxFret, Is.EqualTo(12));
        Assert.That(range.AvailableStrings.Count, Is.EqualTo(6));
    }

    [Test]
    public void OpenPosition_CreatesCorrectRange()
    {
        // Act
        var range = FretboardRange.OpenPosition();

        // Assert
        Assert.That(range.MinFret, Is.EqualTo(0));
        Assert.That(range.MaxFret, Is.EqualTo(3));
        Assert.That(range.Span, Is.EqualTo(4));
    }

    [Test]
    public void FullNeck_CreatesCorrectRange()
    {
        // Act
        var range = FretboardRange.FullNeck(stringCount: 6, fretCount: 24);

        // Assert
        Assert.That(range.MinFret, Is.EqualTo(0));
        Assert.That(range.MaxFret, Is.EqualTo(24));
        Assert.That(range.Span, Is.EqualTo(25));
        Assert.That(range.AvailableStrings.Count, Is.EqualTo(6));
    }

    [Test]
    public void Contains_ChecksPositionCorrectly()
    {
        // Arrange
        var range = FretboardRange.Create(5, 12, 6);

        // Act & Assert
        Assert.That(range.Contains(7, 3), Is.True); // Within range
        Assert.That(range.Contains(3, 3), Is.False); // Fret too low
        Assert.That(range.Contains(15, 3), Is.False); // Fret too high
        Assert.That(range.Contains(7, 7), Is.False); // String not available
    }
}

/// <summary>
///     Unit tests for InMemorySessionContextProvider
/// </summary>
[TestFixture]
public class InMemorySessionContextProviderTests
{
    [Test]
    public void GetContext_ReturnsDefaultContext_WhenNotSet()
    {
        // Arrange
        var provider = new InMemorySessionContextProvider();

        // Act
        var context = provider.GetContext();

        // Assert
        Assert.That(context, Is.Not.Null);
        Assert.That(context.Tuning, Is.Not.Null);
    }

    [Test]
    public void UpdateContext_UpdatesContextAtomically()
    {
        // Arrange
        var provider = new InMemorySessionContextProvider();

        // Act
        provider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Expert));
        var context = provider.GetContext();

        // Assert
        Assert.That(context.SkillLevel, Is.EqualTo(SkillLevel.Expert));
    }

    [Test]
    public void SetContext_ReplacesContext()
    {
        // Arrange
        var provider = new InMemorySessionContextProvider();
        var newContext = MusicalSessionContext.Default()
            .WithGenre(MusicalGenre.Metal);

        // Act
        provider.SetContext(newContext);
        var context = provider.GetContext();

        // Assert
        Assert.That(context.CurrentGenre, Is.EqualTo(MusicalGenre.Metal));
    }

    [Test]
    public void ResetContext_RestoresToDefault()
    {
        // Arrange
        var provider = new InMemorySessionContextProvider();
        provider.UpdateContext(ctx => ctx
            .WithSkillLevel(SkillLevel.Expert)
            .WithGenre(MusicalGenre.Jazz));

        // Act
        provider.ResetContext();
        var context = provider.GetContext();

        // Assert
        Assert.That(context.SkillLevel, Is.Null);
        Assert.That(context.CurrentGenre, Is.Null);
    }

    [Test]
    public void ContextChanged_RaisesEvent_WhenContextUpdates()
    {
        // Arrange
        var provider = new InMemorySessionContextProvider();
        MusicalSessionContext? capturedContext = null;
        provider.ContextChanged += (_, ctx) => capturedContext = ctx;

        // Act
        provider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Beginner));

        // Assert
        Assert.That(capturedContext, Is.Not.Null);
        Assert.That(capturedContext!.SkillLevel, Is.EqualTo(SkillLevel.Beginner));
    }

    [Test]
    public void ConcurrentUpdates_AreThreadSafe()
    {
        // Arrange
        var provider = new InMemorySessionContextProvider();
        var tasks = new List<Task>();

        // Act - simulate concurrent updates
        for (var i = 0; i < 100; i++)
        {
            var skillLevel = (SkillLevel)(i % 4);
            tasks.Add(Task.Run(() => { provider.UpdateContext(ctx => ctx.WithSkillLevel(skillLevel)); }));
        }

        Task.WaitAll([.. tasks]);

        // Assert - should not throw, context should be valid
        var context = provider.GetContext();
        Assert.That(context, Is.Not.Null);
        Assert.That(Enum.IsDefined(typeof(SkillLevel), context.SkillLevel!.Value), Is.True);
    }
}
