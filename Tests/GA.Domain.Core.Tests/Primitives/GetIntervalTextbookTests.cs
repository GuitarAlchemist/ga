namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;

/// <summary>
///     Measures Note.Accidented.GetInterval against textbook interval naming (issue #768).
/// </summary>
[TestFixture]
public class GetIntervalTextbookTests
{
    private static readonly string[] Letters = ["C", "D", "E", "F", "G", "A", "B"];
    private static readonly int[] LetterSemitones = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] ExpectedSemitones = [0, 0, 2, 4, 5, 7, 9, 11]; // by size 1..7
    private static readonly string[] Spellings = ["", "#", "b"];

    private record Row(string From, string To, int TextbookSize, int TextbookSemitones, string Textbook,
        int GaSize, string Ga, int GaSemitones, bool Wrong);

    private static bool IsPerfectSize(int size) => size is 1 or 4 or 5;

    private static int Acc(string s) => s switch { "#" => 1, "b" => -1, _ => 0 };

    private static string TextbookQuality(int size, int deviation)
    {
        var perfect = IsPerfectSize(size);
        var abs = Math.Abs(deviation);
        if (deviation == 0) return perfect ? "P" : "M";
        if (deviation > 0) return abs == 1 ? "A" : abs == 2 ? "AA" : new string('A', abs);
        if (perfect) return abs == 1 ? "d" : new string('d', abs);
        return abs == 1 ? "m" : new string('d', abs - 1);
    }

    private static int GaDeviation(int size, IntervalQuality q)
    {
        var perfect = IsPerfectSize(size);
        return q.Value switch
        {
            0 => 0,
            1 => 0,   // Major
            -1 => -1, // Minor
            2 => 1,
            3 => 2,
            -2 => perfect ? -1 : -2,
            -3 => perfect ? -2 : -3,
            _ => throw new InvalidOperationException($"quality {q.Value}")
        };
    }

    private static Note.Accidented Parse(string name) =>
        new(NaturalNote.Parse(name[..1], null), name.Length == 1 ? null : Accidental.FromValue(Acc(name[1..])));

    private static List<Row> Measure() => Measure(out _);

    private static List<Row> Measure(out List<string> unnameable)
    {
        unnameable = [];
        var names = Letters.SelectMany(l => Spellings.Select(s => l + s)).ToList(); // 21 names
        var rows = new List<Row>();
        foreach (var from in names)
        foreach (var to in names)
        {
            var li1 = Array.IndexOf(Letters, from[..1]);
            var li2 = Array.IndexOf(Letters, to[..1]);
            var size = (li2 - li1 + 7) % 7 + 1;
            var diff = LetterSemitones[li2] + Acc(to[1..]) - (LetterSemitones[li1] + Acc(from[1..]));
            var expected = ExpectedSemitones[size];
            // signed semitone distance for that size: nearest representative of diff mod 12 to the reference
            var deviation = ((diff - expected) % 12 + 18) % 12 - 6;
            var textbook = TextbookQuality(size, deviation) + size;

            // Textbook names with 3+ alterations (ddd5, AAA4) have no IntervalQuality: GetInterval must throw.
            if (Math.Abs(deviation) > (IsPerfectSize(size) ? 2 : (deviation < 0 ? 3 : 2)))
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(
                    () => Parse(from).GetInterval(Parse(to)), $"{from}->{to} ({textbook}) must fail explicitly");
                Assert.That(thrown, Is.Not.Null);
                rows.Add(new(from, to, size, expected + deviation, textbook, size, "throws", 0, false));
                unnameable.Add($"{from}->{to}");
                continue;
            }

            var actual = Parse(from).GetInterval(Parse(to));
            var gaDev = GaDeviation(actual.Size.Value, actual.Quality);
            var ga = TextbookQuality(actual.Size.Value, gaDev) + actual.Size.Value;
            rows.Add(new(from, to, size, expected + deviation, textbook, actual.Size.Value, ga,
                actual.Semitones.Value, textbook != ga));
        }
        return rows;
    }

    [Test]
    public void GetInterval_AllNamePairs_MatchTextbook()
    {
        var rows = Measure();
        var wrong = rows.Where(r => r.Wrong).ToList();
        TestContext.WriteLine($"pairs={rows.Count} wrong={wrong.Count}");
        foreach (var r in wrong)
            TestContext.WriteLine($"{r.From}->{r.To}: GA {r.Ga} ({r.GaSemitones} st) vs textbook {r.Textbook} ({r.TextbookSemitones} st)");
        Assert.That(rows, Has.Count.EqualTo(441));
        Assert.That(wrong, Is.Empty, $"{wrong.Count} of {rows.Count} pairs differ from the textbook interval");
    }

    [TestCase("C#", "C", "d1")]
    [TestCase("Cb", "B", "A7")]
    [TestCase("E#", "Fb", "dd2")]
    public void GetInterval_AcceptancePairs(string from, string to, string expected)
    {
        var row = Measure().Single(r => r.From == from && r.To == to);
        Assert.That(row.Textbook, Is.EqualTo(expected), "test's own textbook oracle");
        Assert.That(row.Ga, Is.EqualTo(expected),
            $"{from}->{to}: GetInterval gave {row.Ga} ({row.GaSemitones} st)");
    }

    [Test]
    public void GetInterval_OnlyTwoPairsHaveNoStandardQuality()
    {
        Measure(out var unnameable);
        Assert.That(unnameable, Is.EquivalentTo(new[] { "B#->Fb", "Fb->B#" }));
    }

    [Test]
    public void GetInterval_DiminishedUnison_ReportsMinusOneSemitone()
    {
        var interval = Parse("C#").GetInterval(Parse("C"));
        Assert.That(interval.Semitones.Value, Is.EqualTo(-1));
    }
}
