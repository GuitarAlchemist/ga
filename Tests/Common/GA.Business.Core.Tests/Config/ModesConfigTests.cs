namespace GA.Business.Core.Tests.Config;

[TestFixture]
public class ModesConfigTests
{
    [Test]
    public void GetAllModes_ReturnsNonEmptyCollection()
    {
        // This test is ignored because it requires the modes.yaml configuration file
        // which is not available in the test environment
        var modes = ModesConfigCache.Instance.GetAllModes();
        Assert.That(modes, Is.Not.Null);
        Assert.That(modes, Is.Not.Empty);
    }
}
