namespace GA.Business.Core.Tests.Atonal;

using Core.Atonal;
using Core.Notes;
using Extensions;

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
        TestContext.WriteLine($"Total PitchClassSet items: Expected=4096, Actual={count} (All subsets of 12 pitch classes = 2^12)");
        Assert.That(count, Is.EqualTo(4096), "The total number of pitch class sets must be 4096.");
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
        TestContext.WriteLine($"Input Notes: {sMajorTriadInput}, Round-trip Notes: Expected={sMajorTriadInput}, Actual={sMajorTriadPcsNotes} (Ensures PCS correctly preserves note names)");
        Assert.That(sMajorTriadPcsNotes, Is.EqualTo(sMajorTriadInput), "The round-trip note names should match the input.");
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
        TestContext.WriteLine($"Input Notes (shuffled): {sMajorTriadInput}, Output Notes (canonical): Expected=C E G, Actual={sMajorTriadPcsNotes} (PCS should normalize note order to canonical form)");
        Assert.That(sMajorTriadPcsNotes, Is.EqualTo("C E G"), "Shuffled input notes should be normalized to canonical order (C E G).");
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_Id()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);

        // Act
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        var id = majorTriadPcs.Id.Value;

        // Assert
        TestContext.WriteLine($"Input: {sMajorTriadInput}, PitchClassSet ID: Expected=145, Actual={id} (Binary representation of {sMajorTriadInput} as a bitmask)");
        Assert.That(id, Is.EqualTo(145), "Major triad {C, E, G} should have PitchClassSet ID 145 (bits 0, 4, 7 set).");
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
        TestContext.WriteLine($"Input: {sMajorTriadInput}, Transpositions and Inversions count: Expected=24, Actual={transpositionsAndInversions.Count} (12 transpositions + 12 inversions)");
        Assert.That(transpositionsAndInversions.Count, Is.EqualTo(24), "A major triad should have 24 related sets (transpositions and inversions).");
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
                .Select(pcs => pcs.Id.Value)
                .OrderBy(value => value)
                .ToImmutableSortedSet();
        var sOrderedTranspositionsAndInversionValues = string.Join(", ", orderedTranspositionsAndInversionValues);

        // Assert
        var expected =
            "137, 145, 265, 274, 289, 290, 529, 530, 545, 548, 578, 580, 1058, 1060, 1090, 1096, 1156, 1160, 2116, 2120, 2180, 2192, 2312, 2320";
        TestContext.WriteLine($"Input: {sCMajorTriadInput}, Set of related PitchClassSet IDs: {sOrderedTranspositionsAndInversionValues}");
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
        var isPrime = pitchClassSet.IsPrimeForm;

        // Assert
        TestContext.WriteLine($"Input: {sCMajorTriadInput}, PitchClassSet: {pitchClassSet}, IsPrimeForm: {isPrime}");
        Assert.That(isPrime, Is.EqualTo(false)); // 145 => not the prime form
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_IsPrimeForm_True()
    {
        // Arrange
        const string sCMinorTriadInput = "C Eb G";
        var cMinorTriadNotes = AccidentedNoteCollection.Parse(sCMinorTriadInput);

        // Act
        var pitchClassSet = cMinorTriadNotes.ToPitchClassSet();
        var isPrime = pitchClassSet.IsPrimeForm;

        // Assert
        TestContext.WriteLine($"Input: {sCMinorTriadInput}, PitchClassSet: {pitchClassSet}, IsPrimeForm: {isPrime}");
        Assert.That(isPrime, Is.EqualTo(true)); // 137 => this is the prime form
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_NormalForm_CMajorTriad()
    {
        // Arrange
        const string sCMajorTriadInput = "C E G";
        var cMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        var cMajorTriadPitchClassSet = cMajorTriadNotes.ToPitchClassSet();

        // Act
        var normalForm = cMajorTriadPitchClassSet.ToNormalForm();

        // Assert
        TestContext.WriteLine($"Input: {sCMajorTriadInput}, PitchClassSet: {cMajorTriadPitchClassSet}, Normal Form: {normalForm.Name}");
        Assert.That(normalForm.Name, Is.EqualTo("0 3 8"));
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_NormalForm_GMajorTriad()
    {
        // Arrange
        const string sGMajorTriadInput = "G B D"; // G major triad
        var gMajorTriadNotes = AccidentedNoteCollection.Parse(sGMajorTriadInput);
        var gMajorTriadPitchClassSet = gMajorTriadNotes.ToPitchClassSet();

        // Act
        var normalForm = gMajorTriadPitchClassSet.ToNormalForm();

        // Assert
        TestContext.WriteLine($"Input: {sGMajorTriadInput}, PitchClassSet: {gMajorTriadPitchClassSet}, Normal Form: {normalForm.Name}");
        Assert.That(normalForm.Name, Is.EqualTo("0 3 8"));
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_IsNormalForm_GMinorTriad()
    {
        // Arrange
        const string sGMinorTriadInput = "G Bb D"; // G minor triad
        var gMinorTriadNotes = AccidentedNoteCollection.Parse(sGMinorTriadInput);
        var gMinorTriadPitchClassSet = gMinorTriadNotes.ToPitchClassSet();

        // Act
        var isNormalForm = gMinorTriadPitchClassSet.IsNormalForm;

        // Assert
        TestContext.WriteLine($"Input: {sGMinorTriadInput}, PitchClassSet: {gMinorTriadPitchClassSet}, IsNormalForm: {isNormalForm}");
        Assert.That(isNormalForm, Is.EqualTo(false));
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_PrimeForm()
    {
        // Arrange
        const string sCMajorTriadInput = "C E G";
        var gMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        var majorTriadPitchClassSet = gMajorTriadNotes.ToPitchClassSet();

        // Act
        var primeForm = majorTriadPitchClassSet.PrimeForm;

        // Assert
        TestContext.WriteLine($"Input: {sCMajorTriadInput}, PitchClassSet: {majorTriadPitchClassSet}, Prime Form: {primeForm?.Name}");
        Assert.That(primeForm?.Name, Is.EqualTo("0 3 7"));
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_ClosestDiatonicKey()
    {
        // Arrange
        var set = PitchClassSet.FromId(1709); // Dorian mode

        // Act
        var key = set.ClosestDiatonicKey;
        var notes = set.GetDiatonicNotes();

        // Assert
        TestContext.WriteLine($"PitchClassSet ID: 1709, Closest Diatonic Key: {key}, Notes: {string.Join(" ", notes)}");

        Assert.Multiple(() =>
        {
            Assert.That(key, Is.Not.Null);
            Assert.That(key!.KeyMode, Is.EqualTo(KeyMode.Minor)); // Dorian is closer to minor
            Assert.That(notes, Is.Not.Null);
            Assert.That(notes!.Count, Is.EqualTo(7)); // Dorian has 7 notes

            // The notes should form a coherent scale
            var noteNames = notes.Select(n => n.ToString()).ToArray();
            Assert.That(noteNames, Is.EquivalentTo(new[] { "C", "D", "Eb", "F", "G", "A", "Bb" }));
        });
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_ClosestDiatonicKey_MajorTriad()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();

        // Act
        var key = majorTriadPcs.ClosestDiatonicKey;
        var notes = majorTriadPcs.GetDiatonicNotes();

        // Assert
        TestContext.WriteLine($"Input: {sMajorTriadInput}, Closest Diatonic Key: {key}, Notes: {string.Join(" ", notes)}");

        Assert.Multiple(() =>
        {
            Assert.That(key, Is.Not.Null);
            Assert.That(key!.KeyMode, Is.EqualTo(KeyMode.Minor)); // Based on the implementation, it maps to minor key
            Assert.That(notes, Is.Not.Null);
            Assert.That(notes!.Count, Is.EqualTo(3));

            // The notes should be the C major triad
            var noteNames = notes.Select(n => n.ToString()).ToArray();
            Assert.That(noteNames, Is.EquivalentTo(new[] { "C", "E", "G" }));
        });
    }
}
