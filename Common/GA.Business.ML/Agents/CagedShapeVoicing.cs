namespace GA.Business.ML.Agents;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.ML.Notation;

/// <summary>
///     Spells a major or minor triad in one of the five CAGED shapes: "C major in the A shape" →
///     <c>x-3-5-5-5-3</c>. The production OPTIC-K search cannot filter by CAGED shape, so such a
///     question used to fall through to a tag search that returned unrelated shapes. Each template
///     is the open-position shape moved up the neck, and every result is checked against the
///     triad's pitch classes before it is returned.
/// </summary>
internal static partial class CagedShapeVoicing
{
    /// <summary>Open-string pitch classes in standard tuning, string 6 (low E) first.</summary>
    private static readonly int[] OpenPitchClasses = [4, 9, 2, 7, 11, 4];

    /// <summary>Fret offsets from the root fret, string 6 first (null = muted), and the string carrying the root.</summary>
    private sealed record Template(int RootString, int?[] Offsets);

    private static readonly Dictionary<(char Shape, bool Minor), Template> Templates = new()
    {
        [('C', false)] = new(1, [null, 0, -1, -3, -2, -3]), // open C   x-3-2-0-1-0
        [('A', false)] = new(1, [null, 0, 2, 2, 2, 0]),     // open A   x-0-2-2-2-0
        [('G', false)] = new(0, [0, -1, -3, -3, -3, 0]),    // open G   3-2-0-0-0-3
        [('E', false)] = new(0, [0, 2, 2, 1, 0, 0]),        // open E   0-2-2-1-0-0
        [('D', false)] = new(2, [null, null, 0, 2, 3, 2]),  // open D   x-x-0-2-3-2
        [('C', true)]  = new(1, [null, 0, -2, -3, -2, null]), // Cm     x-3-1-0-1-x
        [('A', true)]  = new(1, [null, 0, 2, 2, 1, 0]),     // open Am  x-0-2-2-1-0
        [('G', true)]  = new(0, [0, -2, -3, -3, 0, 0]),     // Gm       3-1-0-0-3-3
        [('E', true)]  = new(0, [0, 2, 2, 0, 0, 0]),        // open Em  0-2-2-0-0-0
        [('D', true)]  = new(2, [null, null, 0, 2, 3, 1]),  // open Dm  x-x-0-2-3-1
    };

    private static readonly string[] StringNames = ["6th", "5th", "4th", "3rd", "2nd", "1st"];
    private const string Letters = "CDEFGAB";
    private static readonly int[] NaturalPitchClasses = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>A CAGED voicing: chart-order diagram (string 6 first) and the chord tone on each string.</summary>
    public sealed record Result(string ChordName, char Shape, string Diagram, int RootFret, int RootString, string?[] StringNotes);

    /// <summary>
    ///     Parses "&lt;root&gt; [major|minor] … in the &lt;C|A|G|E|D&gt; shape" and spells the voicing,
    ///     or returns null when the message is not such a request (or names a chord other than a
    ///     major or minor triad), so the caller can fall back to its search.
    /// </summary>
    public static Result? TryCreate(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var shapeMatch = ShapeRegex().Match(message);
        if (!shapeMatch.Success) return null;
        var shape = shapeMatch.Groups["shape"].Value[0];

        // Blank out the shape phrase so its letter is not read as the chord's root.
        var rest = message.Remove(shapeMatch.Index, shapeMatch.Length).Insert(shapeMatch.Index, new string(' ', shapeMatch.Length));
        var candidates = ChordRegex().Matches(rest).ToList();
        if (candidates.Count == 0) return null;

        // Prefer a root that is clearly a chord: it has an accidental or quality, or is followed by
        // major/minor/chord/barre/triad. A lone capital "A" can be the article.
        if (candidates.Count > 1)
            candidates = candidates.Where(c => c.Groups["acc"].Success || c.Groups["accword"].Success || c.Groups["q"].Success || c.Groups["word"].Success).ToList();
        if (candidates.Count != 1) return null;
        var chord = candidates[0];

        var suffix = chord.Groups["q"].Value;
        var word = chord.Groups["word"].Value.ToLowerInvariant();
        bool isMinor;
        if (suffix is "m" or "min" || word is "minor" or "min") isMinor = true;
        else if (suffix is "" or "maj" || word is "major" or "maj" or "chord" or "barre" or "triad" or "") isMinor = false;
        else return null;

        var accidental = chord.Groups["accword"].Success
            ? (chord.Groups["accword"].Value.StartsWith("f", StringComparison.OrdinalIgnoreCase) ? "b" : "#")
            : NormalizeAccidental(chord.Groups["acc"].Value);
        var rootName = chord.Groups["root"].Value + accidental;
        return Build(rootName, isMinor, shape);
    }

    /// <summary>Spells <paramref name="rootName"/> major or minor in the given CAGED shape.</summary>
    internal static Result Build(string rootName, bool isMinor, char shape)
    {
        var template = Templates[(shape, isMinor)];
        var rootPc = PitchClassOf(rootName);
        var rootFret = ((rootPc - OpenPitchClasses[template.RootString]) % 12 + 12) % 12;
        if (rootFret + template.Offsets.Where(o => o.HasValue).Min()!.Value < 0) rootFret += 12;

        var frets = template.Offsets.Select(o => o.HasValue ? rootFret + o.Value : (int?)null).ToArray();

        var third = isMinor ? 3 : 4;
        var tones = new Dictionary<int, string>
        {
            [rootPc] = rootName,
            [(rootPc + third) % 12] = Spell(rootName, 2, third),
            [(rootPc + 7) % 12] = Spell(rootName, 4, 7),
        };

        var notes = new string?[6];
        for (var s = 0; s < 6; s++)
        {
            if (frets[s] is not { } fret) continue;
            var pc = (OpenPitchClasses[s] + fret) % 12;
            // Every template is a root-position triad: each sounding note is a chord tone, the bass is the root.
            if (!tones.TryGetValue(pc, out var name))
                throw new InvalidOperationException($"CAGED {shape} shape for {rootName}: string {6 - s} fret {fret} is not a chord tone.");
            notes[s] = name;
        }

        var bass = Array.FindIndex(frets, f => f.HasValue);
        if (notes[bass] != rootName || notes.Where(n => n is not null).Distinct().Count() != 3)
            throw new InvalidOperationException($"CAGED {shape} shape for {rootName}: not a root-position triad.");

        var diagram = string.Join("-", frets.Select(f => f?.ToString() ?? "x"));
        return new Result($"{rootName} {(isMinor ? "minor" : "major")}", shape, diagram, rootFret, template.RootString, notes);
    }

    /// <summary>Markdown answer: the diagram, where the root sits, a VexTab fence, and the notes.</summary>
    internal static string Describe(Result r)
    {
        var sb = new StringBuilder();
        var where = r.RootFret == 0
            ? $"the open {StringNames[r.RootString]} string"
            : $"the {StringNames[r.RootString]} string, {Ordinal(r.RootFret)} fret";
        sb.AppendLine($"**{r.ChordName}, {r.Shape} shape**: `{r.Diagram}` (low E to high E), root on {where}.");
        if (PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(r.Diagram) is { } notation)
        {
            sb.AppendLine();
            sb.AppendLine("```vextab");
            sb.AppendLine(notation);
            sb.AppendLine("```");
        }
        sb.AppendLine();
        sb.AppendLine($"Notes, low to high: {string.Join(" ", r.StringNotes.Where(n => n is not null))}.");
        return sb.ToString();
    }

    private static string Spell(string rootName, int letterSteps, int semitones)
    {
        var letterIndex = Letters.IndexOf(rootName[0]);
        var targetLetter = (letterIndex + letterSteps) % 7;
        var targetPc = (PitchClassOf(rootName) + semitones) % 12;
        var diff = ((targetPc - NaturalPitchClasses[targetLetter]) % 12 + 12) % 12;
        var accidental = diff switch
        {
            0 => "",
            1 => "#",
            2 => "##",
            11 => "b",
            10 => "bb",
            _ => throw new InvalidOperationException($"Cannot spell {semitones} semitones above {rootName}."),
        };
        return Letters[targetLetter] + accidental;
    }

    private static int PitchClassOf(string name)
    {
        var pc = NaturalPitchClasses[Letters.IndexOf(name[0])];
        foreach (var c in name.Skip(1)) pc += c == '#' ? 1 : -1;
        return (pc % 12 + 12) % 12;
    }

    private static string NormalizeAccidental(string acc) => acc.Replace("♯", "#").Replace("♭", "b");

    private static string Ordinal(int n) => n switch
    {
        1 or 21 => $"{n}st",
        2 or 22 => $"{n}nd",
        3 or 23 => $"{n}rd",
        _ => $"{n}th",
    };

    // "A shape", "A-shape", "Ashape"; the shape letter must be a capital so "a shape" stays an article.
    [GeneratedRegex(@"\b(?<shape>[CAGED])(?:-|\s)?(?i:shape)\b")]
    private static partial Regex ShapeRegex();

    // A capital root, an optional accidental ("Bb", "B flat", "B-flat") and triad suffix, then an optional qualifying word.
    [GeneratedRegex(@"(?<![\w#♯♭])(?<root>[A-G])(?:(?<acc>#|b|♯|♭)|[\s-]+(?<accword>(?i:flat|sharp))(?![a-z]))?(?<q>maj|min|m)?(?![\w#♯♭])(?:\s+(?<word>(?i:major|minor|maj|min|chord|barre|triad)))?")]
    private static partial Regex ChordRegex();
}
