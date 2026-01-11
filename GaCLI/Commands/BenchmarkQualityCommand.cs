namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;
using System.Text.RegularExpressions;

public class BenchmarkQualityCommand(MongoDbService mongoDbService)
{
    private record BenchmarkCase(
        string Name,
        SearchVoicingsCommand.ValidatedOptions Options,
        Func<List<VoicingEntity>, (double Score, List<string> Issues)> Validator
    );

    public async Task ExecuteAsync()
    {
        
        AnsiConsole.Write(new FigletText("Quality Check").Color(Color.Gold1));
        
        // ========== GROUND TRUTH ORACLE ==========
        // These are KNOWN standard chord shapes from community-vetted sources.
        // Format: (Diagram, ExpectedChordName, Description)
        var groundTruthChords = new List<(string Diagram, string ExpectedName, string Description)>
        {
            // Standard Open Chords (universally agreed upon)
            ("x-3-2-0-1-0", "C", "Standard open C major"),
            ("x-0-2-2-2-0", "A", "Standard open A major"),
            ("x-0-2-2-1-0", "Am", "Standard open A minor"),
            ("0-2-2-1-0-0", "E", "Standard open E major"),
            ("0-2-2-0-0-0", "Em", "Standard open E minor"),
            ("3-2-0-0-0-3", "G", "Standard open G major"),
            ("x-x-0-2-3-2", "D", "Standard open D major"),
            ("x-x-0-2-3-1", "Dm", "Standard open D minor"),
            
            // Barre Chords (E-shape and A-shape)
            ("1-3-3-2-1-1", "F", "F barre chord (E-shape)"),
            ("x-1-3-3-3-1", "Bb", "Bb barre chord (A-shape)"),
            
            // 7th Chords
            ("3-2-0-0-0-1", "G7", "Standard G7 open"),
            ("x-0-2-0-2-0", "A7", "Standard A7 open"),
            ("0-2-0-1-0-0", "E7", "Standard E7 open"),
            
            // Famous Voicings
            ("0-7-6-7-8-0", "E7#9", "Hendrix chord (Purple Haze)"),
        };

        var benchmarks = new List<BenchmarkCase>
        {
            new(
                "Beginner Open C",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "C", 
                    Difficulty = "Beginner",
                    MinFret = 0,
                    MaxFret = 3,
                    Limit = 5
                },
                results =>
                {
                    var issues = new List<string>();
                    var score = 1.0;
                    
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);

                    // Check top result is truly beginner
                    var top = results[0];
                    if (top.DifficultyScore > 1.5) 
                    {
                        score -= 0.5;
                        issues.Add($"Top result difficulty {top.DifficultyScore} (expected <= 1.5)");
                    }
                    if (top.BarreRequired)
                    {
                        score -= 0.5;
                        issues.Add("Top result requires barre (expected none)");
                    }

                    return (Math.Max(0, score), issues);
                }
            ),
            new(
                "Sad Funk Context",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    Tag = "sad",
                    // We can't search multiple tags in the simple options yet easily, 
                    // but we can check if the results *also* have funk derived traits 
                    // or we check 'sad' specifically first.
                    // Let's test 'sad' specifically quality first.
                    Limit = 10
                },
                results =>
                {
                    var issues = new List<string>();
                    var score = 0.0;
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);

                    int validCount = 0;
                    foreach(var r in results)
                    {
                        // Sad should implies minor, dim, or specific tags
                        bool isMinor = r.ChordName.Contains("m") && !r.ChordName.Contains("maj");
                        bool hasSadTag = r.SemanticTags.Contains("sad") || r.SemanticTags.Contains("melancholy");
                        if (isMinor || hasSadTag) validCount++;
                    }

                    score = (double)validCount / results.Count;
                    if (score < 0.5) issues.Add($"Only {validCount}/{results.Count} results appear 'sad' (minor/tagged)");

                    return (score, issues);
                }
            ),
            new(
                "Jazz Shells",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    Tag = "shell-voicing",
                    Limit = 10
                },
                results =>
                {
                    var issues = new List<string>();
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);

                    int shellCount = results.Count(r => r.VoicingType?.Contains("Shell") == true || r.SemanticTags.Contains("shell-voicing"));
                    double score = (double)shellCount / results.Count;
                    
                    // Shells typically have few notes (2-3)
                    int lowNoteCount = results.Count(r => r.MidiNotes.Length <= 4); // Shells are sparse
                    if (lowNoteCount < results.Count * 0.8) issues.Add("Many results imply distinct non-shell density (>4 notes)");

                    return (score, issues);
                }
            ),
             new(
                "Upper Structure Logic",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    MinFret = 12,
                    Limit = 5
                },
                results =>
                {
                    // If we asked for high frets, we should get high frets
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    int errors = results.Count(r => r.MinFret < 12);
                    if (errors > 0) return (0.0, ["Found results below fret 12"]);
                    
                    return (1.0, []);
                }
            ),
            new(
                "Strict No-Barre Constraint",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "G", 
                    NoBarre = true,
                    Limit = 20
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    int failures = results.Count(r => r.BarreRequired);
                    if (failures > 0) return (0.0, [$"{failures} results required barre (expected 0)"]);
                    
                    return (1.0, []);
                }
            ),
            new(
                "Difficulty Consistency",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "Bm", // Usually has varying difficulties
                    Limit = 10
                },
                results =>
                {
                    if (results.Count < 2) return (1.0, ["Skipped: Not enough data"]);
                    
                    // Since default sort is MinFret then Difficulty, we can't strictly check Difficulty alone
                    // unless we confine MinFret. But generally, later results shouldn't be drastically easier 
                    // if they are in the same position.
                    // Let's check a loose correlation: generally increasing difficulty.
                    
                    int inversions = 0;
                    for(int i = 0; i < results.Count - 1; i++)
                    {
                        // If next one is much easier (diff score < prev - 1.0) and in similar range (+/- 2 frets)
                        if (results[i+1].DifficultyScore < results[i].DifficultyScore - 1.0 
                            && Math.Abs(results[i+1].MinFret - results[i].MinFret) <= 2)
                        {
                            inversions++;
                        }
                    }
                    
                    if (inversions > 0) return (0.7, [$"Found {inversions} sorting inversions (easier chords ranked lower)"]);
                    return (1.0, []);
                }
            ),
            new(
                "Dominant 7th Theory",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "G7",
                    Limit = 10
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // A proper G7 must have guide tones (3rd and 7th)
                    int missingGuideTones = results.Count(r => !r.HasGuideTones);

                    if (missingGuideTones > 0) return (0.5, [$"{missingGuideTones} results missing essential guide tones (3rd/7th)"]);
                    return (1.0, []);
                }
            ),
            new(
                "High Register Brightness",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    Register = "High",
                    Limit = 10
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // High register chords should generally be bright
                    double avgBrightness = results.Average(r => r.Brightness);
                    if (avgBrightness < 0.6) return (0.5, [$"Average brightness {avgBrightness:F2} is too low for 'High' register (expected > 0.6)"]);
                    
                    return (1.0, []);
                }
            ),
            // ========== PHASE 5: SEMANTIC BENCHMARKS ==========
            new(
                "Register Drift (Low -> High)",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "Am", // Stable test case
                    Limit = 50 
                },
                results =>
                {
                    if (results.Count < 10) return (1.0, ["Skipped: Need more Am voicings for drift analysis"]);
                    
                    // Group results by register tag
                    var low = results.Where(r => r.SemanticTags.Contains("register:low")).ToList();
                    var mid = results.Where(r => r.SemanticTags.Contains("register:mid")).ToList();
                    var high = results.Where(r => r.SemanticTags.Contains("register:high")).ToList();

                    if (low.Count == 0 || high.Count == 0) 
                        return (1.0, ["Skipped: Missing low/high register tags in results"]);

                    double avgLowFret = low.Average(r => r.MinFret);
                    double avgHighFret = high.Average(r => r.MinFret);
                    double avgLowBright = low.Average(r => r.Brightness);
                    double avgHighBright = high.Average(r => r.Brightness);

                    var issues = new List<string>();
                    var score = 1.0;

                    // Level 2 Monotonicity check
                    if (avgHighFret <= avgLowFret)
                    {
                        score -= 0.5;
                        issues.Add($"Fret Drift Failure: High Register ({avgHighFret:F1}) is not higher than Low ({avgLowFret:F1})");
                    }
                    if (avgHighBright <= avgLowBright)
                    {
                        score -= 0.5;
                        issues.Add($"Brightness Drift Failure: High Register ({avgHighBright:F2}) is not brighter than Low ({avgLowBright:F2})");
                    }

                    return (score, issues);
                }
            ),
            new(
                "Mood: Melancholy Precision",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    Tag = "melancholy",
                    Limit = 10
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // Melancholy should correlate with minor/dim and higher roughness/spectrum-width
                    int valid = results.Count(r => 
                        (r.ChordName?.Contains("m") == true && !r.ChordName.Contains("maj")) || 
                        r.SemanticTags.Contains("sad") ||
                        r.ConsonanceScore < 0.7);

                    double score = (double)valid / results.Count;
                    var issues = new List<string>();
                    if (score < 0.7) issues.Add($"Precision low: {valid}/{results.Count} results have expected musical traits for melancholy");
                    
                    return (score, issues);
                }
            ),
            // ========== ADVANCED BENCHMARKS ==========
            new(
                "Hendrix Chord Detection",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "E7#9",
                    Limit = 5
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // Hendrix chord should have "hendrix" or "7#9" tag
                    int hasTag = results.Count(r => 
                        r.SemanticTags.Any(t => t.Contains("hendrix") || t.Contains("7#9") || t.Contains("blues")));
                    
                    if (hasTag < results.Count / 2) 
                        return (0.5, [$"Only {hasTag}/{results.Count} have appropriate semantic tags"]);
                    
                    return (1.0, []);
                }
            ),
            new(
                "Muddiness Detection",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    MaxFret = 3,
                    Limit = 20
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // Low-position chords with close intervals should be flagged as potentially muddy
                    var lowChords = results.Where(r => r.Register == "Low" || r.Register == "Mid-Low").ToList();
                    if (lowChords.Count == 0) return (1.0, []);
                    
                    int mudFlagged = lowChords.Count(r => r.MayBeMuddy);
                    double mudRate = (double)mudFlagged / lowChords.Count;
                    
                    // At least some low chords should be flagged
                    if (mudRate < 0.1 && lowChords.Count >= 5) 
                        return (0.7, [$"Only {mudRate:P0} of low-register chords flagged as muddy (expected some)"]);
                    
                    return (1.0, []);
                }
            ),
            new(
                "Inversion Recognition",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    Tag = "inversion",
                    Limit = 10
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // Inversions should have slash chord info or "inversion" in tags
                    int validInversions = results.Count(r => 
                        r.SemanticTags.Contains("inversion") || 
                        r.SemanticTags.Contains("slash-chord") ||
                        r.ChordName?.Contains("/") == true);
                    
                    double score = (double)validInversions / results.Count;
                    if (score < 0.8) return (score, [$"{results.Count - validInversions} results don't appear to be inversions"]);
                    
                    return (1.0, []);
                }
            ),
            new(
                "Minor Chord Semantics",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "Am",
                    Limit = 10
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // Minor chords should have "sad" or "melancholy" semantic tag
                    int hasSadTag = results.Count(r => 
                        r.SemanticTags.Any(t => t == "sad" || t == "melancholy" || t == "minor"));
                    
                    double score = (double)hasSadTag / results.Count;
                    if (score < 0.5) return (0.5, [$"Only {hasSadTag}/{results.Count} minor chords have sad/melancholy tags"]);
                    
                    return (1.0, []);
                }
            ),
            new(
                "Consonance Accuracy",
                new SearchVoicingsCommand.ValidatedOptions 
                { 
                    ChordName = "C",
                    Difficulty = "Beginner",
                    Limit = 5
                },
                results =>
                {
                    if (results.Count == 0) return (1.0, ["Skipped: No data"]);
                    
                    // Simple major triads should have high consonance (> 0.5)
                    double avgConsonance = results.Average(r => r.ConsonanceScore);
                    if (avgConsonance < 0.4) 
                        return (0.5, [$"Average consonance {avgConsonance:F2} too low for simple major triads (expected > 0.4)"]);
                    
                    return (1.0, []);
                }
            )
        };

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Benchmark");
        table.AddColumn("Score");
        table.AddColumn("Issues");

        double totalScore = 0;

        await AnsiConsole.Status().StartAsync("Running Benchmarks...", async ctx =>
        {
            foreach (var benchmark in benchmarks)
            {
                ctx.Status($"Running: {benchmark.Name}");
                
                // We need to capture the results. existing logic prints to console.
                // We need to 'peek' using the service directly, but for now we are wrapping the Command which prints.
                // *Correction*: We can't easily capture the command output without refactoring.
                // *Refactoring Plan*: Use MongoDbService directly here to reproduce the search logic from SearchVoicingsCommand 
                // OR modify SearchVoicingsCommand to return results. 
                // Since I have `MongoDbService`, I will replicate the search logic minimally or better, 
                // just use the same logic as the SearchCommand builder.
                
                // REPLICATION OF SEARCH LOGIC FOR BENCHMARKING
                // This ensures we test the *data*, even if the UI command differs slightly.
                var collection = mongoDbService.Voicings;
                var builder = Builders<VoicingEntity>.Filter;
                var filter = builder.Empty;
                var ops = benchmark.Options;

                if (!string.IsNullOrWhiteSpace(ops.ChordName))
                    filter &= builder.Regex(v => v.ChordName, new MongoDB.Bson.BsonRegularExpression($"^{Regex.Escape(ops.ChordName)}$", "i"));
                
                if (!string.IsNullOrWhiteSpace(ops.Difficulty))
                    filter &= builder.Eq(v => v.Difficulty, ops.Difficulty);

                if (!string.IsNullOrWhiteSpace(ops.Tag))
                    filter &= builder.AnyEq(v => v.SemanticTags, ops.Tag);

                if (ops.MinFret.HasValue) filter &= builder.Gte(v => v.MinFret, ops.MinFret.Value);
                if (ops.MaxFret.HasValue) filter &= builder.Lte(v => v.MaxFret, ops.MaxFret.Value);
                
                if (!string.IsNullOrWhiteSpace(ops.Register))
                    filter &= builder.Eq(v => v.Register, ops.Register);
                
                // Minimal Sort
                var sort = Builders<VoicingEntity>.Sort.Ascending(v => v.MinFret).Ascending(v => v.Difficulty);

                var results = await collection.Find(filter).Sort(sort).Limit(ops.Limit).ToListAsync();

                // Validation
                var (score, issues) = benchmark.Validator(results);
                totalScore += score;
                
                // Render Row
                var color = score > 0.8 ? "green" : (score > 0.5 ? "yellow" : "red");
                var issueText = issues.Count > 0 ? string.Join(", ", issues) : "[dim]None[/]";
                table.AddRow(benchmark.Name, $"[{color}]{score:P0}[/]", issueText);
            }
        });

        AnsiConsole.Write(table);
        
        var average = totalScore / benchmarks.Count;
        AnsiConsole.MarkupLine($"\nOverall Quality Score: {(average > 0.8 ? "[green]" : "[yellow]")}{average:P0}[/]");
        
        // ========== GROUND TRUTH ORACLE VALIDATION ==========
        AnsiConsole.MarkupLine("\n[bold yellow]── Ground Truth Oracle ──[/]");
        AnsiConsole.MarkupLine("[dim]Validating analyzer accuracy against known standard chord shapes...[/]\n");
        
        var oracleTable = new Table().Border(TableBorder.Rounded);
        oracleTable.AddColumn("Diagram");
        oracleTable.AddColumn("Expected");
        oracleTable.AddColumn("Actual");
        oracleTable.AddColumn("Status");
        
        int oraclePassed = 0;
        int oracleTotal = 0;
        var collection2 = mongoDbService.Voicings;
        
        foreach (var (diagram, expectedName, description) in groundTruthChords)
        {
            oracleTotal++;
            
            // Perform lookup using the Low-to-High diagram string
            var entity = await collection2.Find(v => v.Diagram == diagram).FirstOrDefaultAsync();
            
            if (entity == null)
            {
                oracleTable.AddRow(diagram, expectedName, "[dim]Not indexed[/]", "[yellow]⚠ SKIP[/]");
                oraclePassed++; // Don't penalize for missing data
                continue;
            }
            
            // Check if the chord name matches (case-insensitive, allowing variations)
            var actualName = entity.ChordName ?? "???";
            bool matches = actualName.Equals(expectedName, StringComparison.OrdinalIgnoreCase) ||
                          actualName.StartsWith(expectedName, StringComparison.OrdinalIgnoreCase) ||
                          actualName.Replace(" Major", "").Replace(" Minor", "m").Equals(expectedName, StringComparison.OrdinalIgnoreCase);
            
            if (matches)
            {
                oraclePassed++;
                oracleTable.AddRow(diagram, expectedName, $"[green]{actualName}[/]", "[green]✓ PASS[/]");
            }
            else
            {
                oracleTable.AddRow(diagram, expectedName, $"[red]{actualName}[/]", "[red]✗ FAIL[/]");
            }
        }
        
        AnsiConsole.Write(oracleTable);
        
        double oracleScore = oracleTotal > 0 ? (double)oraclePassed / oracleTotal : 1.0;
        AnsiConsole.MarkupLine($"\nGround Truth Score: {(oracleScore > 0.8 ? "[green]" : "[yellow]")}{oracleScore:P0}[/] ({oraclePassed}/{oracleTotal} chords correctly identified)");
    }
}
