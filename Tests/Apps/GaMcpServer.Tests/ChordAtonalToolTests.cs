namespace GaMcpServer.Tests;

using GaMcpServer.Tools;

[TestFixture]
public sealed class ChordAtonalToolTests
{
    [Test]
    public async Task GaHomometricDistinguish_AcceptsCanonicalForteLabels()
    {
        var result = await ChordAtonalTool.GaHomometricDistinguish("4-Z15", "4-Z29");

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("ICV verdict: IDENTICAL"));
            Assert.That(result, Does.Contain("Homometric but distinct"));
            Assert.That(result, Does.Not.StartWith("Error:"));
        });
    }
}
