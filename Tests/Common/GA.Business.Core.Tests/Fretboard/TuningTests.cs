namespace GA.Business.Core.Tests.Fretboard;

using Core.Fretboard.Primitives;
using Core.Notes;

[TestFixture]
public class TuningTests
{
    [Test]
    public void Default_HasExpectedProperties()
    {
        // Act
        var tuning = Tuning.Default;

        // Assert
        Assert.That(tuning.PitchCollection.ToString(), Is.EqualTo("E2 A2 D3 G3 B3 E4"));
        Assert.That(tuning.PitchCollection.Count, Is.EqualTo(6));
    }

    [Test]
    public void Constructor_WithValidPitchCollection_CreatesTuning()
    {
        // Arrange
        var pitchCollection = PitchCollection.Parse("D2 A2 D3 G3 B3 E4"); // Drop D tuning

        // Act
        var tuning = new Tuning(pitchCollection);

        // Assert
        Assert.That(tuning.PitchCollection, Is.EqualTo(pitchCollection));
    }

    [Test]
    public void Constructor_WithNullPitchCollection_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Tuning(null!));
    }

    [Test]
    public void Indexer_WithValidString_ReturnsPitch()
    {
        // Arrange
        var tuning = Tuning.Default;

        // Act
        var pitch = tuning[Str.Min]; // First string (lowest)

        // Assert
        Assert.That(pitch.ToString(), Is.EqualTo("E4")); // Highest string is E4
    }

    [Test]
    public void Indexer_WithInvalidString_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tuning = Tuning.Default;
        var invalidString = Str.Min + 10; // Out of range

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = tuning[invalidString]);
    }

    [Test]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var tuning = new Tuning(PitchCollection.Parse("D2 A2 D3 G3 B3 E4")); // Drop D tuning

        // Act
        var result = tuning.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("D2 A2 D3 G3 B3 E4"));
    }

    [Test]
    public void CustomTunings_CanBeCreated()
    {
        // Test various common guitar tunings
        TestTuning("E2 A2 D3 G3 B3 E4", "Standard");
        TestTuning("D2 A2 D3 G3 B3 E4", "Drop D");
        TestTuning("D2 A2 D3 G3 A3 D4", "Open D");
        TestTuning("E2 B2 E3 G3 B3 E4", "Open E");
        TestTuning("D2 G2 D3 G3 B3 D4", "Open G");
        TestTuning("C2 G2 C3 G3 C4 E4", "Open C");

        static void TestTuning(string pitchString, string tuningName)
        {
            // Arrange
            var pitchCollection = PitchCollection.Parse(pitchString);

            // Act
            var tuning = new Tuning(pitchCollection);

            // Assert
            Assert.That(tuning, Is.Not.Null, $"Failed to create {tuningName} tuning");
            Assert.That(tuning.ToString(), Is.EqualTo(pitchString),
                $"Incorrect string representation for {tuningName} tuning");
        }
    }
}
