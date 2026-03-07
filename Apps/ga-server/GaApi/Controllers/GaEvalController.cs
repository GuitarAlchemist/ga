namespace GaApi.Controllers;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.FSharp.Control;
using static GA.Business.DSL.Interpreter.GaFsiSessionPool;
using GaClosureCategory = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureCategory;
using GaClosureRegistry  = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// REST API for evaluating GA Language scripts and introspecting the closure registry.
/// Backed by the process-wide <see cref="GaFsiPool"/> session pool.
/// Eval endpoint is restricted to Development environment — never exposed in staging or production.
/// </summary>
[ApiController]
[Route("api/ga")]
public sealed class GaEvalController(IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Evaluate a ga { } computation expression script in an FSI session.</summary>
    [HttpPost("eval")]
    [ProducesResponseType(typeof(GaEvalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Eval(
        [FromBody] GaEvalRequest request,
        CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden, "Script evaluation is only available in development mode.");

        if (string.IsNullOrWhiteSpace(request.Script))
            return BadRequest("script must not be empty");

        // F# Async<T> → C# Task<T>: use FSharpAsync.StartAsTask
        var fsharpAsync = GaFsiPool.Instance.EvalAsync(request.Script, cancellationToken);
        var result      = await FSharpAsync.StartAsTask(fsharpAsync, null, cancellationToken);

        if (result is GaScriptResult.GaScriptOk ok)
            return Ok(new GaEvalResponse(true, ok.stdout, null, ok.value?.ToString()));

        if (result is GaScriptResult.GaScriptError err)
            return Ok(new GaEvalResponse(false, err.stdout, err.message, null));

        return StatusCode(500, "Unknown evaluation result");
    }

    /// <summary>List all registered GA DSL closures, optionally filtered by category. Development only.</summary>
    [HttpGet("closures")]
    [ProducesResponseType(typeof(GaClosureInfo[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ListClosures([FromQuery] string? category = null)
    {
        if (!env.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden, "Closure introspection is only available in development mode.");

        var registry = GaClosureRegistry.Global;
        var closures = category is null
            ? registry.List(Microsoft.FSharp.Core.FSharpOption<GaClosureCategory>.None)
            : registry.List(Microsoft.FSharp.Core.FSharpOption<GaClosureCategory>.Some(ParseCategory(category)));

        var infos = closures.Select(c => new GaClosureInfo(
            c.Name,
            c.Category.ToString(),
            c.Description,
            [.. c.Tags],
            c.InputSchema
                .Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value))
                .ToDictionary(x => x.Key, x => x.Value),
            c.OutputType)).ToArray();

        return Ok(infos);
    }

    private static GaClosureCategory ParseCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            "pipeline" => GaClosureCategory.Pipeline,
            "agent"    => GaClosureCategory.Agent,
            "io"       => GaClosureCategory.Io,
            _          => GaClosureCategory.Domain
        };
}

public sealed record GaEvalRequest(string Script);

public sealed record GaEvalResponse(
    bool    Success,
    string  Output,
    string? Error,
    string? Value);

public sealed record GaClosureInfo(
    string                      Name,
    string                      Category,
    string                      Description,
    string[]                    Tags,
    Dictionary<string, string>  InputSchema,
    string                      OutputType);
