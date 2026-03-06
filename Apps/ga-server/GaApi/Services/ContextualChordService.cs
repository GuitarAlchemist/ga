namespace GaApi.Services;

using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Core.Theory.Tonal.Modes;
using GA.Domain.Core.Theory.Tonal.Modes.Diatonic;
using GA.Domain.Core.Theory.Tonal.Primitives.Diatonic;
using GA.Domain.Core.Theory.Tonal.Scales;
using GA.Domain.Services.Chords;
using GaApi.Models;
using Chord = GA.Domain.Core.Theory.Harmony.Chord;

public class ContextualChordService
{
    public async Task<IEnumerable<ChordInContext>> GetChordsForKeyAsync(string keyName)
    {
        var (root, scale) = ParseKey(keyName);
        var baseMode = scale == ScaleType.Major 
            ? (ScaleMode)MajorScaleMode.Get(1) 
            : (ScaleMode)MajorScaleMode.Get(6);

        return await GenerateChordsForModeAsync(baseMode, root);
    }

    public async Task<IEnumerable<ChordInContext>> GetChordsForScaleAsync(string scaleName, string rootName)
    {
        if (!Note.Accidented.TryParse(rootName, null, out var root))
            throw new ArgumentException($"Invalid root note: {rootName}");

        // For now, simplify scale lookup
        ScaleMode? mode = null;
        if (scaleName.Contains("Major", StringComparison.OrdinalIgnoreCase)) mode = MajorScaleMode.Get(1);
        if (scaleName.Contains("Minor", StringComparison.OrdinalIgnoreCase)) mode = MajorScaleMode.Get(6);
        
        if (mode == null) throw new ArgumentException($"Scale not supported: {scaleName}");

        return await GenerateChordsForModeAsync(mode, root);
    }

    public async Task<IEnumerable<ChordInContext>> GetChordsForModeAsync(string modeName, string rootName)
    {
        if (!Note.Accidented.TryParse(rootName, null, out var root))
            throw new ArgumentException($"Invalid root note: {rootName}");

        // Simple mode mapping
        ScaleMode? mode = modeName.ToLowerInvariant() switch
        {
            "ionian" => MajorScaleMode.Get(1),
            "dorian" => MajorScaleMode.Get(2),
            "phrygian" => MajorScaleMode.Get(3),
            "lydian" => MajorScaleMode.Get(4),
            "mixolydian" => MajorScaleMode.Get(5),
            "aeolian" => MajorScaleMode.Get(6),
            "locrian" => MajorScaleMode.Get(7),
            _ => null
        };

        if (mode == null) throw new ArgumentException($"Mode not supported: {modeName}");

        return await GenerateChordsForModeAsync(mode, root);
    }

    /// <summary>
    ///     Returns chords that can be borrowed (via modal interchange) from the parallel modes
    ///     of <paramref name="keyName"/>.  A chord is considered borrowed when it has a different
    ///     symbol than the home-key chord at the same scale degree AND it does not already appear
    ///     anywhere in the home key's diatonic chord set.
    /// </summary>
    public async Task<IEnumerable<BorrowedChordInContext>> GetBorrowedChordsAsync(string keyName)
    {
        var (root, scaleType) = ParseKey(keyName);
        var homeModeDegree = scaleType == ScaleType.Major ? 1 : 6;
        var homeMode = (ScaleMode)MajorScaleMode.Get(homeModeDegree);

        var homeChords = (await GenerateChordsForModeAsync(homeMode, root)).ToList();
        var homeSymbols = homeChords
            .Select(c => c.ContextualName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<BorrowedChordInContext>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] modeNames = ["Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian"];

        for (var modeDegree = 1; modeDegree <= 7; modeDegree++)
        {
            if (modeDegree == homeModeDegree) continue;

            var parallelMode = (ScaleMode)MajorScaleMode.Get(modeDegree);
            var parallelChords = (await GenerateChordsForModeAsync(parallelMode, root)).ToList();

            for (var i = 0; i < Math.Min(parallelChords.Count, homeChords.Count); i++)
            {
                var pc = parallelChords[i];
                var hc = homeChords[i];

                if (string.Equals(pc.ContextualName, hc.ContextualName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (homeSymbols.Contains(pc.ContextualName))
                    continue;

                if (!seen.Add(pc.ContextualName))
                    continue;

                results.Add(new BorrowedChordInContext(
                    pc.TemplateName,
                    pc.Root,
                    pc.ContextualName,
                    pc.ScaleDegree,
                    pc.Function,
                    pc.Commonality,
                    pc.IsNaturallyOccurring,
                    pc.AlternateNames,
                    pc.Notes,
                    modeNames[modeDegree - 1],
                    pc.RomanNumeral,
                    pc.FunctionalDescription));
            }
        }

        return results;
    }

    private async Task<IEnumerable<ChordInContext>> GenerateChordsForModeAsync(ScaleMode baseMode, Note.Accidented root)
    {
        var templates = ChordTemplateFactory.CreateModalChords(baseMode).ToList();
        var results = new List<ChordInContext>();

        for (int i = 0; i < templates.Count; i++)
        {
            var template = templates[i];
            if (template is ChordTemplate.TonalModal modal)
            {
                var degreeRootPC = (root.PitchClass.Value + baseMode.Notes.ElementAt(i).PitchClass.Value - baseMode.Notes.First().PitchClass.Value + 12) % 12;
                var degreeRoot = new PitchClass { Value = degreeRootPC }.ToChromaticNote().ToAccidented();
                
                var chord = new Chord(degreeRoot, modal.Formula);
                
                results.Add(new ChordInContext(
                    modal.ChordFormula.Name,
                    degreeRoot.ToString(),
                    chord.Symbol,
                    i + 1,
                    modal.Function.ToString(),
                    1.0,
                    true,
                    [],
                    [.. chord.Notes.Select(n => n.ToString())],
                    GetRomanNumeral(i + 1, chord.Quality),
                    modal.Description
                ));
            }
        }

        return await Task.FromResult(results);
    }

    private static string GetRomanNumeral(int degree, ChordQuality quality)
    {
        string[] upper = ["I", "II", "III", "IV", "V", "VI", "VII"];
        string[] lower = ["i", "ii", "iii", "iv", "v", "vi", "vii"];
        
        var numeral = (quality == ChordQuality.Minor || quality == ChordQuality.Diminished) 
            ? lower[degree - 1] 
            : upper[degree - 1];
            
        if (quality == ChordQuality.Diminished) numeral += "°";
        if (quality == ChordQuality.Augmented) numeral += "+";
        
        return numeral;
    }

    private enum ScaleType { Major, Minor }

    private (Note.Accidented Root, ScaleType Scale) ParseKey(string keyName)
    {
        var parts = keyName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rootStr = parts[0];
        var typeStr = parts.Length > 1 ? parts[1].ToLowerInvariant() : "major";

        if (rootStr.EndsWith("m"))
        {
            rootStr = rootStr.Substring(0, rootStr.Length - 1);
            typeStr = "minor";
        }

        if (!Note.Accidented.TryParse(rootStr, null, out var root))
        {
            throw new ArgumentException($"Invalid root note: {rootStr}");
        }

        var type = typeStr.Contains("min") ? ScaleType.Minor : ScaleType.Major;
        return (root, type);
    }
}
