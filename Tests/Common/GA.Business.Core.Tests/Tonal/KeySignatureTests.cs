#pragma warning disable CA1861 // Avoid constant arrays as arguments

namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class KeySignatureTests
{
    [TestCase(-7, new[] { "Bb", "Eb", "Ab", "Db", "Gb", "Cb", "Fb" })]
    [TestCase(-6, new[] { "Bb", "Eb", "Ab", "Db", "Gb", "Cb" })]
    [TestCase(-5, new[] { "Bb", "Eb", "Ab", "Db", "Gb" })]
    [TestCase(-4, new[] { "Bb", "Eb", "Ab", "Db" })]
    [TestCase(-3, new[] { "Bb", "Eb", "Ab" })]
    [TestCase(-2, new[] { "Bb", "Eb" })]
    [TestCase(-1, new[] { "Bb" })]
    [TestCase(0, new string[] { })]
    [TestCase(1, new[] { "F#" })]
    [TestCase(2, new[] { "F#", "C#" })]
    [TestCase(3, new[] { "F#", "C#", "G#" })]
    [TestCase(4, new[] { "F#", "C#", "G#", "D#" })]
    [TestCase(5, new[] { "F#", "C#", "G#", "D#", "A#" })]
    [TestCase(6, new[] { "F#", "C#", "G#", "D#", "A#", "E#" })]
    [TestCase(7, new[] { "F#", "C#", "G#", "D#", "A#", "E#", "B#" })]
    public void KeySignature_AccidentedNotes(int value, string[] expected)
    {
        // Arrange
        var keySignature = KeySignature.FromValue(value);

        // Act
        var actual = keySignature.AccidentedNotes.Select(note => note.ToString()).ToArray();

        // Assert
        TestContext.Out.WriteLine($"[KeySignature_AccidentedNotes] value={value}\n  expected=[{string.Join(", ", expected)}]\n  actual=[{string.Join(", ", actual)}]");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-7, "Bb Eb Ab Db Gb Cb Fb")]
    [TestCase(-6, "Bb Eb Ab Db Gb Cb")]
    [TestCase(-5, "Bb Eb Ab Db Gb")]
    [TestCase(-4, "Bb Eb Ab Db")]
    [TestCase(-3, "Bb Eb Ab")]
    [TestCase(-2, "Bb Eb")]
    [TestCase(-1, "Bb")]
    [TestCase(0, "")]
    [TestCase(1, "F#")]
    [TestCase(2, "F# C#")]
    [TestCase(3, "F# C# G#")]
    [TestCase(4, "F# C# G# D#")]
    [TestCase(5, "F# C# G# D# A#")]
    [TestCase(6, "F# C# G# D# A# E#")]
    [TestCase(7, "F# C# G# D# A# E# B#")]
    public void KeySignature_AccidentedNotes_Printable(int value, string expected)
    {
        // Arrange
        var keySignature = KeySignature.FromValue(value);

        // Act
        var actual = keySignature.AccidentedNotes.ToString();

        // Assert
        TestContext.Out.WriteLine($"[KeySignature_AccidentedNotes_Printable] value={value}\n  expected='{expected}'\n  actual='{actual}'");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-7, AccidentalKind.Flat)]
    [TestCase(-6, AccidentalKind.Flat)]
    [TestCase(-5, AccidentalKind.Flat)]
    [TestCase(-4, AccidentalKind.Flat)]
    [TestCase(-3, AccidentalKind.Flat)]
    [TestCase(-2, AccidentalKind.Flat)]
    [TestCase(-1, AccidentalKind.Flat)]
    [TestCase(0, AccidentalKind.Sharp)] // Assuming that the accidental kind for a neutral key signature is sharp
    [TestCase(1, AccidentalKind.Sharp)]
    [TestCase(2, AccidentalKind.Sharp)]
    [TestCase(3, AccidentalKind.Sharp)]
    [TestCase(4, AccidentalKind.Sharp)]
    [TestCase(5, AccidentalKind.Sharp)]
    [TestCase(6, AccidentalKind.Sharp)]
    [TestCase(7, AccidentalKind.Sharp)]
    public void KeySignature_AccidentalKind(int value, AccidentalKind expected)
    {
        // Arrange
        var keySignature = KeySignature.FromValue(value);

        // Act
        var actual = keySignature.AccidentalKind;

        // Assert
        TestContext.Out.WriteLine($"[KeySignature_AccidentalKind] value={value}\n  expected={expected}\n  actual={actual}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-7, false)]
    [TestCase(-6, false)]
    [TestCase(-5, false)]
    [TestCase(-4, false)]
    [TestCase(-3, false)]
    [TestCase(-2, false)]
    [TestCase(-1, false)]
    [TestCase(0, true)] // Assuming that the key signature for a neutral key is considered a sharp key
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    [TestCase(5, true)]
    [TestCase(6, true)]
    [TestCase(7, true)]
    public void KeySignature_IsSharpKey(int value, bool expected)
    {
        // Arrange
        var keySignature = KeySignature.FromValue(value);

        // Act
        var actual = keySignature.IsSharpKey;

        // Assert
        TestContext.Out.WriteLine($"[KeySignature_IsSharpKey] value={value}\n  expected={expected}\n  actual={actual}");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-7, true)]
    [TestCase(-6, true)]
    [TestCase(-5, true)]
    [TestCase(-4, true)]
    [TestCase(-3, true)]
    [TestCase(-2, true)]
    [TestCase(-1, true)]
    [TestCase(0, false)] // Assuming that the key signature for a neutral key is not considered a flat key
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(5, false)]
    [TestCase(6, false)]
    [TestCase(7, false)]
    public void KeySignature_IsFlatKey(int value, bool expected)
    {
        // Arrange
        var keySignature = KeySignature.FromValue(value);

        // Act
        var actual = keySignature.IsFlatKey;

        // Assert
        TestContext.Out.WriteLine($"[KeySignature_IsFlatKey] value={value}\n  expected={expected}\n  actual={actual}");
        Assert.That(actual, Is.EqualTo(expected));
    }
}
