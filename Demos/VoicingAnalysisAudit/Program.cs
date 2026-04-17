namespace GA.Demos.VoicingAnalysisAudit;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using GA.Business.Core.Analysis.Voicings;
using GA.Domain.Core.Instruments;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using GA.Domain.Services.Fretboard.Voicings.Generation;

/// <summary>
///     One-off audit tool that runs VoicingAnalyzer over the full voicing corpus
///     (guitar + bass + ukulele, deduplicated identically to FretboardVoicingsCLI's
///     RunExportEmbeddingsAsync pipeline) and produces a detailed quality report.
///
///     DIAGNOSTIC PHASE: the goal is to answer "is voicing analysis production-quality
///     or silently broken?" BEFORE we invest in curated golden tests. This tool is
///     strictly READ-ONLY; no production code is modified and no embeddings are
///     generated (the expensive step).
///
///     Output:
///         - Human-readable summary on stdout (sections A..I).
///         - Detailed JSON report at state/audit/voicing-audit-{YYYY-MM-DD}.json.
///         - Progress + informational messages on stderr.
/// </summary>
internal static class Program
{
    private enum TuningPreset
    {
        Guitar,
        Bass,
        Ukulele
    }

    private static async Task<int> Main(string[] args)
    {
        var overallSw = Stopwatch.StartNew();

        // Resolve output path (first positional non-flag arg, else default under repo state/).
        var repoRoot = FindRepoRoot();
        var defaultOutDir = Path.Combine(repoRoot, "state", "audit");
        Directory.CreateDirectory(defaultOutDir);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var outputPath = args.FirstOrDefault(a => !a.StartsWith("--"))
                         ?? Path.Combine(defaultOutDir, $"voicing-audit-{date}.json");

        Console.Error.WriteLine("Voicing Analysis Audit — read-only diagnostic");
        Console.Error.WriteLine($"  Repo root:  {repoRoot}");
        Console.Error.WriteLine($"  Output:     {outputPath}");
        Console.Error.WriteLine();

        // Per-instrument corpus definitions: identical to FretboardVoicingsCLI.
        var presets = new[] { TuningPreset.Guitar, TuningPreset.Bass, TuningPreset.Ukulele };

        var aggregator = new Aggregator();

        foreach (var preset in presets)
        {
            var instrumentName = preset.ToString().ToLowerInvariant();
            var (tuning, fretCount) = preset switch
            {
                TuningPreset.Guitar => (Tuning.Default, 24),
                TuningPreset.Bass => (Tuning.Bass, 21),
                TuningPreset.Ukulele => (Tuning.Ukulele, 15),
                _ => (Tuning.Default, 24)
            };
            var fretboard = new Fretboard(tuning, fretCount);

            const int windowSize = 4;
            const int minPlayedNotes = 2;

            // ---------------------------------------------------------------
            // PHASE 1: generate + dedup (mirror FretboardVoicingsCLI)
            // ---------------------------------------------------------------
            var phase1Sw = Stopwatch.StartNew();
            Console.Error.Write($"  {instrumentName}: generating + deduplicating voicings...");

            var rawCount = 0;
            var survivors = new Dictionary<(string, int), DedupCandidate>();

            await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(
                               fretboard, windowSize, minPlayedNotes, parallel: true))
            {
                rawCount++;

                var relFrets = VoicingDecomposer.GetRelativeFrets(voicing.Positions);
                if (relFrets == null) continue; // malformed, same rule as CLI

                var shape = BuildRelativeFretSignature(relFrets);
                var pcMask = BuildPitchClassMask(voicing.Notes);
                var key = (shape, pcMask);

                var cost = CheapPlayabilityCost(voicing.Positions);
                if (!survivors.TryGetValue(key, out var existing) || cost < existing.Cost)
                {
                    survivors[key] = new DedupCandidate(voicing, cost);
                }

                if (rawCount % 20000 == 0)
                {
                    Console.Error.Write(
                        $"\r  {instrumentName}: generating + deduplicating voicings... {rawCount:N0} raw, {survivors.Count:N0} unique");
                }
            }

            var selected = survivors.Values.Select(c => c.Voicing).ToList();
            phase1Sw.Stop();
            var pct = rawCount > 0 ? 100.0 * selected.Count / rawCount : 0.0;
            Console.Error.WriteLine(
                $"\r  {instrumentName}: {rawCount:N0} raw → {selected.Count:N0} unique ({pct:N1}%) in {phase1Sw.Elapsed.TotalSeconds:N1}s      ");

            aggregator.StartInstrument(instrumentName, selected.Count);

            // ---------------------------------------------------------------
            // PHASE 2: analyze (NO embedding) + aggregate
            // ---------------------------------------------------------------
            var phase2Sw = Stopwatch.StartNew();
            Console.Error.Write($"  {instrumentName}: analyzing {selected.Count:N0} voicings...");

            var analyzed = 0;
            foreach (var voicing in selected)
            {
                MusicalVoicingAnalysis analysis;
                try
                {
                    analysis = VoicingAnalyzer.Analyze(voicing);
                }
                catch (Exception ex)
                {
                    aggregator.RecordAnalyzerException(instrumentName, voicing, ex);
                    continue;
                }

                aggregator.RecordVoicing(instrumentName, voicing, analysis);

                analyzed++;
                if (analyzed % 5000 == 0)
                {
                    Console.Error.Write(
                        $"\r  {instrumentName}: analyzing {selected.Count:N0} voicings... {analyzed:N0}");
                }
            }

            phase2Sw.Stop();
            Console.Error.WriteLine(
                $"\r  {instrumentName}: analyzed {analyzed:N0} in {phase2Sw.Elapsed.TotalSeconds:N1}s                  ");
        }

        overallSw.Stop();
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Corpus processed in {overallSw.Elapsed.TotalSeconds:N1}s — building report...");
        Console.Error.WriteLine();

        // ---------------------------------------------------------------
        // Build report + emit JSON + stdout summary
        // ---------------------------------------------------------------
        var report = aggregator.BuildReport(overallSw.Elapsed);
        WriteJsonReport(outputPath, report);
        PrintStdoutSummary(report, outputPath);

        return 0;
    }

    // ================================================================
    // AGGREGATOR
    // ================================================================
    private sealed class Aggregator
    {
        private readonly Dictionary<string, int> _perInstrumentCount = new();
        private int _totalCount;

        // Chord name recognition
        private int _nullChordName;
        private int _unknownChordName;
        private readonly Dictionary<string, int> _chordNameCounts = new(StringComparer.Ordinal);

        // Harmonic function coverage
        private int _nullHarmonicFunction;
        private readonly Dictionary<string, int> _harmonicFunctionCounts = new(StringComparer.Ordinal);

        // Forte coverage
        private int _forteResolved;
        private readonly List<ForteFailure> _forteFailures = new();

        // Drop voicings
        private readonly Dictionary<string, int> _dropVoicingCounts = new(StringComparer.Ordinal);

        // Cross-instrument consistency: pitch-class mask -> per-instrument ChordName
        private readonly Dictionary<int, Dictionary<string, string?>> _pcMaskToInstrumentChord = new();

        // Cardinality distribution
        private readonly Dictionary<int, int> _cardinalityCounts = new();
        private int _twoNoteWithChordName;

        // Invariant failures
        private int _midiNotesMismatch;
        private int _nullPitchClassSet;
        private int _negativePhysicalLayout;
        private int _intervalSpreadInvariant;
        private readonly List<InvariantFailure> _invariantFailureSamples = new();

        // Analyzer exceptions
        private readonly List<AnalyzerException> _analyzerExceptions = new();

        // Issue samples for the "top 3 systemic issues" section
        private readonly List<VoicingSample> _nullChordSamples = new();
        private readonly List<VoicingSample> _forteFailureSamples = new();
        private readonly List<VoicingSample> _twoNoteChordSamples = new();
        private readonly List<VoicingSample> _unknownChordSamples = new();

        private string _currentInstrument = "";

        public void StartInstrument(string name, int expectedCount)
        {
            _currentInstrument = name;
            _perInstrumentCount[name] = expectedCount;
        }

        public void RecordAnalyzerException(string instrument, Voicing voicing, Exception ex)
        {
            if (_analyzerExceptions.Count < 20)
            {
                _analyzerExceptions.Add(new AnalyzerException(
                    instrument,
                    VoicingExtensions.GetPositionDiagram(voicing.Positions),
                    ex.GetType().Name,
                    Truncate(ex.Message, 200)));
            }
        }

        public void RecordVoicing(string instrument, Voicing voicing, MusicalVoicingAnalysis analysis)
        {
            _totalCount++;

            var chordId = analysis.ChordId;
            var chordName = chordId?.ChordName;
            var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);

            // --- B. Chord name recognition ---
            if (chordName == null)
            {
                _nullChordName++;
                if (_nullChordSamples.Count < 10)
                {
                    _nullChordSamples.Add(BuildSample(instrument, voicing, analysis, "ChordName is null"));
                }
            }
            else
            {
                if (string.Equals(chordName, "Unknown", StringComparison.Ordinal))
                {
                    _unknownChordName++;
                    if (_unknownChordSamples.Count < 10)
                    {
                        _unknownChordSamples.Add(BuildSample(instrument, voicing, analysis,
                            "ChordName == \"Unknown\""));
                    }
                }

                if (!_chordNameCounts.TryAdd(chordName, 1))
                {
                    _chordNameCounts[chordName]++;
                }
            }

            // --- C. Harmonic function coverage ---
            var harmonicFn = chordId?.HarmonicFunction;
            if (string.IsNullOrEmpty(harmonicFn))
            {
                _nullHarmonicFunction++;
            }
            else
            {
                if (!_harmonicFunctionCounts.TryAdd(harmonicFn, 1))
                {
                    _harmonicFunctionCounts[harmonicFn]++;
                }
            }

            // --- D. Forte coverage ---
            var pcs = analysis.PitchClassSet;
            if (pcs == null)
            {
                _nullPitchClassSet++;
                if (_invariantFailureSamples.Count < 20)
                {
                    _invariantFailureSamples.Add(new InvariantFailure(instrument, diagram,
                        "PitchClassSet is null"));
                }
            }
            else
            {
                if (ProgrammaticForteCatalog.TryGetForteNumber(pcs, out var forte))
                {
                    _forteResolved++;
                }
                else
                {
                    if (_forteFailures.Count < 50)
                    {
                        _forteFailures.Add(new ForteFailure(
                            instrument,
                            diagram,
                            string.Join(",", pcs.Select(p => p.Value.ToString())),
                            pcs.Cardinality.Value,
                            pcs.IntervalClassVector.ToString(),
                            pcs.PrimeForm == null
                                ? "PitchClassSet has no PrimeForm"
                                : "PrimeForm not in ProgrammaticForteCatalog"));
                    }

                    if (_forteFailureSamples.Count < 10)
                    {
                        _forteFailureSamples.Add(BuildSample(instrument, voicing, analysis,
                            "Forte lookup failed"));
                    }
                }
            }

            // --- E. Drop voicing distribution ---
            var drop = analysis.VoicingCharacteristics?.DropVoicing ?? "None";
            if (!_dropVoicingCounts.TryAdd(drop, 1))
            {
                _dropVoicingCounts[drop]++;
            }

            // --- F. Cross-instrument consistency ---
            var pcMask = BuildPitchClassMask(voicing.Notes);
            if (!_pcMaskToInstrumentChord.TryGetValue(pcMask, out var perInstrument))
            {
                perInstrument = new Dictionary<string, string?>();
                _pcMaskToInstrumentChord[pcMask] = perInstrument;
            }
            // Record first seen ChordName per instrument for this pc-mask.
            // If multiple voicings on the same instrument map to the same mask we
            // just keep the first observation (the dedup key already guarantees
            // near-uniqueness on the mask+shape tuple).
            if (!perInstrument.ContainsKey(instrument))
            {
                perInstrument[instrument] = chordName;
            }

            // --- G. Cardinality distribution ---
            var cardinality = pcs?.Cardinality.Value ?? voicing.Notes.Select(n => n.Value % 12).Distinct().Count();
            if (!_cardinalityCounts.TryAdd(cardinality, 1))
            {
                _cardinalityCounts[cardinality]++;
            }

            if (cardinality == 2 && chordName != null && !string.Equals(chordName, "Unknown", StringComparison.Ordinal))
            {
                _twoNoteWithChordName++;
                if (_twoNoteChordSamples.Count < 10)
                {
                    _twoNoteChordSamples.Add(BuildSample(instrument, voicing, analysis,
                        $"2-note voicing assigned ChordName='{chordName}'"));
                }
            }

            // --- H. Invariant sanity checks ---
            var playedCount = CountPlayedPositions(voicing.Positions);
            if (analysis.MidiNotes.Length != playedCount)
            {
                _midiNotesMismatch++;
                if (_invariantFailureSamples.Count < 20)
                {
                    _invariantFailureSamples.Add(new InvariantFailure(instrument, diagram,
                        $"MidiNotes.Length={analysis.MidiNotes.Length} but played-strings={playedCount}"));
                }
            }

            var physical = analysis.PhysicalLayout;
            if (physical != null)
            {
                // Note: FretPositions[i] == -1 is an INTENTIONAL sentinel for muted
                // strings (see VoicingPhysicalAnalyzer.ExtractPhysicalLayout line 48),
                // so we only flag the fields that are semantically non-negative.
                if (physical.MinFret < 0 || physical.MaxFret < 0
                    || AnyNegative(physical.StringsUsed)
                    || AnyNegative(physical.MutedStrings)
                    || AnyNegative(physical.OpenStrings))
                {
                    _negativePhysicalLayout++;
                    if (_invariantFailureSamples.Count < 20)
                    {
                        _invariantFailureSamples.Add(new InvariantFailure(instrument, diagram,
                            $"PhysicalLayout has negative semantic field(s) (MinFret={physical.MinFret}, MaxFret={physical.MaxFret})"));
                    }
                }
            }

            var chars = analysis.VoicingCharacteristics;
            if (chars != null && analysis.MidiNotes.Length >= 2 && chars.IntervalSpread <= 0)
            {
                _intervalSpreadInvariant++;
                if (_invariantFailureSamples.Count < 20)
                {
                    _invariantFailureSamples.Add(new InvariantFailure(instrument, diagram,
                        $"IntervalSpread={chars.IntervalSpread} for {analysis.MidiNotes.Length}-note voicing"));
                }
            }
        }

        public AuditReport BuildReport(TimeSpan runtime)
        {
            // Cross-instrument consistency computation
            var sharedSets = 0;
            var consistentSets = 0;
            var disagreements = new List<ConsistencyDisagreement>();
            foreach (var (mask, perInstrument) in _pcMaskToInstrumentChord)
            {
                if (perInstrument.Count < 2) continue;
                sharedSets++;

                var distinctNames = perInstrument.Values
                    .Select(v => v ?? "<null>")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (distinctNames.Length == 1)
                {
                    consistentSets++;
                }
                else if (disagreements.Count < 10)
                {
                    disagreements.Add(new ConsistencyDisagreement(
                        MaskToPcString(mask),
                        perInstrument.ToDictionary(kv => kv.Key, kv => kv.Value ?? "<null>")));
                }
            }

            var top20 = _chordNameCounts
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new TopEntry(kv.Key, kv.Value))
                .ToList();

            var voicingsPerSec = runtime.TotalSeconds > 0 ? _totalCount / runtime.TotalSeconds : 0.0;
            var peakWorkingSetMb = Process.GetCurrentProcess().PeakWorkingSet64 / 1024.0 / 1024.0;

            return new AuditReport(
                Timestamp: DateTime.UtcNow.ToString("O"),
                Corpus: new CorpusSection(
                    _perInstrumentCount.GetValueOrDefault("guitar"),
                    _perInstrumentCount.GetValueOrDefault("bass"),
                    _perInstrumentCount.GetValueOrDefault("ukulele"),
                    _totalCount),
                ChordRecognition: new ChordRecognitionSection(
                    new PercentCount(_nullChordName, Pct(_nullChordName, _totalCount)),
                    new PercentCount(_unknownChordName, Pct(_unknownChordName, _totalCount)),
                    _chordNameCounts.Count,
                    top20),
                HarmonicFunction: new HarmonicFunctionSection(
                    _nullHarmonicFunction,
                    Pct(_nullHarmonicFunction, _totalCount),
                    _harmonicFunctionCounts.OrderByDescending(kv => kv.Value)
                        .ToDictionary(kv => kv.Key, kv => kv.Value)),
                ForteCoverage: new ForteCoverageSection(
                    _forteResolved,
                    _totalCount,
                    Pct(_forteResolved, _totalCount),
                    _forteFailures.Take(10).ToList()),
                DropVoicingDistribution: _dropVoicingCounts
                    .OrderByDescending(kv => kv.Value)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                CrossInstrumentConsistency: new ConsistencySection(sharedSets, consistentSets, disagreements),
                CardinalityDistribution: _cardinalityCounts
                    .OrderBy(kv => kv.Key)
                    .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                TwoNoteWithChordName: _twoNoteWithChordName,
                InvariantFailures: new InvariantFailureSection(
                    _midiNotesMismatch,
                    _nullPitchClassSet,
                    _negativePhysicalLayout,
                    _intervalSpreadInvariant,
                    _invariantFailureSamples.Take(20).ToList()),
                AnalyzerExceptions: _analyzerExceptions,
                Performance: new PerformanceSection(
                    runtime.TotalSeconds,
                    voicingsPerSec,
                    peakWorkingSetMb),
                IssueSamples: new IssueSamplesSection(
                    _nullChordSamples,
                    _unknownChordSamples,
                    _forteFailureSamples,
                    _twoNoteChordSamples));
        }

        private static VoicingSample BuildSample(
            string instrument,
            Voicing voicing,
            MusicalVoicingAnalysis analysis,
            string issue) =>
            new(
                instrument,
                VoicingExtensions.GetPositionDiagram(voicing.Positions),
                analysis.MidiNotes,
                analysis.PitchClassSet == null
                    ? "<null>"
                    : "{" + string.Join(",", analysis.PitchClassSet.Select(p => p.Value)) + "}",
                analysis.ChordId?.ChordName,
                analysis.ChordId?.Quality,
                analysis.VoicingCharacteristics?.IntervalSpread ?? -1,
                issue);

        private static string MaskToPcString(int mask)
        {
            var pcs = new List<int>();
            for (var i = 0; i < 12; i++)
            {
                if ((mask & (1 << i)) != 0) pcs.Add(i);
            }

            return "{" + string.Join(",", pcs) + "}";
        }

        private static double Pct(int num, int denom) =>
            denom == 0 ? 0 : Math.Round(100.0 * num / denom, 3);

        private static bool AnyNegative(int[] arr)
        {
            foreach (var v in arr)
            {
                if (v < 0) return true;
            }

            return false;
        }

        private static int CountPlayedPositions(Position[] positions)
        {
            var count = 0;
            foreach (var p in positions)
            {
                if (p is Position.Played) count++;
            }

            return count;
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "...";
    }

    // ================================================================
    // DEDUP HELPERS — verbatim copy of FretboardVoicingsCLI.Program helpers
    // (see Demos/Music Theory/FretboardVoicingsCLI/Program.cs
    // RunExportEmbeddingsAsync). Kept identical so the corpus counts
    // match the CLI exactly.
    // ================================================================
    private readonly record struct DedupCandidate(Voicing Voicing, int Cost);

    private static string BuildRelativeFretSignature(RelativeFret[] relativeFrets)
    {
        var sb = new StringBuilder(relativeFrets.Length * 3);
        for (var i = 0; i < relativeFrets.Length; i++)
        {
            if (i > 0) sb.Append('-');
            var rf = relativeFrets[i];
            if (rf.Equals(RelativeFret.Min)) sb.Append('x');
            else sb.Append(rf.Value);
        }

        return sb.ToString();
    }

    private static int BuildPitchClassMask(MidiNote[] notes)
    {
        var mask = 0;
        foreach (var n in notes)
        {
            mask |= 1 << (n.Value % 12);
        }

        return mask;
    }

    private static int CheapPlayabilityCost(Position[] positions)
    {
        var minPlayedFret = int.MaxValue;
        var maxPlayedFret = 0;
        var mutedCount = 0;

        foreach (var p in positions)
        {
            switch (p)
            {
                case Position.Muted:
                    mutedCount++;
                    break;
                case Position.Played played:
                    var fret = played.Location.Fret.Value;
                    if (fret > 0)
                    {
                        if (fret < minPlayedFret) minPlayedFret = fret;
                        if (fret > maxPlayedFret) maxPlayedFret = fret;
                    }

                    break;
            }
        }

        if (minPlayedFret == int.MaxValue) minPlayedFret = 0;
        return minPlayedFret * 100 + mutedCount * 10 + (maxPlayedFret - minPlayedFret);
    }

    // ================================================================
    // REPOSITORY ROOT RESOLUTION
    // ================================================================
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "AllProjects.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Fall back to current directory.
        return Directory.GetCurrentDirectory();
    }

    // ================================================================
    // STDOUT SUMMARY + JSON WRITER
    // ================================================================
    private static void PrintStdoutSummary(AuditReport report, string outputPath)
    {
        Console.WriteLine("=====================================================================");
        Console.WriteLine("  VOICING ANALYSIS AUDIT — SUMMARY");
        Console.WriteLine($"  Timestamp: {report.Timestamp}");
        Console.WriteLine("=====================================================================");
        Console.WriteLine();

        Console.WriteLine("A. Overall counts per instrument:");
        Console.WriteLine($"   guitar:   {report.Corpus.Guitar,10:N0}");
        Console.WriteLine($"   bass:     {report.Corpus.Bass,10:N0}");
        Console.WriteLine($"   ukulele:  {report.Corpus.Ukulele,10:N0}");
        Console.WriteLine($"   total:    {report.Corpus.Total,10:N0}");
        Console.WriteLine();

        Console.WriteLine("B. Chord name recognition:");
        Console.WriteLine($"   ChordName == null:       {report.ChordRecognition.NullChordName.Count,10:N0} ({report.ChordRecognition.NullChordName.Pct:N2}%)");
        Console.WriteLine($"   ChordName == \"Unknown\":  {report.ChordRecognition.UnknownChordName.Count,10:N0} ({report.ChordRecognition.UnknownChordName.Pct:N2}%)");
        Console.WriteLine($"   distinct ChordName values: {report.ChordRecognition.DistinctChordNames,8:N0}");
        Console.WriteLine("   top 20 ChordName + count:");
        foreach (var e in report.ChordRecognition.Top20)
        {
            Console.WriteLine($"     {Pad(e.Name, 30)} {e.Count,10:N0}");
        }

        Console.WriteLine();

        Console.WriteLine("C. Harmonic function coverage:");
        Console.WriteLine($"   HarmonicFunction null/empty: {report.HarmonicFunction.NullCount,10:N0} ({report.HarmonicFunction.Pct:N2}%)");
        Console.WriteLine("   distribution:");
        foreach (var (k, v) in report.HarmonicFunction.Distribution)
        {
            Console.WriteLine($"     {Pad(k, 30)} {v,10:N0}");
        }

        Console.WriteLine();

        Console.WriteLine("D. Forte coverage (critical invariant):");
        Console.WriteLine($"   PitchClassSet → Forte: {report.ForteCoverage.Resolved:N0}/{report.ForteCoverage.Total:N0} ({report.ForteCoverage.Pct:N3}%)");
        if (report.ForteCoverage.Failures.Count > 0)
        {
            Console.WriteLine($"   failures (sample {report.ForteCoverage.Failures.Count}):");
            foreach (var f in report.ForteCoverage.Failures)
            {
                Console.WriteLine($"     - {f.Instrument} diagram=\"{f.Diagram}\" pcs={{{f.PitchClasses}}} cardinality={f.Cardinality} icv={f.Icv} reason={f.Reason}");
            }
        }

        Console.WriteLine();

        Console.WriteLine("E. Drop voicing distribution:");
        foreach (var (k, v) in report.DropVoicingDistribution)
        {
            Console.WriteLine($"   {Pad(k, 20)} {v,10:N0}");
        }

        Console.WriteLine();

        Console.WriteLine("F. Cross-instrument consistency:");
        Console.WriteLine($"   pc-sets on >1 instrument:    {report.CrossInstrumentConsistency.SharedSets,10:N0}");
        Console.WriteLine($"   with consistent ChordName:   {report.CrossInstrumentConsistency.Consistent,10:N0}");
        Console.WriteLine(
            $"   consistency rate:            {(report.CrossInstrumentConsistency.SharedSets > 0 ? 100.0 * report.CrossInstrumentConsistency.Consistent / report.CrossInstrumentConsistency.SharedSets : 0):N2}%");
        if (report.CrossInstrumentConsistency.Disagreements.Count > 0)
        {
            Console.WriteLine("   disagreements (sample):");
            foreach (var d in report.CrossInstrumentConsistency.Disagreements)
            {
                var inst = string.Join("; ", d.ChordNameByInstrument.Select(kv => $"{kv.Key}={kv.Value}"));
                Console.WriteLine($"     - pc-set {d.PitchClasses}: {inst}");
            }
        }

        Console.WriteLine();

        Console.WriteLine("G. Cardinality distribution:");
        foreach (var (k, v) in report.CardinalityDistribution)
        {
            Console.WriteLine($"   {k}-note: {v,10:N0} ({Pct(v, report.Corpus.Total):N2}%)");
        }

        Console.WriteLine($"   2-note voicings with non-null ChordName: {report.TwoNoteWithChordName,10:N0}");
        Console.WriteLine();

        Console.WriteLine("H. Quality-field invariant failures:");
        Console.WriteLine($"   MidiNotes length ≠ played-strings count: {report.InvariantFailures.MidiNotesMismatch,10:N0}");
        Console.WriteLine($"   PitchClassSet is null:                    {report.InvariantFailures.NullPitchClassSet,10:N0}");
        Console.WriteLine($"   PhysicalLayout has negative field(s):     {report.InvariantFailures.NegativePhysicalLayout,10:N0}");
        Console.WriteLine($"   IntervalSpread <=0 for 2+ notes:          {report.InvariantFailures.IntervalSpreadInvariant,10:N0}");
        if (report.AnalyzerExceptions.Count > 0)
        {
            Console.WriteLine($"   Analyzer threw exceptions on:             {report.AnalyzerExceptions.Count,10:N0}");
        }

        Console.WriteLine();

        Console.WriteLine("I. Performance:");
        Console.WriteLine($"   total runtime:     {report.Performance.RuntimeSeconds:N2}s");
        Console.WriteLine($"   voicings/sec:      {report.Performance.VoicingsPerSec:N1}");
        Console.WriteLine($"   peak working set:  {report.Performance.PeakWorkingSetMb:N1} MB");
        Console.WriteLine();
        Console.WriteLine($"JSON report written to: {outputPath}");
    }

    private static void WriteJsonReport(string path, AuditReport report)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
    }

    private static string Pad(string s, int w) => s.Length >= w ? s : s.PadRight(w);
    private static double Pct(int num, int denom) => denom == 0 ? 0 : Math.Round(100.0 * num / denom, 3);

    // ================================================================
    // REPORT DTOs
    // ================================================================
    private sealed record AuditReport(
        string Timestamp,
        CorpusSection Corpus,
        ChordRecognitionSection ChordRecognition,
        HarmonicFunctionSection HarmonicFunction,
        ForteCoverageSection ForteCoverage,
        Dictionary<string, int> DropVoicingDistribution,
        ConsistencySection CrossInstrumentConsistency,
        Dictionary<string, int> CardinalityDistribution,
        int TwoNoteWithChordName,
        InvariantFailureSection InvariantFailures,
        List<AnalyzerException> AnalyzerExceptions,
        PerformanceSection Performance,
        IssueSamplesSection IssueSamples);

    private sealed record CorpusSection(int Guitar, int Bass, int Ukulele, int Total);

    private sealed record ChordRecognitionSection(
        PercentCount NullChordName,
        PercentCount UnknownChordName,
        int DistinctChordNames,
        List<TopEntry> Top20);

    private sealed record PercentCount(int Count, double Pct);

    private sealed record TopEntry(string Name, int Count);

    private sealed record HarmonicFunctionSection(
        int NullCount,
        double Pct,
        Dictionary<string, int> Distribution);

    private sealed record ForteCoverageSection(
        int Resolved,
        int Total,
        double Pct,
        List<ForteFailure> Failures);

    private sealed record ForteFailure(
        string Instrument,
        string Diagram,
        string PitchClasses,
        int Cardinality,
        string Icv,
        string Reason);

    private sealed record ConsistencySection(
        int SharedSets,
        int Consistent,
        List<ConsistencyDisagreement> Disagreements);

    private sealed record ConsistencyDisagreement(
        string PitchClasses,
        Dictionary<string, string> ChordNameByInstrument);

    private sealed record InvariantFailureSection(
        int MidiNotesMismatch,
        int NullPitchClassSet,
        int NegativePhysicalLayout,
        int IntervalSpreadInvariant,
        List<InvariantFailure> Samples);

    private sealed record InvariantFailure(string Instrument, string Diagram, string Description);

    private sealed record AnalyzerException(string Instrument, string Diagram, string ExceptionType, string Message);

    private sealed record PerformanceSection(
        double RuntimeSeconds,
        double VoicingsPerSec,
        double PeakWorkingSetMb);

    private sealed record IssueSamplesSection(
        List<VoicingSample> NullChordName,
        List<VoicingSample> UnknownChordName,
        List<VoicingSample> ForteFailure,
        List<VoicingSample> TwoNoteWithChordName);

    private sealed record VoicingSample(
        string Instrument,
        string Diagram,
        int[] MidiNotes,
        string PitchClasses,
        string? ChordName,
        string? Quality,
        int IntervalSpread,
        string Issue);
}
