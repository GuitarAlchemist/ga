namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class KeyTests
{
    [Test]
    public void GetItems_Major_ReturnsMajorKeys()
    {
        var majorKeys = Key.GetItems(KeyMode.Major);
        Assert.AreEqual(15, majorKeys.Count);
        Assert.IsTrue(majorKeys.All(key => key.KeyMode == KeyMode.Major));
        Assert.IsTrue(majorKeys.Select(key => key.ToString()).SequenceEqual(
        [
            "Key of Cb", 
            "Key of Gb", 
            "Key of Db", 
            "Key of Ab", 
            "Key of Eb", 
            "Key of Bb", 
            "Key of F",
            "Key of C", 
            "Key of G", 
            "Key of D", 
            "Key of A", 
            "Key of E", 
            "Key of B", 
            "Key of F#", 
            "Key of C#"
        ]));
    }
    
    [Test]
    public void GetItems_Minor_ReturnsMinorKeys()
    {
        var minorKeys = Key.GetItems(KeyMode.Minor);
        Assert.AreEqual(15, minorKeys.Count);
        Assert.IsTrue(minorKeys.All(key => key.KeyMode == KeyMode.Minor));
        Assert.IsTrue(minorKeys.Select(key => key.ToString()).SequenceEqual(
        [
            "Key of Abm", 
            "Key of Ebm", 
            "Key of Bbm", 
            "Key of Fm", 
            "Key of Cm", 
            "Key of Gm", 
            "Key of Dm",
            "Key of Am", 
            "Key of Em", 
            "Key of Bm", 
            "Key of F#m", 
            "Key of C#m", 
            "Key of G#m",
            "Key of D#m", 
            "Key of A#m"
        ]));
    }
    
    [Test]
    public void Items_ReturnsAllKeys()
    {
        var allKeys = Key.Items;
        Assert.AreEqual(30, allKeys.Count);
        Assert.AreEqual(15, allKeys.Count(key => key.KeyMode == KeyMode.Major));
        Assert.AreEqual(15, allKeys.Count(key => key.KeyMode == KeyMode.Minor));
        Assert.IsTrue(allKeys.Select(key => key.ToString()).SequenceEqual(
        [
            "Key of Cb", 
            "Key of Gb", 
            "Key of Db", 
            "Key of Ab", 
            "Key of Eb", 
            "Key of Bb", 
            "Key of F",
            "Key of C", 
            "Key of G", 
            "Key of D", 
            "Key of A", 
            "Key of E", 
            "Key of B", 
            "Key of F#", 
            "Key of C#",
            "Key of Abm", 
            "Key of Ebm", 
            "Key of Bbm", 
            "Key of Fm", 
            "Key of Cm", 
            "Key of Gm", 
            "Key of Dm",
            "Key of Am", 
            "Key of Em", 
            "Key of Bm", 
            "Key of F#m", 
            "Key of C#m", 
            "Key of G#m", 
            "Key of D#m", 
            "Key of A#m"
        ]));
    }
}