namespace GA.Business.ML.Tests.Search;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Embeddings.Services;
using GA.Business.ML.Search;

/// <summary>
///     Golden test for the query side of OPTIC-K retrieval: every chord symbol in the corpus below must parse
///     (<see cref="ChordPitchClasses.TryParse"/>) and encode (<see cref="MusicalQueryEncoder.Encode"/>) exactly as
///     it did when the golden file was generated. The corpus covers every recognised root spelling (plus a few
///     the parser refuses today: B#, E#, Fb) crossed with the representative qualities and the usual edge forms.
/// </summary>
/// <remarks>
///     A safety net for any change to the chord reader behind query encoding (for example, sharing it with
///     <see cref="GA.Business.ML.Agents.ChordVocabulary"/>). Regenerate it only for a deliberate, reviewed change:
///     run <see cref="Regenerate"/> explicitly. Vectors are compared through a digest of their components rounded
///     to 6 decimals, so the comparison does not depend on platform floating-point noise.
/// </remarks>
[TestFixture]
[Category("Unit")]
public class ChordPitchClassesGoldenTests
{
    private const string GoldenFileName = "chord-pitch-classes.golden.tsv";

    private static readonly string[] Roots =
    [
        "C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B", "Cb",
        "c", "bb", "f#", "B#", "E#", "Fb",
    ];

    private static readonly string[] ExtraQualities =
    [
        "M", "M7", "M9", "△7", "Δ", "ø", "ø7", "°", "°7", "o7", "-", "-7", "min", "minor", "Maj7", "major",
        "7(#9)", "7(b9)", "m(maj7)", "(add9)", "add#11", "addb9", "13no11", "9no3", "maj7no5", "7sus4b9", "m11b5",
        "13#9", "maj9#11", "/G", "maj7/E", "m7/Bb", "xyz", "ade", "ee",
    ];

    private static IEnumerable<string> Symbols() =>
        Roots.SelectMany(root => ChordPitchClasses.KnownQualities.Concat(ExtraQualities).Distinct().Select(q => root + q));

    private static string Describe(MusicalQueryEncoder encoder, string symbol)
    {
        var ok = ChordPitchClasses.TryParse(symbol, out var root, out var pcs);
        var vector = encoder.Encode(new StructuredQuery(symbol, null, null, null, null));
        var rounded = string.Join(",", vector.Select(v => Math.Round(v, 6).ToString("F6", CultureInfo.InvariantCulture)));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rounded)))[..16];
        return $"{symbol}\t{ok}\t{root?.ToString(CultureInfo.InvariantCulture) ?? "-"}\t{string.Join(",", pcs)}\t{digest}";
    }

    [Test]
    public void ParseAndEncode_MatchGolden()
    {
        var encoder = new MusicalQueryEncoder(new ModalVectorService());
        var golden = File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Search", "Golden", GoldenFileName));

        Assert.That(golden, Has.Length.EqualTo(Symbols().Count()), "golden corpus and test corpus differ; regenerate deliberately");

        var mismatches = golden
            .Select(line => (Expected: line, Symbol: line.Split('\t')[0]))
            .Select(row => (row.Expected, Actual: Describe(encoder, row.Symbol)))
            .Where(row => row.Expected != row.Actual)
            .Select(row => $"expected {row.Expected}\n  actual {row.Actual}")
            .ToList();

        Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches.Take(20)));
    }

    [Test]
    [Explicit("Regenerates the golden file in the source tree; run only for a deliberate, reviewed change.")]
    public void Regenerate()
    {
        var encoder = new MusicalQueryEncoder(new ModalVectorService());
        var path = Path.Combine(Path.GetDirectoryName(SourceFile())!, "Golden", GoldenFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, Symbols().Select(s => Describe(encoder, s)));
    }

    private static string SourceFile([CallerFilePath] string path = "") => path;
}
