namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using GA.Business.DSL.Closures;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using ModelContextProtocol.Server;
using GaClosure         = GA.Business.DSL.Closures.GaClosureRegistry.GaClosure;
using GaClosureCategory = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureCategory;
using GaClosureRegistry = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;
using GaError           = GA.Business.DSL.Closures.GaAsync.GaError;

/// <summary>
/// MCP bridge from the chatbot's in-process tool registry to the F# closure
/// registry (<see cref="GaClosureRegistry"/>) — surfaces ~40 domain
/// closures as one composable tool surface instead of demanding 40 keyhole
/// MCP tools. Contract: <c>docs/contracts/2026-05-06-ga-dsl-eval-contract.md</c>
/// (v0.1, NOT frozen).
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. Same template as the prior
/// six MCP tool classes: length-guarded inputs, sanitized echo via
/// <see cref="McpEchoSanitizer"/>, structured result with Error-branch invariant.
///
/// Visibility: only <see cref="GaClosureCategory.Domain"/> closures are
/// exposed. Pipeline / Io / Agent categories touch the network, filesystem,
/// and other agents — not safe to put on the LLM-facing surface for v0.1.
/// </remarks>
[McpServerToolType]
public sealed class DslEvalMcpTools
{
    /// <summary>
    /// Conservative cap on closure-name input. Real closure names are
    /// dotted-namespaced like <c>"domain.transposeChord"</c>; 64 admits
    /// every realistic name without inviting MB-sized abuse.
    /// </summary>
    private const int MaxClosureNameLength = 64;

    /// <summary>
    /// Per-invocation timeout in seconds. Domain closures are expected to
    /// complete well under this; the cap exists to bound the worst case.
    /// Override via the <c>MCP_DSL_EVAL_TIMEOUT_SECONDS</c> environment
    /// variable.
    /// </summary>
    private static readonly TimeSpan ClosureTimeout = ResolveTimeout();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private static TimeSpan ResolveTimeout()
    {
        var raw = Environment.GetEnvironmentVariable("MCP_DSL_EVAL_TIMEOUT_SECONDS");
        if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return TimeSpan.FromSeconds(30);
    }

    [McpServerTool(Name = "ga_dsl_list_closures"), Description(
        "List the domain closures available for evaluation via ga_dsl_eval. " +
        "Returns name, description, category, and tags for each visible closure. " +
        "Call this first to discover what closures exist before constructing an EvalClosure call. " +
        "Only Domain-category closures are exposed; Pipeline/Io/Agent are excluded.")]
    public ClosureListResult ListClosures()
    {
        var visible = GaClosureRegistry.Global
            .List(FSharpOption<GaClosureCategory>.Some(GaClosureCategory.Domain))
            .Select(c => new ClosureListEntry
            {
                Name        = c.Name,
                Description = c.Description,
                Category    = c.Category.ToString(),
                Tags        = [.. c.Tags],
            })
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();

        return new ClosureListResult
        {
            Closures = visible,
        };
    }

    [McpServerTool(Name = "ga_dsl_get_closure_schema"), Description(
        "Return the input schema and output type of a single closure so the caller can construct " +
        "valid arguments before invoking ga_dsl_eval. Closure names are case-insensitive. " +
        "Errors with closure-not-found if the name is unknown, or closure-not-exposed if the " +
        "closure exists but isn't in the visible (Domain) category.")]
    public ClosureSchemaResult GetClosureSchema(
        [Description("The closure name, e.g. 'domain.transposeChord'. Case-insensitive.")]
        string closureName)
    {
        if (string.IsNullOrWhiteSpace(closureName) || closureName.Length > MaxClosureNameLength)
        {
            return ClosureSchemaResult.Failure(
                "closure-not-found",
                $"closure '{McpEchoSanitizer.SanitizeEcho(closureName)}' not found; call ga_dsl_list_closures to see available names");
        }

        var found = GaClosureRegistry.Global.TryGet(closureName);
        if (FSharpOption<GaClosure>.get_IsNone(found))
        {
            return ClosureSchemaResult.Failure(
                "closure-not-found",
                $"closure '{McpEchoSanitizer.SanitizeEcho(closureName)}' not found; call ga_dsl_list_closures to see available names");
        }

        var closure = found.Value;
        if (closure.Category != GaClosureCategory.Domain)
        {
            return ClosureSchemaResult.Failure(
                "closure-not-exposed",
                $"closure '{McpEchoSanitizer.SanitizeEcho(closure.Name)}' is not exposed to the chatbot (category: {closure.Category})");
        }

        return new ClosureSchemaResult
        {
            Name        = closure.Name,
            Description = closure.Description,
            Category    = closure.Category.ToString(),
            Tags        = [.. closure.Tags],
            InputSchema = closure.InputSchema.ToDictionary(kv => kv.Key, kv => kv.Value),
            OutputType  = closure.OutputType,
        };
    }

    [McpServerTool(Name = "ga_dsl_eval"), Description(
        "Invoke a domain closure by name with a flat key-value argument map. " +
        "Argument values are strings; they're coerced to the closure's declared input types " +
        "(string/int/bool/double) per the v0.1 contract. " +
        "Returns the closure's output as a string and as JSON, or a structured Error.")]
    public DslEvalResult EvalClosure(
        [Description("The closure name, e.g. 'domain.transposeChord'. Case-insensitive.")]
        string closureName,
        [Description("Flat key-value map of arguments. All values are strings; type coercion happens server-side per the closure's InputSchema.")]
        Dictionary<string, string>? args)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(closureName) || closureName.Length > MaxClosureNameLength)
        {
            return DslEvalResult.Failure(
                closureName ?? string.Empty,
                "closure-not-found",
                $"closure '{McpEchoSanitizer.SanitizeEcho(closureName)}' not found; call ga_dsl_list_closures to see available names",
                sw.ElapsedMilliseconds,
                category: null);
        }

        var found = GaClosureRegistry.Global.TryGet(closureName);
        if (FSharpOption<GaClosure>.get_IsNone(found))
        {
            return DslEvalResult.Failure(
                closureName,
                "closure-not-found",
                $"closure '{McpEchoSanitizer.SanitizeEcho(closureName)}' not found; call ga_dsl_list_closures to see available names",
                sw.ElapsedMilliseconds,
                category: null);
        }

        var closure = found.Value;
        var category = closure.Category.ToString();

        if (closure.Category != GaClosureCategory.Domain)
        {
            return DslEvalResult.Failure(
                closure.Name,
                "closure-not-exposed",
                $"closure '{McpEchoSanitizer.SanitizeEcho(closure.Name)}' is not exposed to the chatbot (category: {category})",
                sw.ElapsedMilliseconds,
                category);
        }

        // Argument coercion per v0.1 contract (§3).
        var coerceResult = CoerceArgs(closure, args ?? []);
        if (coerceResult.Error is not null)
        {
            return DslEvalResult.Failure(
                closure.Name,
                coerceResult.Error.Code,
                coerceResult.Error.Message,
                sw.ElapsedMilliseconds,
                category);
        }

        // Run the F# closure synchronously with a timeout.
        try
        {
            using var cts = new CancellationTokenSource(ClosureTimeout);
            var token = FSharpOption<CancellationToken>.Some(cts.Token);

            // GaClosure.Exec : Map<string, obj> -> Async<Result<obj, GaError>>
            var asyncWorkflow = closure.Exec.Invoke(coerceResult.CoercedArgs!);
            var result = FSharpAsync.RunSynchronously(asyncWorkflow, FSharpOption<int>.None, token);

            if (result.IsError)
            {
                var err = result.ErrorValue;
                return DslEvalResult.Failure(
                    closure.Name,
                    "closure-runtime-error",
                    McpEchoSanitizer.SanitizeEcho(err.ToString() ?? string.Empty),
                    sw.ElapsedMilliseconds,
                    category);
            }

            var okValue = result.ResultValue;
            var resultJson = JsonSerializer.Serialize(okValue, SerializerOptions);
            var resultString = okValue switch
            {
                null => null,
                string s => s,
                _ => null, // complex types: caller reads ResultJson
            };

            return new DslEvalResult
            {
                ClosureName     = closure.Name,
                ClosureCategory = category,
                Result          = resultString,
                ResultJson      = resultJson,
                ElapsedMs       = sw.ElapsedMilliseconds,
            };
        }
        catch (OperationCanceledException)
        {
            return DslEvalResult.Failure(
                closure.Name,
                "closure-timeout",
                $"closure '{closure.Name}' exceeded the {(int)ClosureTimeout.TotalSeconds}s timeout",
                sw.ElapsedMilliseconds,
                category);
        }
        catch (Exception ex)
        {
            return DslEvalResult.Failure(
                closure.Name,
                "closure-exception",
                McpEchoSanitizer.SanitizeEcho($"{ex.GetType().Name}: {ex.Message}"),
                sw.ElapsedMilliseconds,
                category);
        }
    }

    private sealed record CoerceOutcome(
        FSharpMap<string, object>? CoercedArgs,
        DslEvalError? Error);

    private static CoerceOutcome CoerceArgs(GaClosure closure, Dictionary<string, string> rawArgs)
    {
        // 1) Detect missing required args (everything in InputSchema is required for v0.1).
        foreach (var declared in closure.InputSchema)
        {
            if (!rawArgs.ContainsKey(declared.Key))
            {
                return new CoerceOutcome(null, new DslEvalError
                {
                    Code = "missing-required-arg",
                    Message = $"closure '{McpEchoSanitizer.SanitizeEcho(closure.Name)}' requires argument '{McpEchoSanitizer.SanitizeEcho(declared.Key)}' (declared type: {declared.Value})",
                    ClosureCategory = closure.Category.ToString(),
                });
            }
        }

        // 2) Coerce each declared arg to the type implied by InputSchema.
        var coerced = new List<Tuple<string, object>>(rawArgs.Count);
        foreach (var kv in rawArgs)
        {
            object value;
            if (closure.InputSchema.ContainsKey(kv.Key))
            {
                var declaredType = closure.InputSchema[kv.Key].Trim();
                if (!TryCoerce(kv.Value, declaredType, out value!))
                {
                    return new CoerceOutcome(null, new DslEvalError
                    {
                        Code = "arg-coerce-failed",
                        Message = $"argument '{McpEchoSanitizer.SanitizeEcho(kv.Key)}' could not be parsed as {declaredType.Split(' ')[0]} (got '{McpEchoSanitizer.SanitizeEcho(kv.Value)}')",
                        ClosureCategory = closure.Category.ToString(),
                    });
                }
            }
            else
            {
                // Extra args not in InputSchema: pass through as string.
                value = kv.Value;
            }
            coerced.Add(Tuple.Create(kv.Key, value));
        }

        var fsMap = MapModule.OfSeq<string, object>(coerced);
        return new CoerceOutcome(fsMap, null);
    }

    private static bool TryCoerce(string raw, string declaredType, out object value)
    {
        // Substring-prefix match per contract §3. The declared type may be
        // bare ("int") or prefixed-prose ("int — number of semitones"); we
        // only look at the leading token.
        var leading = declaredType.TrimStart();

        if (leading.StartsWith("string", StringComparison.OrdinalIgnoreCase))
        {
            value = raw;
            return true;
        }
        if (leading.StartsWith("int", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i))
            {
                value = i;
                return true;
            }
            value = null!;
            return false;
        }
        if (leading.StartsWith("bool", StringComparison.OrdinalIgnoreCase))
        {
            if (bool.TryParse(raw, out var b))
            {
                value = b;
                return true;
            }
            value = null!;
            return false;
        }
        if (leading.StartsWith("double", StringComparison.OrdinalIgnoreCase) ||
            leading.StartsWith("float", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                value = d;
                return true;
            }
            value = null!;
            return false;
        }

        // Anything else: pass through as string. The closure decides how to interpret.
        value = raw;
        return true;
    }
}

/// <summary>
/// Single entry in <see cref="ClosureListResult.Closures"/>.
/// </summary>
public sealed record ClosureListEntry
{
    public string Name        { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category    { get; init; } = string.Empty;
    public string[] Tags      { get; init; } = [];
}

/// <summary>
/// Result of <see cref="DslEvalMcpTools.ListClosures"/>.
/// </summary>
public sealed record ClosureListResult
{
    public ClosureListEntry[] Closures { get; init; } = [];
    public string? Error { get; init; }
}

/// <summary>
/// Result of <see cref="DslEvalMcpTools.GetClosureSchema"/>.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, all string fields
/// are <see cref="string.Empty"/> and <see cref="InputSchema"/> /
/// <see cref="Tags"/> are empty. Branch on <see cref="Error"/> first.
/// </remarks>
public sealed record ClosureSchemaResult
{
    public string Name        { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category    { get; init; } = string.Empty;
    public string[] Tags      { get; init; } = [];
    public Dictionary<string, string> InputSchema { get; init; } = [];
    public string OutputType  { get; init; } = string.Empty;
    public string? Error { get; init; }

    public static ClosureSchemaResult Failure(string code, string message) => new()
    {
        Error = $"{code}: {message}",
    };
}

/// <summary>
/// Structured error returned by <see cref="DslEvalResult.Error"/>.
/// </summary>
public sealed record DslEvalError
{
    public string Code            { get; init; } = string.Empty;
    public string Message         { get; init; } = string.Empty;
    public string? ClosureCategory { get; init; }
}

/// <summary>
/// Result of <see cref="DslEvalMcpTools.EvalClosure"/>.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, <see cref="Result"/>
/// and <see cref="ResultJson"/> are <see langword="null"/>. Callers branch on
/// <see cref="Error"/> first.
/// </remarks>
public sealed record DslEvalResult
{
    public string ClosureName       { get; init; } = string.Empty;
    public string? ClosureCategory  { get; init; }
    public string? Result           { get; init; }
    public string? ResultJson       { get; init; }
    public long ElapsedMs           { get; init; }
    public DslEvalError? Error      { get; init; }

    public static DslEvalResult Failure(string closureName, string code, string message, long elapsedMs, string? category) => new()
    {
        ClosureName     = closureName,
        ClosureCategory = category,
        ElapsedMs       = elapsedMs,
        Error = new DslEvalError
        {
            Code = code,
            Message = message,
            ClosureCategory = category,
        },
    };
}
