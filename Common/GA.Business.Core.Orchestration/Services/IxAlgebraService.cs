namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Domain.Core.Theory.Atonal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed partial class IxAlgebraService(
    IConfiguration configuration,
    ILogger<IxAlgebraService> logger) : IIxAlgebraService
{
    private readonly string _revision = configuration["IX:Revision"] ?? "internal-ga";
    private readonly string _source = configuration["IX:Source"] ?? "ix-compatible";
    private readonly bool _preferExternal = configuration.GetValue("IX:External:Enabled", true);
    private readonly string? _executablePath = configuration["IX:External:ExecutablePath"];
    private readonly string? _workingDirectory = configuration["IX:External:WorkingDirectory"];
    private readonly int _timeoutSeconds = Math.Max(1, configuration.GetValue("IX:External:TimeoutSeconds", 10));
    private readonly string[] _arguments = configuration.GetSection("IX:External:Arguments").Get<string[]>() ?? [];

    public async Task<IxAlgebraAnswer?> TryAnswerAsync(string query, CancellationToken cancellationToken = default)
    {
        if (_preferExternal)
        {
            var external = await TryAnswerExternallyAsync(query, cancellationToken);
            if (external is not null)
            {
                return external;
            }
        }

        return TryAnswerInternally(query);
    }

    private async Task<IxAlgebraAnswer?> TryAnswerExternallyAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_executablePath))
        {
            return null;
        }

        if (!IsResolvableExecutable(_executablePath))
        {
            logger.LogDebug("IX external executable was configured but not found at {ExecutablePath}", _executablePath);
            return null;
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            using var process = new Process
            {
                StartInfo = BuildStartInfo(),
            };

            if (!process.Start())
            {
                logger.LogWarning("Failed to start IX external algebra process at {ExecutablePath}", _executablePath);
                return null;
            }

            var request = JsonSerializer.Serialize(new ExternalIxAlgebraRequest(query));
            await process.StandardInput.WriteAsync(request.AsMemory(), linkedCts.Token);
            await process.StandardInput.WriteLineAsync();
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "IX external algebra process exited with code {ExitCode}. stderr: {Stderr}",
                    process.ExitCode,
                    TrimForLog(stderr));
                return null;
            }

            var payload = ParseExternalResponse(stdout);
            if (payload is null)
            {
                logger.LogWarning("IX external algebra process returned unparseable output: {Stdout}", TrimForLog(stdout));
                return null;
            }

            if (string.IsNullOrWhiteSpace(payload.NaturalLanguageAnswer) ||
                string.IsNullOrWhiteSpace(payload.QueryType))
            {
                logger.LogWarning("IX external algebra process returned incomplete output: {Stdout}", TrimForLog(stdout));
                return null;
            }

            var facts = payload.Facts ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var source = string.IsNullOrWhiteSpace(payload.Source) ? _source : payload.Source;
            var revision = string.IsNullOrWhiteSpace(payload.Revision) ? _revision : payload.Revision;

            return new IxAlgebraAnswer(
                payload.NaturalLanguageAnswer,
                payload.QueryType,
                facts,
                new GroundingMetadata(source, revision, payload.QueryType, facts));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "IX external algebra process timed out after {TimeoutSeconds}s at {ExecutablePath}",
                _timeoutSeconds,
                _executablePath);
            return null;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "IX external algebra process failed. Falling back to internal GA algebra.");
            return null;
        }
    }

    private ProcessStartInfo BuildStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath!,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(_workingDirectory))
        {
            startInfo.WorkingDirectory = _workingDirectory;
        }

        foreach (var argument in _arguments.Where(argument => !string.IsNullOrWhiteSpace(argument)))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private IxAlgebraAnswer? TryAnswerInternally(string query)
    {
        var normalized = query.ToLowerInvariant();

        // Reverse Forte-label lookup: "what is Forte number 4-Z29" → prime form.
        // Must run BEFORE set extraction, otherwise "4-Z29" gets mis-parsed as a
        // pitch-class set ("29" → {2,9}) and answered as the wrong question.
        if (normalized.Contains("forte"))
        {
            var reverse = TryAnswerForteLookup(query);
            if (reverse is not null)
            {
                return reverse;
            }
        }

        var sets = ExtractPitchClassSets(query);
        if (sets.Count == 0)
        {
            return null;
        }

        if (normalized.Contains("z-related") || normalized.Contains("z relation") || normalized.Contains("z-pair"))
        {
            return AnswerZRelation(sets);
        }

        if (normalized.Contains("prime form"))
        {
            return AnswerPrimeForm(sets[0]);
        }

        if (normalized.Contains("interval class vector") || Regex.IsMatch(normalized, @"\bicv\b"))
        {
            return AnswerIntervalClassVector(sets[0]);
        }

        if (normalized.Contains("forte"))
        {
            return AnswerForte(sets[0]);
        }

        if (normalized.Contains("set class"))
        {
            return AnswerSetClassSummary(sets[0]);
        }

        return null;
    }

    private IxAlgebraAnswer AnswerPrimeForm(PitchClassSet set)
    {
        var prime = set.PrimeForm ?? set;
        var facts = new Dictionary<string, string>
        {
            ["input"] = FormatSet(set),
            ["primeForm"] = FormatSet(prime)
        };

        return CreateAnswer(
            $"The prime form of {FormatSet(set)} is {FormatSet(prime)}.",
            "prime-form",
            facts);
    }

    private IxAlgebraAnswer AnswerIntervalClassVector(PitchClassSet set)
    {
        var facts = new Dictionary<string, string>
        {
            ["input"] = FormatSet(set),
            ["intervalClassVector"] = set.IntervalClassVector.ToString(),
            ["intervalClassVectorId"] = set.IntervalClassVector.Id.Value.ToString()
        };

        return CreateAnswer(
            $"The interval-class vector for {FormatSet(set)} is {set.IntervalClassVector}.",
            "interval-class-vector",
            facts);
    }

    private IxAlgebraAnswer AnswerForte(PitchClassSet set)
    {
        var prime = set.PrimeForm ?? set;
        var forte = ForteCatalog.GetForteNumber(prime);
        var forteText = forte?.ToString() ?? "unavailable";
        var facts = new Dictionary<string, string>
        {
            ["input"] = FormatSet(set),
            ["primeForm"] = FormatSet(prime),
            ["forte"] = forteText
        };

        return CreateAnswer(
            forte is not null
                ? $"The Forte label for {FormatSet(set)} is {forteText}."
                : $"I could compute the prime form for {FormatSet(set)}, but no Forte label was available.",
            "forte",
            facts);
    }

    // Reverse direction of AnswerForte: a canonical Forte label (e.g. "4-Z29")
    // → the set class it names, using Allen Forte's 1973 numbering
    // (CanonicalForteCatalog) — NOT GA's internal Rahn ordering, because a user
    // typing "4-Z29" means the canonical [0,1,3,7], not Rahn index 29. Returns
    // null when the query carries no Forte-label token or the label isn't in the
    // canonical catalog, so the caller falls through to the set-extraction path.
    private IxAlgebraAnswer? TryAnswerForteLookup(string query)
    {
        var match = ForteLabelRegex().Match(query);
        if (!match.Success || !CanonicalForteCatalog.TryGetPrimeForm(match.Value, out var primeForm))
        {
            return null;
        }

        var facts = new Dictionary<string, string>
        {
            ["forte"] = match.Value,
            ["primeForm"] = FormatSet(primeForm),
            ["intervalClassVector"] = primeForm.IntervalClassVector.ToString(),
            ["catalog"] = "forte-1973"
        };

        return CreateAnswer(
            $"Forte {match.Value} is the set class with prime form {FormatSet(primeForm)} " +
            $"(interval-class vector {primeForm.IntervalClassVector}).",
            "forte-lookup",
            facts);
    }

    private IxAlgebraAnswer AnswerSetClassSummary(PitchClassSet set)
    {
        var setClass = new SetClass(set);
        var forte = ForteCatalog.GetForteNumber(setClass.PrimeForm)?.ToString() ?? "unavailable";
        var facts = new Dictionary<string, string>
        {
            ["input"] = FormatSet(set),
            ["primeForm"] = FormatSet(setClass.PrimeForm),
            ["intervalClassVector"] = setClass.IntervalClassVector.ToString(),
            ["forte"] = forte
        };

        var answer =
            $"Set-class summary for {FormatSet(set)}: prime form {FormatSet(setClass.PrimeForm)}, ICV {setClass.IntervalClassVector}, Forte {forte}.";

        return CreateAnswer(answer, "set-class-summary", facts);
    }

    private IxAlgebraAnswer AnswerZRelation(IReadOnlyList<PitchClassSet> sets)
    {
        if (sets.Count >= 2)
        {
            var left = sets[0].PrimeForm ?? sets[0];
            var right = sets[1].PrimeForm ?? sets[1];
            var isZRelated = left.IntervalClassVector == right.IntervalClassVector && left.Id != right.Id;
            var facts = new Dictionary<string, string>
            {
                ["left"] = FormatSet(left),
                ["right"] = FormatSet(right),
                ["leftIcv"] = left.IntervalClassVector.ToString(),
                ["rightIcv"] = right.IntervalClassVector.ToString(),
                ["zRelated"] = isZRelated.ToString()
            };

            var answer = isZRelated
                ? $"{FormatSet(left)} and {FormatSet(right)} are Z-related: they share ICV {left.IntervalClassVector} but have different prime forms."
                : $"{FormatSet(left)} and {FormatSet(right)} are not Z-related.";

            return CreateAnswer(answer, "z-relation", facts);
        }

        var set = sets[0].PrimeForm ?? sets[0];
        var partner = SetClass.Items
            .Select(sc => sc.PrimeForm)
            .FirstOrDefault(candidate => candidate.IntervalClassVector == set.IntervalClassVector && candidate.Id != set.Id);

        var hasPartner = partner is not null;
        var singleFacts = new Dictionary<string, string>
        {
            ["input"] = FormatSet(set),
            ["intervalClassVector"] = set.IntervalClassVector.ToString(),
            ["isZRelated"] = hasPartner.ToString()
        };

        if (partner is not null)
        {
            singleFacts["partner"] = FormatSet(partner);
        }

        var singleAnswer = partner is not null
            ? $"{FormatSet(set)} is Z-related. One partner is {FormatSet(partner)}, and both share ICV {set.IntervalClassVector}."
            : $"{FormatSet(set)} is not Z-related.";

        return CreateAnswer(singleAnswer, "z-relation", singleFacts);
    }

    private IxAlgebraAnswer CreateAnswer(
        string answer,
        string queryType,
        IReadOnlyDictionary<string, string> facts) =>
        new(
            answer,
            queryType,
            facts,
            new GroundingMetadata(_source, _revision, queryType, facts));

    private static ExternalIxAlgebraResponse? ParseExternalResponse(string stdout)
    {
        var candidate = stdout.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var firstJsonStart = candidate.IndexOf('{');
        if (firstJsonStart > 0)
        {
            candidate = candidate[firstJsonStart..];
        }

        return JsonSerializer.Deserialize<ExternalIxAlgebraResponse>(
            candidate,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    private static List<PitchClassSet> ExtractPitchClassSets(string query)
    {
        var results = new List<PitchClassSet>();

        foreach (Match match in BracketedSetRegex().Matches(query))
        {
            var candidate = string.Concat(TokenRegex().Matches(match.Value).Select(m => NormalizeToken(m.Value)));
            if (TryParsePitchClassSet(candidate, out var set))
            {
                results.Add(set);
            }
        }

        if (results.Count > 0)
        {
            return results;
        }

        // Comma/space-separated bare pitch-class lists WITHOUT brackets, e.g.
        // "0,1,4,6" or "0 1 4 6". Without this, the bracketed pass misses and
        // the contiguous pass below splits "0,1,4,6" into single rejected
        // digits, so a perfectly well-formed set-theory question extracts
        // nothing and the deterministic engine never runs (it fell through to
        // the LLM and timed out). Require >= 3 tokens so prose like "bars 2, 4"
        // or an interval "0, 7" can't be mistaken for a pc-set. This is data
        // extraction, not query routing.
        foreach (Match match in SeparatedSetRegex().Matches(query))
        {
            var candidate = string.Concat(TokenRegex().Matches(match.Value).Select(m => NormalizeToken(m.Value)));
            if (TryParsePitchClassSet(candidate, out var set))
            {
                results.Add(set);
            }
        }

        if (results.Count > 0)
        {
            return results;
        }

        foreach (Match match in CompactSetRegex().Matches(query))
        {
            if (TryParsePitchClassSet(match.Value, out var set))
            {
                results.Add(set);
            }
        }

        return results;
    }

    private static bool TryParsePitchClassSet(string candidate, out PitchClassSet set)
    {
        set = null!;
        var normalized = candidate.Trim().ToUpperInvariant();
        if (normalized.Length < 2)
        {
            return false;
        }

        return PitchClassSet.TryParse(normalized, null, out set);
    }

    private static string NormalizeToken(string token) =>
        token.Trim().ToUpperInvariant() switch
        {
            "10" => "T",
            "11" => "E",
            var value => value
        };

    private static string FormatSet(PitchClassSet set) =>
        "[" + string.Join(",", set.OrderBy(pc => pc.Value).Select(pc => pc.Value)) + "]";

    private static string TrimForLog(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        const int maxLength = 500;
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static bool IsResolvableExecutable(string executablePath)
    {
        if (Path.IsPathRooted(executablePath) || executablePath.Contains(Path.DirectorySeparatorChar) || executablePath.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(executablePath);
        }

        return true;
    }

    [GeneratedRegex(@"[\[\{][^\]\}]+[\]\}]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedSetRegex();

    // A Forte label: cardinality-index with an optional Z marker on the index,
    // e.g. "4-Z29", "4-z15", "3-11". Used for reverse lookup (label → set).
    [GeneratedRegex(@"\b\d{1,2}-Z?\d{1,2}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForteLabelRegex();

    // A run of >= 3 pitch-class tokens separated by commas/spaces, e.g.
    // "0,1,4,6" or "0 1 4 6 9". Used only to pull a set out of a query the
    // router already classified as set-theory; the >= 3 floor avoids matching
    // ordinary number pairs in prose.
    [GeneratedRegex(@"\b(?:10|11|[0-9TEte])(?:\s*,\s*|\s+)(?:10|11|[0-9TEte])(?:(?:\s*,\s*|\s+)(?:10|11|[0-9TEte]))+\b", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatedSetRegex();

    [GeneratedRegex(@"\b(?:10|11|[0-9]|[TEAB])+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompactSetRegex();

    [GeneratedRegex(@"10|11|[0-9]|[TEAB]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private sealed record ExternalIxAlgebraRequest([property: JsonPropertyName("query")] string Query);

    private sealed record ExternalIxAlgebraResponse(
        string NaturalLanguageAnswer,
        string QueryType,
        Dictionary<string, string>? Facts,
        string? Source,
        string? Revision);
}
