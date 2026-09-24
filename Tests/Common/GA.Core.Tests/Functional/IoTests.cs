namespace GA.Core.Tests.Functional;

using Core.Functional;

/// <summary>
///     Tests verifying that Io&lt;T&gt;.Retry retries the expected number of times without real delays.
/// </summary>
[TestFixture]
[Category("Functional")]
public class IoTests
{
    [Test]
    public void Retry_SucceedsOnFirstAttempt_DoesNotRetry()
    {
        // Arrange
        var attempts = 0;
        var io = Io.Of(() =>
        {
            attempts++;
            return 42;
        });

        // Act
        var result = io.Retry(3, TimeSpan.Zero).Run();

        // Assert
        Assert.That(result, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public void Retry_SucceedsAfterTransientFailures_RetriesExpectedNumberOfTimes()
    {
        // Arrange
        var attempts = 0;
        var io = Io.Of(() =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new InvalidOperationException("transient failure");
            }

            return attempts;
        });

        // Act
        var result = io.Retry(5, TimeSpan.Zero).Run();

        // Assert
        Assert.That(result, Is.EqualTo(3));
        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public void Retry_ExhaustsAttempts_ThrowsAndStopsAtMaxAttempts()
    {
        // Arrange
        var attempts = 0;
        var io = Io.Of<int>(() =>
        {
            attempts++;
            throw new InvalidOperationException("always fails");
        });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => io.Retry(3, TimeSpan.Zero).Run());
        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public async Task RetryAsync_SucceedsAfterTransientFailures_DoesNotBlockThread()
    {
        // Arrange
        var attempts = 0;
        var io = Io.Of(() =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new InvalidOperationException("transient failure");
            }

            return attempts;
        });

        // Act
        var result = await io.RetryAsync(5, TimeSpan.Zero);

        // Assert
        Assert.That(result, Is.EqualTo(3));
        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public void RetryAsync_ExhaustsAttempts_ThrowsAndStopsAtMaxAttempts()
    {
        // Arrange
        var attempts = 0;
        var io = Io.Of<int>(() =>
        {
            attempts++;
            throw new InvalidOperationException("always fails");
        });

        // Act & Assert
        Assert.That(async () => await io.RetryAsync(3, TimeSpan.Zero), Throws.InstanceOf<InvalidOperationException>());
        Assert.That(attempts, Is.EqualTo(3));
    }
}
