namespace GA.Business.Core.Tests.Config;

using Core.Config;

public class ModesConfigTests
{
    [Test]
    public void Test1()
    {
        var modes = ModesConfigCache.Instance.GetAllModes();
    }
}