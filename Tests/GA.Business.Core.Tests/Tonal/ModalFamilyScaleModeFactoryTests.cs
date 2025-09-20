using System;
using System.Linq;
using NUnit.Framework;
using GA.Business.Core.Atonal;
using GA.Business.Core.Notes;
using GA.Business.Core.Scales;
using GA.Business.Core.Tonal.Modes;

namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class ModalFamilyScaleModeFactoryTests
{
    [Test]
    public void TryCreateMode_WithAllValidDegrees_CreatesModes()
    {
        // Arrange
        var scale = Scale.Major; // 7-note scale
        
        // Act & Assert
        for (int degree = 1; degree <= 7; degree++)
        {
            var mode = ModalFamilyScaleModeFactory.TryCreateMode(scale, degree);
            Assert.That(mode, Is.Not.Null, $"Should create a valid mode for degree {degree}");
            Assert.That(mode.Degree, Is.EqualTo(degree), $"Mode should have degree {degree}");
            Assert.That(mode.Notes.Count, Is.EqualTo(7), "Mode should have 7 notes");
        }
    }
    
    [Test]
    public void TryCreateMode_WithInvalidDegrees_ThrowsException()
    {
        // Arrange
        var scale = Scale.Major;
        
        // Act & Assert
        // Degree must be positive
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            ModalFamilyScaleModeFactory.TryCreateMode(scale, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            ModalFamilyScaleModeFactory.TryCreateMode(scale, -1));
            
        // Degree too high (major scale has 7 modes)
        var result = ModalFamilyScaleModeFactory.TryCreateMode(scale, 8);
        Assert.That(result, Is.Null, "Should return null for degree 8 (beyond 7 modes)");
    }
    
    [Test]
    public void TryCreateMode_WithNullScale_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            ModalFamilyScaleModeFactory.TryCreateMode(null, 1));
    }
    
    [Test]
    public void TryCreateMode_CreatesCorrectModes()
    {
        // Arrange
        var scale = Scale.Major;
        
        // Check Ionian mode (1st mode)
        var ionian = ModalFamilyScaleModeFactory.TryCreateMode(scale, 1);
        Assert.That(ionian, Is.Not.Null);
        Assert.That(ionian.Notes, Is.EqualTo(AccidentedNoteCollection.Parse("C D E F G A B")));
        Assert.That(ionian.IsMinorMode, Is.False);
        
        // Check Dorian mode (2nd mode)
        var dorian = ModalFamilyScaleModeFactory.TryCreateMode(scale, 2);
        Assert.That(dorian, Is.Not.Null);
        Assert.That(dorian.Notes, Is.EqualTo(AccidentedNoteCollection.Parse("D E F G A B C")));
        Assert.That(dorian.IsMinorMode, Is.True);
        
        // Check Lydian mode (4th mode)
        var lydian = ModalFamilyScaleModeFactory.TryCreateMode(scale, 4);
        Assert.That(lydian, Is.Not.Null);
        Assert.That(lydian.Notes, Is.EqualTo(AccidentedNoteCollection.Parse("F G A B C D E")));
        Assert.That(lydian.IsMinorMode, Is.False);
        
        // Check Aeolian mode (6th mode / natural minor)
        var aeolian = ModalFamilyScaleModeFactory.TryCreateMode(scale, 6);
        Assert.That(aeolian, Is.Not.Null);
        Assert.That(aeolian.Notes, Is.EqualTo(AccidentedNoteCollection.Parse("A B C D E F G")));
        Assert.That(aeolian.IsMinorMode, Is.True);
    }
    
    [Test]
    public void GetFamiliesByIntervalVector_WithMajorScaleVector_ReturnsFamily()
    {
        // Arrange
        var majorScaleVector = IntervalClassVector.Parse("<2 5 4 3 6 1>");
        
        // Act
        var families = ModalFamilyScaleModeFactory.GetFamiliesByIntervalVector(majorScaleVector).ToList();
        
        // Assert
        Assert.That(families, Is.Not.Empty, "Should find families with the major scale vector");
        
        foreach (var family in families)
        {
            Assert.That(family.IntervalClassVector.ToString(), Is.EqualTo("<2 5 4 3 6 1>"),
                "Each returned family should have the requested interval vector");
            
            // A 7-note scale should have exactly 7 modes
            Assert.That(family.Modes.Count, Is.EqualTo(7), 
                "A diatonic scale family should have 7 modes");
        }
    }
    
    [Test]
    public void GetFamiliesByIntervalVector_WithNullVector_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            ModalFamilyScaleModeFactory.GetFamiliesByIntervalVector(null).ToList());
    }
}
