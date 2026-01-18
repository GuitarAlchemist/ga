namespace GA.Business.Core.Tests.Notes;

using Core.Atonal;
using Core.Notes;

public class PitchClassTests
{
    [Test(TestOf = typeof(PitchClass))]
    public void FromValue_ReturnsCorrectPitchClass()
    {
        // Arrange
        int value = 0;

        // Act
        var pc = PitchClass.FromValue(value);

        // Assert
        TestContext.WriteLine($"Value: {value}, PitchClass: {pc}");
        Assert.Multiple(() =>
        {
            Assert.That(pc.Value, Is.EqualTo(value));
            Assert.That(pc.ToString(), Is.EqualTo("0"));
        });
    }

    [Test(TestOf = typeof(PitchClass))]
    public void Parse_ReturnsCorrectPitchClass()
    {
        // Arrange
        string input = "7";

        // Act
        var pc = PitchClass.Parse(input, null);

        // Assert
        TestContext.WriteLine($"Input: {input}, Parsed PitchClass: {pc}");
        Assert.That(pc.Value, Is.EqualTo(7));
    }

    [Test(TestOf = typeof(PitchClass))]
    public void ToChromaticNote_ReturnsCorrectNote()
    {
        // Arrange
        var pc = PitchClass.FromValue(4); // E

        // Act
        var note = pc.ToChromaticNote();

        // Assert
        TestContext.WriteLine($"PitchClass: {pc}, Chromatic Note: {note}");
        Assert.That(note, Is.EqualTo(Note.Chromatic.E));
    }
}
