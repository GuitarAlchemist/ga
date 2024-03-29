namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class KeyTests
{
    [Test]
    public void GetItems_Major_ReturnsMajorKeys()
    {
        var majorKeys = Key.GetItems(KeyMode.Major);
        Assert.That(majorKeys.Count, Is.EqualTo(15));
        Assert.That(majorKeys.All(key => key.KeyMode == KeyMode.Major));
        Assert.That(majorKeys.Select(key => key.ToString()).SequenceEqual(
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
        Assert.That(minorKeys.Count, Is.EqualTo(15));
        
        Assert.That(minorKeys.All(key => key.KeyMode == KeyMode.Minor));
        Assert.That(minorKeys.Select(key => key.ToString()).SequenceEqual(
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
        Assert.That(allKeys.Count, Is.EqualTo(30));
        Assert.That(allKeys.Count(key => key.KeyMode == KeyMode.Major), Is.EqualTo(15));
        Assert.That(allKeys.Count(key => key.KeyMode == KeyMode.Minor), Is.EqualTo(15));
        
        Assert.That(allKeys.Select(key => key.ToString()).SequenceEqual(
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