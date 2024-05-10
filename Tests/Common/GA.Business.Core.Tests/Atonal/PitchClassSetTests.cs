namespace GA.Business.Core.Tests.Atonal;

using Extensions;
using GA.Business.Core.Atonal;
using GA.Business.Core.Notes;

public class PitchClassSetTests
{
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_All()
    {
        // Arrange
        var items = PitchClassSet.Items;
        
        // Act
        var count = items.Count;
        
        // Assert
        Assert.That(count, Is.EqualTo(4096));
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_NotesRoundTrip()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        
        // Act
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        var sMajorTriadPcsNotes = string.Join(" ", majorTriadPcs.Notes);
        
        // Assert
        Assert.That(sMajorTriadPcsNotes, Is.EqualTo(sMajorTriadInput));
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_NotesRoundTrip_ShuffleNotes()
    {
        // Arrange
        const string sMajorTriadInput = "C G E";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        
        // Act
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        var sMajorTriadPcsNotes = string.Join(" ", majorTriadPcs.Notes);
        
        // Assert
        Assert.That(sMajorTriadPcsNotes, Is.EqualTo("C E G"));
    }    

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_Identity()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        
        // Act
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        
        // Assert
        Assert.That(majorTriadPcs.Identity.Value, Is.EqualTo(145));
        Assert.That(majorTriadPcs.Identity.ScalePageUrl.AbsoluteUri, Is.EqualTo("https://ianring.com/musictheory/scales/145"));
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_TranspositionsAndInversions_Count()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        
        // Act
        var pitchClassSet = majorTriadNotes.ToPitchClassSet();
        var transpositionsAndInversions = pitchClassSet.TranspositionsAndInversions;
        
        // Assert
        Assert.That(transpositionsAndInversions.Count, Is.EqualTo(24));
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_TranspositionsAndInversions()
    {
        // Arrange
        const string sCMajorTriadInput = "C E G";
        var cMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        
        // Act
        var pitchClassSet = cMajorTriadNotes.ToPitchClassSet();
        var transpositionsAndInversions = pitchClassSet.TranspositionsAndInversions;
        var orderedTranspositionsAndInversionValues =
            transpositionsAndInversions
                .Select(pcs => pcs.Identity.Value)
                .OrderBy(value => value)
                .ToImmutableSortedSet();
        var sOrderedTranspositionsAndInversionValues = string.Join(", ", orderedTranspositionsAndInversionValues);
        
        // Assert
        var expected = "137, 145, 265, 274, 289, 290, 529, 530, 545, 548, 578, 580, 1058, 1060, 1090, 1096, 1156, 1160, 2116, 2120, 2180, 2192, 2312, 2320";
        Assert.That(sOrderedTranspositionsAndInversionValues, Is.EqualTo(expected));
    }    
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_IsPrimeForm_False()
    {
        // Arrange
        const string sCMajorTriadInput = "C E G";
        var cMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        
        // Act
        var pitchClassSet = cMajorTriadNotes.ToPitchClassSet();
        
        // Assert
        Assert.That(pitchClassSet.IsPrimeForm, Is.EqualTo(false)); // 145 => not the prime form
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_IsPrimeForm_True()
    {
        // Arrange
        const string sCMinorTriadInput = "C Eb G";
        var cMinorTriadNotes = AccidentedNoteCollection.Parse(sCMinorTriadInput);
        
        // Act
        var pitchClassSet = cMinorTriadNotes .ToPitchClassSet();
        
        // Assert
        Assert.That(pitchClassSet.IsPrimeForm, Is.EqualTo(true)); // 137 => this is the prime form
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_NormalForm_CMajorTriad()
    {
        const string sCMajorTriadInput = "C E G";
        var cMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        var cMajorTriadPitchClassSet = cMajorTriadNotes.ToPitchClassSet();

        var normalForm = cMajorTriadPitchClassSet.ToNormalForm();
    }    

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_NormalForm_GMajorTriad()
    {
        const string sGMajorTriadInput = "G B D"; // G major triad
        var gMajorTriadNotes = AccidentedNoteCollection.Parse(sGMajorTriadInput);
        var gMajorTriadPitchClassSet = gMajorTriadNotes.ToPitchClassSet();

        var normalForm = gMajorTriadPitchClassSet.ToNormalForm();
    }
    
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_PrimeForm()
    {
        const string sCMajorTriadInput = "C E G";
        var gMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        var majorTriadPitchClassSet = gMajorTriadNotes.ToPitchClassSet();

        var primeForm = majorTriadPitchClassSet.PrimeForm;
    }    
}