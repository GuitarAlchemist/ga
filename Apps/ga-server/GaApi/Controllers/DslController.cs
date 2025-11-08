namespace GaApi.Controllers;

using GA.MusicTheory.DSL.Generators;
using GA.MusicTheory.DSL.Parsers;
using GA.MusicTheory.DSL.Types;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.FSharp.Core;

/// <summary>
///     API endpoints for Music Theory DSL parsing and generation
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("regular")]
public class DslController(ILogger<DslController> logger) : ControllerBase
{
    /// <summary>
    ///     Parse a Grothendieck operation and return the AST
    /// </summary>
    /// <param name="request">The parse request containing the input string</param>
    /// <returns>Parse result with AST or error</returns>
    [HttpPost("parse-grothendieck")]
    [ProducesResponseType(typeof(ParseGrothendieckResponse), 200)]
    [ProducesResponseType(400)]
    public ActionResult<ParseGrothendieckResponse> ParseGrothendieck([FromBody] ParseGrothendieckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new ParseGrothendieckResponse
            {
                Success = false,
                Error = "Input cannot be empty"
            });
        }

        logger.LogInformation("Parsing Grothendieck operation: {Input}", request.Input);

        try
        {
            var result = GrothendieckOperationsParser.parse(request.Input);

            if (result.IsOk)
            {
                var operation = result.ResultValue;

                // Convert F# discriminated union to a serializable object
                var ast = ConvertGrothendieckOperationToObject(operation);

                logger.LogInformation("Successfully parsed Grothendieck operation");

                return Ok(new ParseGrothendieckResponse
                {
                    Success = true,
                    Ast = ast
                });
            }

            var error = result.ErrorValue;
            logger.LogWarning("Failed to parse Grothendieck operation: {Error}", error);

            return Ok(new ParseGrothendieckResponse
            {
                Success = false,
                Error = error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing Grothendieck operation");
            return BadRequest(new ParseGrothendieckResponse
            {
                Success = false,
                Error = $"Parse error: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Generate DSL code from a Grothendieck operation (round-trip: parse then generate)
    /// </summary>
    /// <param name="request">The generation request containing the input DSL</param>
    /// <returns>Generated DSL code</returns>
    [HttpPost("generate-grothendieck")]
    [ProducesResponseType(typeof(GenerateGrothendieckResponse), 200)]
    [ProducesResponseType(400)]
    public ActionResult<GenerateGrothendieckResponse> GenerateGrothendieck(
        [FromBody] GenerateGrothendieckRequest request)
    {
        logger.LogInformation("Generating Grothendieck DSL code from input: {Input}", request.Input);

        try
        {
            // Parse the input first
            var parseResult = GrothendieckOperationsParser.parse(request.Input);

            if (parseResult.IsOk)
            {
                var operation = parseResult.ResultValue;

                // Generate DSL code from the parsed operation
                var generatedCode = GrothendieckGenerator.generate(operation);

                logger.LogInformation("Successfully generated Grothendieck DSL code");

                return Ok(new GenerateGrothendieckResponse
                {
                    Success = true,
                    Code = generatedCode,
                    Original = request.Input
                });
            }

            var error = parseResult.ErrorValue;
            logger.LogWarning("Failed to parse input for generation: {Error}", error);

            return Ok(new GenerateGrothendieckResponse
            {
                Success = false,
                Error = $"Parse error: {error}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating Grothendieck DSL");
            return BadRequest(new GenerateGrothendieckResponse
            {
                Success = false,
                Error = $"Generation error: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Convert F# GrothendieckOperation to a serializable object
    /// </summary>
    private static object ConvertGrothendieckOperationToObject(GrammarTypes.GrothendieckOperation operation)
    {
        return operation switch
        {
            GrammarTypes.GrothendieckOperation.TensorProduct tensor => new
            {
                Type = "TensorProduct",
                Left = ConvertMusicalObjectToObject(tensor.Item1),
                Right = ConvertMusicalObjectToObject(tensor.Item2)
            },
            GrammarTypes.GrothendieckOperation.DirectSum sum => new
            {
                Type = "DirectSum",
                Left = ConvertMusicalObjectToObject(sum.Item1),
                Right = ConvertMusicalObjectToObject(sum.Item2)
            },
            GrammarTypes.GrothendieckOperation.Product product => new
            {
                Type = "Product",
                Objects = product.Item.Select(ConvertMusicalObjectToObject).ToList()
            },
            GrammarTypes.GrothendieckOperation.Coproduct coprod => new
            {
                Type = "Coproduct",
                Objects = coprod.Item.Select(ConvertMusicalObjectToObject).ToList()
            },
            GrammarTypes.GrothendieckOperation.Exponential exp => new
            {
                Type = "Exponential",
                Base = ConvertMusicalObjectToObject(exp.Item1),
                Exponent = ConvertMusicalObjectToObject(exp.Item2)
            },
            GrammarTypes.GrothendieckOperation.DefineFunctor def => new
            {
                Type = "DefineFunctor",
                def.Item.Name,
                def.Item.SourceCategory,
                def.Item.TargetCategory
            },
            GrammarTypes.GrothendieckOperation.ApplyFunctor app => new
            {
                Type = "ApplyFunctor",
                Functor = app.Item1,
                Object = ConvertMusicalObjectToObject(app.Item2)
            },
            GrammarTypes.GrothendieckOperation.ComposeFunctors comp => new
            {
                Type = "ComposeFunctors",
                Functors = comp.Item.ToList()
            },
            GrammarTypes.GrothendieckOperation.DefineNatTrans nat => new
            {
                Type = "DefineNatTrans",
                nat.Item.Name,
                nat.Item.SourceFunctor,
                nat.Item.TargetFunctor
            },
            GrammarTypes.GrothendieckOperation.ApplyNatTrans appNat => new
            {
                Type = "ApplyNatTrans",
                Name = appNat.Item1,
                Object = ConvertMusicalObjectToObject(appNat.Item2)
            },
            GrammarTypes.GrothendieckOperation.Limit lim => new
            {
                Type = "Limit",
                Diagram = lim.Item.ToString()
            },
            GrammarTypes.GrothendieckOperation.Pullback pullback => new
            {
                Type = "Pullback",
                Left = ConvertMusicalObjectToObject(pullback.Item1),
                Morphism = pullback.Item2.ToString(),
                Right = ConvertMusicalObjectToObject(pullback.Item3)
            },
            GrammarTypes.GrothendieckOperation.Equalizer eq => new
            {
                Type = "Equalizer",
                Morphism1 = eq.Item1.ToString(),
                Morphism2 = eq.Item2.ToString()
            },
            GrammarTypes.GrothendieckOperation.Colimit colim => new
            {
                Type = "Colimit",
                Diagram = colim.Item.ToString()
            },
            GrammarTypes.GrothendieckOperation.Pushout pushout => new
            {
                Type = "Pushout",
                Left = ConvertMusicalObjectToObject(pushout.Item1),
                Morphism = pushout.Item2.ToString(),
                Right = ConvertMusicalObjectToObject(pushout.Item3)
            },
            GrammarTypes.GrothendieckOperation.Coequalizer coeq => new
            {
                Type = "Coequalizer",
                Morphism1 = coeq.Item1.ToString(),
                Morphism2 = coeq.Item2.ToString()
            },
            GrammarTypes.GrothendieckOperation.SubobjectClassifier omega => new
            {
                Type = "SubobjectClassifier",
                Object = omega.Item != null && OptionModule.IsSome(omega.Item)
                    ? ConvertMusicalObjectToObject(omega.Item.Value)
                    : null
            },
            GrammarTypes.GrothendieckOperation.PowerObject power => new
            {
                Type = "PowerObject",
                Object = ConvertMusicalObjectToObject(power.Item)
            },
            GrammarTypes.GrothendieckOperation.InternalHom hom => new
            {
                Type = "InternalHom",
                Source = ConvertMusicalObjectToObject(hom.Item1),
                Target = ConvertMusicalObjectToObject(hom.Item2)
            },
            GrammarTypes.GrothendieckOperation.DefineSheaf sheaf => new
            {
                Type = "DefineSheaf",
                sheaf.Item.Name,
                Space = sheaf.Item.Space.ToString()
            },
            GrammarTypes.GrothendieckOperation.SheafRestriction restr => new
            {
                Type = "SheafRestriction",
                Sheaf = restr.Item1,
                OpenSet = restr.Item2
            },
            GrammarTypes.GrothendieckOperation.SheafGluing glue => new
            {
                Type = "SheafGluing",
                Objects = glue.Item1.Select(ConvertMusicalObjectToObject).ToList()
            },
            _ => new { Type = "Unknown", Value = operation.ToString() }
        };
    }

    /// <summary>
    ///     Convert F# MusicalObject to a serializable object
    /// </summary>
    private static object ConvertMusicalObjectToObject(GrammarTypes.MusicalObject obj)
    {
        return obj switch
        {
            GrammarTypes.MusicalObject.NoteObject note => new { Type = "Note", Value = note.Item.ToString() },
            GrammarTypes.MusicalObject.ChordObject chord => new { Type = "Chord", Value = chord.Item.ToString() },
            GrammarTypes.MusicalObject.ScaleObject scale => new { Type = "Scale", Value = scale.Item.ToString() },
            GrammarTypes.MusicalObject.ProgressionObject prog => new
                { Type = "Progression", Value = prog.Item.ToString() },
            GrammarTypes.MusicalObject.VoicingObject voicing => new
                { Type = "Voicing", Value = voicing.Item.ToString() },
            GrammarTypes.MusicalObject.SetClassObject setClass => new
                { Type = "SetClass", Value = setClass.pitchClasses },
            _ => new { Type = "Unknown", Value = obj.ToString() }
        };
    }

    /// <summary>
    ///     Parse a chord progression and return the AST
    /// </summary>
    /// <param name="request">The parse request containing the input string</param>
    /// <returns>Parse result with AST or error</returns>
    [HttpPost("parse-chord-progression")]
    [ProducesResponseType(typeof(ParseChordProgressionResponse), 200)]
    [ProducesResponseType(400)]
    public ActionResult<ParseChordProgressionResponse> ParseChordProgression(
        [FromBody] ParseChordProgressionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new ParseChordProgressionResponse
            {
                Success = false,
                Error = "Input cannot be empty"
            });
        }

        logger.LogInformation("Parsing chord progression: {Input}", request.Input);

        try
        {
            var result = ChordProgressionParser.parse(request.Input);

            if (result.IsOk)
            {
                var progression = result.ResultValue;

                // Convert to serializable object
                var ast = new
                {
                    Chords = progression.Chords.Select(c => c switch
                    {
                        GrammarTypes.ProgressionChord.AbsoluteChord chord => new
                            { Type = "Absolute", Chord = chord.Item.ToString() },
                        GrammarTypes.ProgressionChord.RomanNumeralChord roman => new
                            { Type = "Roman", Chord = roman.Item.ToString() },
                        _ => new { Type = "Unknown", Chord = c.ToString() }
                    }).ToList(),
                    Key = progression.Key?.ToString(),
                    TimeSignature = progression.TimeSignature?.ToString(),
                    progression.Tempo,
                    progression.Metadata
                };

                logger.LogInformation("Successfully parsed chord progression");

                return Ok(new ParseChordProgressionResponse
                {
                    Success = true,
                    Ast = ast
                });
            }

            var error = result.ErrorValue;
            logger.LogWarning("Failed to parse chord progression: {Error}", error);

            return Ok(new ParseChordProgressionResponse
            {
                Success = false,
                Error = error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing chord progression");
            return StatusCode(500, new ParseChordProgressionResponse
            {
                Success = false,
                Error = $"Internal error: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Parse a fretboard navigation command and return the AST
    /// </summary>
    /// <param name="request">The parse request containing the input string</param>
    /// <returns>Parse result with AST or error</returns>
    [HttpPost("parse-fretboard-navigation")]
    [ProducesResponseType(typeof(ParseFretboardNavigationResponse), 200)]
    [ProducesResponseType(400)]
    public ActionResult<ParseFretboardNavigationResponse> ParseFretboardNavigation(
        [FromBody] ParseFretboardNavigationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new ParseFretboardNavigationResponse
            {
                Success = false,
                Error = "Input cannot be empty"
            });
        }

        logger.LogInformation("Parsing fretboard navigation: {Input}", request.Input);

        try
        {
            var result = FretboardNavigationParser.parse(request.Input);

            if (result.IsOk)
            {
                var command = result.ResultValue;

                // Convert to serializable object
                var ast = ConvertNavigationCommandToObject(command);

                logger.LogInformation("Successfully parsed fretboard navigation command");

                return Ok(new ParseFretboardNavigationResponse
                {
                    Success = true,
                    Ast = ast
                });
            }

            var error = result.ErrorValue;
            logger.LogWarning("Failed to parse fretboard navigation: {Error}", error);

            return Ok(new ParseFretboardNavigationResponse
            {
                Success = false,
                Error = error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing fretboard navigation");
            return StatusCode(500, new ParseFretboardNavigationResponse
            {
                Success = false,
                Error = $"Internal error: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Convert a NavigationCommand to a serializable object
    /// </summary>
    private static object ConvertNavigationCommandToObject(GrammarTypes.NavigationCommand cmd)
    {
        return cmd switch
        {
            GrammarTypes.NavigationCommand.GotoPosition pos => new
            {
                Type = "GotoPosition",
                pos.Item.String,
                pos.Item.Fret,
                Finger = pos.Item.Finger?.ToString()
            },
            GrammarTypes.NavigationCommand.GotoShape shape => new
            {
                Type = "GotoShape",
                Shape = shape.Item1.ToString(),
                Fret = shape.fret
            },
            GrammarTypes.NavigationCommand.Move move => new
            {
                Type = "Move",
                Direction = move.direction.ToString(),
                Distance = move.distance
            },
            GrammarTypes.NavigationCommand.Slide slide => new
            {
                Type = "Slide",
                From = new { slide.from.String, slide.from.Fret },
                To = new { slide.to.String, slide.to.Fret }
            },
            GrammarTypes.NavigationCommand.FindNote find => new
            {
                Type = "FindNote",
                Note = find.note.ToString(),
                Constraints = find.constraints?.ToString()
            },
            GrammarTypes.NavigationCommand.FindChord findChord => new
            {
                Type = "FindChord",
                Chord = findChord.chord.ToString(),
                Constraints = findChord.constraints?.ToString()
            },
            GrammarTypes.NavigationCommand.NavigatePath path => new
            {
                Type = "NavigatePath",
                From = new { path.from.String, path.from.Fret },
                To = new { path.to.String, path.to.Fret }
            },
            _ => new { Type = "Unknown", Value = cmd.ToString() }
        };
    }
}

// Request/Response DTOs
public record ParseGrothendieckRequest(string Input);

public record ParseGrothendieckResponse
{
    public bool Success { get; set; }
    public object? Ast { get; set; }
    public string? Error { get; set; }
}

public record GenerateGrothendieckRequest(string Input);

public record GenerateGrothendieckResponse
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Original { get; set; }
    public string? Error { get; set; }
}

public record ParseChordProgressionRequest(string Input);

public record ParseChordProgressionResponse
{
    public bool Success { get; set; }
    public object? Ast { get; set; }
    public string? Error { get; set; }
}

public record ParseFretboardNavigationRequest(string Input);

public record ParseFretboardNavigationResponse
{
    public bool Success { get; set; }
    public object? Ast { get; set; }
    public string? Error { get; set; }
}
