namespace GA.Business.Core.Tests.Examples;

// using GA.Business.Core.Examples; // Namespace does not exist

[TestFixture]
public class SimpleBspDemoTests
{
    [Test]
    public async Task SimpleBSPDemo_RunDemo_ShouldCompleteWithoutErrors()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await SimpleBspDemo.RunDemo());
    }

    [Test]
    public async Task SimpleBSPDemo_DemoBasicProgression_ShouldCompleteWithoutErrors()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await SimpleBspDemo.DemoBasicProgression());
    }
}
