namespace GA.Business.ML.Tabs;

using System.Text.RegularExpressions;
using Domain.Core.Theory.Atonal;
using Domain.Services.Fretboard.Voicings.Analysis;
using Models;
using Musical.Analysis;

// For PitchClass, PitchClassSet

public class TabAnalysisService(
    TabTokenizer tokenizer,
    TabToPitchConverter converter,
    MusicalEmbeddingGenerator generator,
    CadenceDetector cadenceDetector)
{
    private readonly CadenceDetector _cadenceDetector = cadenceDetector;
    private readonly TabToPitchConverter _converter = converter;
    private readonly MusicalEmbeddingGenerator _generator = generator;
    private readonly TabTokenizer _tokenizer = tokenizer;

    public TabAnalysisService(TabTokenizer tokenizer, TabToPitchConverter converter,
        MusicalEmbeddingGenerator generator)
        : this(tokenizer, converter, generator, new())
    {
    }

    public async Task<TabAnalysisResult> AnalyzeAsync(string asciiTab)
    {
        // Check for compact diagrams like x02210 or 3x0003
        var compactMatch = Regex.Match(asciiTab, @"\b([x\d]{6})\b");
        // Check for hyphenated diagrams like x-x-0-2-3-1
        var hyphenMatch = Regex.Match(asciiTab, @"\b((?:[x\d]-){5}[x\d])\b");

        List<TabBlock> blocks;

        if (compactMatch.Success)
        {
            var code = compactMatch.Groups[1].Value;
            var notes = new List<TabNote>();
            for (var i = 0; i < 6; i++)
            {
                var c = code[i];
                if (char.IsDigit(c))
                {
                    // "x02210" -> Index 0 is Low E (String 0)
                    notes.Add(new(i, c - '0'));
                }
            }

            blocks =
            [
                new()
                {
                    StringCount = 6,
                    Slices = { new() { Notes = notes } }
                }
            ];
        }
        else if (hyphenMatch.Success)
        {
            var code = hyphenMatch.Groups[1].Value.Replace("-", "");
            var notes = new List<TabNote>();
            for (var i = 0; i < 6; i++)
            {
                var c = code[i];
                if (char.IsDigit(c))
                {
                    // "x-x-0-2-3-1" -> Index 0 is Low E
                    notes.Add(new(i, c - '0'));
                }
            }

            blocks =
            [
                new()
                {
                    StringCount = 6,
                    Slices = { new() { Notes = notes } }
                }
            ];
        }
        else
        {
            blocks = _tokenizer.Tokenize(asciiTab);
        }

        var events = new List<TabEvent>();
        var index = 0;

        foreach (var block in blocks)
        {
            foreach (var slice in block.Slices)
            {
                if (slice.IsEmpty || slice.IsBarLine)
                {
                    continue;
                }

                if (slice.Notes.Count == 0)
                {
                    continue;
                }

                var midiNotes = _converter.GetMidiNotes(slice);
                if (midiNotes.Count == 0)
                {
                    continue;
                }

                var pitchClasses = _converter.GetPitchClasses(slice);

                // Use Core Analyzer for robust identification (Root, Name, Function)
                var pcsList = pitchClasses.Select(pc => PitchClass.FromValue(pc)).ToList();
                var pcsSet = new PitchClassSet(pcsList);
                var bassPc = PitchClass.FromValue(midiNotes.Min() % 12);

                var analysis = VoicingHarmonicAnalyzer.IdentifyChord(pcsSet, pcsList, bassPc);

                // Use domain model to get Forte number
                var forteCode = ForteCatalog.TryGetForteNumber(pcsSet.PrimeForm, out var forte)
                    ? forte.ToString()
                    : null;

                var doc = new ChordVoicingRagDocument
                {
                    Id = Guid.NewGuid().ToString(),
                    ChordName = analysis.ChordName ?? AnalysisConstants.Unknown,
                    RootPitchClass = PitchClass.TryParse(analysis.RootPitchClass, null, out var r)
                        ? r.Value
                        : midiNotes.Min() % 12,
                    MidiNotes = [.. midiNotes],
                    PitchClasses = [.. pitchClasses],
                    PitchClassSet = string.Join(",", pitchClasses),
                    SemanticTags = Array.Empty<string>(), // Will be populated by AutoTaggingService
                    Diagram = FormatDiagram(slice, blocks),

                    // Richer Metadata from Analyzer
                    HarmonicFunction = analysis.HarmonicFunction,
                    Quality = analysis.Quality,
                    IsNaturallyOccurring = analysis.IsNaturallyOccurring,

                    // Defaults required by schema logic
                    IntervalClassVector = pcsSet.IntervalClassVector.ToString(),
                    Consonance = Math.Max(0.0, 1.0 - pcsSet.IntervalClassVector.Sum() / 12.0),
                    MinFret = slice.Notes.Min(n => n.Fret),
                    MaxFret = slice.Notes.Max(n => n.Fret),

                    // Required fields
                    SearchableText = $"{analysis.ChordName} {analysis.HarmonicFunction}",
                    PossibleKeys = [.. pcsSet.GetCompatibleKeys().Select(k => k.ToString())],
                    YamlAnalysis = "{}",
                    AnalysisEngine = AnalysisConstants.TabAnalysisEngine,
                    AnalysisVersion = "1.1",
                    Jobs = Array.Empty<string>(),
                    TuningId = AnalysisConstants.DefaultTuning,
                    PitchClassSetId = pcsSet.Id.ToString(),
                    ForteCode = forteCode
                };

                var embedding = await _generator.GenerateEmbeddingAsync(doc);

                events.Add(new()
                {
                    TimestampIndex = index++,
                    OriginalSlice = slice,
                    Document = doc,
                    Embedding = embedding
                });
            }
        }

        // Detect Cadence
        var progression = events.Select(e => e.Document).ToList();
        var cadenceMatch = _cadenceDetector.DetectCadence(progression);
        var cadenceString = cadenceMatch != null
            ? $"{cadenceMatch.Definition.Name} in Key of {cadenceMatch.Key.ToSharpNote()}"
            : null;

        return new() { Events = events, DetectedCadence = cadenceString };
    }

    private string FormatDiagram(TabSlice slice, List<TabBlock> blocks)
    {
        // Infer string count from the block or slice
        // TabSlice doesn't check string count, but Notes has StringIndex.
        // Assuming Standard Tuning 6 strings for diagram if not specified?
        // Or calculate max StringIndex + 1.

        var maxStringIdx = 5; // Default to 6-string guitar
        if (slice.Notes.Any())
        {
            maxStringIdx = Math.Max(maxStringIdx, slice.Notes.Max(n => n.StringIndex));
        }

        // Build array of "x"
        var frets = new string[maxStringIdx + 1];
        Array.Fill(frets, "x");

        foreach (var note in slice.Notes)
        {
            if (note.StringIndex >= 0 && note.StringIndex < frets.Length)
            {
                frets[note.StringIndex] = note.Fret.ToString();
            }
        }

        return string.Join("-", frets);
    }
}
