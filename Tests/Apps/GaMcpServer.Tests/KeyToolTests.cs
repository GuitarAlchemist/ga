namespace GaMcpServer.Tests;

using GaMcpServer.Tools;

[TestFixture]
public class KeyToolTests
{
    [TestCase("Key of C", "Key of Cm")]
    [TestCase("Key of Am", "Key of A")]
    public void GetParallelKey_PreservesTonicAndChangesMode(string keyName, string expected) =>
        Assert.That(KeyTool.GetParallelKey(keyName), Is.EqualTo(expected));

    [Test]
    public void GetNeighboringKeys_ForCMajor_ReturnsFAndGMajor()
    {
        var neighbors = KeyTool.GetNeighboringKeys("Key of C");

        Assert.Multiple(() =>
        {
            Assert.That(neighbors.Previous, Is.EqualTo("Key of F"));
            Assert.That(neighbors.Current, Is.EqualTo("Key of C"));
            Assert.That(neighbors.Next, Is.EqualTo("Key of G"));
        });
    }
}
