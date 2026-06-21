namespace GA.Business.Core.Tests.Atonal;

using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Guards the canonicalization deepening (architecture-review candidate #4): the prime-form
///     reductions moved onto <see cref="PitchClassSetId" /> (the home of the elementary id ops), with
///     <see cref="PitchClassSet.PrimeForm" /> and <see cref="TranspositionClass" /> delegating. Prime
///     form feeds set-class enumeration → OPTIC-K, so any drift is a one-way door. These tests verify
///     the new id-level reductions against an <em>independent</em> min-over-rotations oracle across all
///     4096 ids, and pin the set-class count, so a behavioural change cannot ship silently.
/// </summary>
[TestFixture]
public class PitchClassSetCanonicalizationTests
{
    // Independent oracle: smallest id among the 12 transpositions.
    private static int MinTransposition(PitchClassSetId id)
    {
        var min = id.Value;
        for (var i = 0; i < 12; i++)
        {
            var t = id.Transpose(i).Value;
            if (t < min)
            {
                min = t;
            }
        }

        return min;
    }

    // Independent oracle: smallest id among the 24 transposition + inversion forms.
    private static int MinTranspositionInversion(PitchClassSetId id) =>
        Math.Min(MinTransposition(id), MinTransposition(id.Inverse));

    [Test]
    public void TranspositionPrimeForm_Matches_Independent_Min_Across_All_4096_Ids()
    {
        for (var v = 0; v <= 4095; v++)
        {
            var id = new PitchClassSetId(v);
            Assert.That(id.TranspositionPrimeForm.Value, Is.EqualTo(MinTransposition(id)),
                $"id {v}: TranspositionPrimeForm diverged from the min over 12 transpositions");
        }
    }

    [Test]
    public void PrimeForm_Matches_Independent_Min_Across_All_4096_Ids()
    {
        for (var v = 0; v <= 4095; v++)
        {
            var id = new PitchClassSetId(v);
            Assert.That(id.PrimeForm.Value, Is.EqualTo(MinTranspositionInversion(id)),
                $"id {v}: PrimeForm diverged from the min over 24 T/I forms");
        }
    }

    [Test]
    public void PitchClassSet_PrimeForm_Delegates_To_Id_PrimeForm()
    {
        foreach (var pcs in PitchClassSet.Items)
        {
            Assert.That(pcs.PrimeForm, Is.Not.Null);
            Assert.That(pcs.PrimeForm!.Id.Value, Is.EqualTo(pcs.Id.PrimeForm.Value),
                $"PitchClassSet.PrimeForm must equal Id.PrimeForm for id {pcs.Id.Value}");
        }
    }

    [Test]
    public void SetClass_Count_Is_Unchanged_At_224()
    {
        // The set-class enumeration is built on PrimeForm and feeds OPTIC-K. The count is a
        // one-way-door invariant: a change here means the canonicalization shifted.
        Assert.That(SetClass.Items.Count, Is.EqualTo(224));
    }

    [Test]
    public void TranspositionClass_Count_Matches_Distinct_TranspositionPrimeForms()
    {
        var distinct = new HashSet<int>();
        for (var v = 0; v <= 4095; v++)
        {
            distinct.Add(new PitchClassSetId(v).TranspositionPrimeForm.Value);
        }

        Assert.That(TranspositionClass.Items.Count, Is.EqualTo(distinct.Count));
    }

    [TestCase("047", "037")]   // C major triad → set-class prime form {0,3,7} (3-11)
    [TestCase("037", "037")]   // already a prime form
    [TestCase("0146", "0146")] // all-interval tetrachord 4-Z15 (already prime)
    public void PrimeForm_Anchors(string input, string expectedPrime)
    {
        Assert.That(PitchClassSet.TryParse(input, null, out var pcs), Is.True);
        Assert.That(PitchClassSet.TryParse(expectedPrime, null, out var expected), Is.True);

        Assert.That(pcs.PrimeForm!.Id.Value, Is.EqualTo(expected.Id.Value),
            $"prime form of {input} should be {expectedPrime}");
    }
}
