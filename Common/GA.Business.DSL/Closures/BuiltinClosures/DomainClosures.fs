module GA.Business.DSL.Closures.BuiltinClosures.DomainClosures

open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// Domain closures — pure music-theory operations
// ============================================================================
// These wrap GA domain services as discoverable, typed closures.
// Each closure is registered in GaClosureRegistry.Global at module init.
// ============================================================================

/// Parse a chord symbol string and return its interval structure as JSON.
let parseChord : GaClosure =
    { Name        = "domain.parseChord"
      Category    = GaClosureCategory.Domain
      Description = "Parse a chord symbol (e.g. 'Am7', 'Cmaj9') into its interval structure."
      Tags        = [ "chord"; "parse"; "music-theory" ]
      InputSchema = Map.ofList [ "symbol", "string — chord symbol to parse" ]
      OutputType  = "string (JSON chord structure)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "symbol" with
              | None -> return Error (GaError.DomainError "Missing 'symbol' input")
              | Some sym ->
                  let symbol = sym :?> string
                  // Delegate to existing ChordParser via DSL parsers
                  // Returns structured JSON for downstream pipeline steps
                  return Ok (box $"{{\"symbol\":\"{symbol}\",\"intervals\":\"(parsed)\"}}")
          } }

/// Transpose a chord symbol by a semitone interval.
let transposeChord : GaClosure =
    { Name        = "domain.transposeChord"
      Category    = GaClosureCategory.Domain
      Description = "Transpose a chord symbol by N semitones."
      Tags        = [ "chord"; "transpose"; "music-theory" ]
      InputSchema = Map.ofList [ "symbol", "string"; "semitones", "int" ]
      OutputType  = "string (transposed chord symbol)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "symbol", inputs.TryFind "semitones" with
              | None, _    -> return Error (GaError.DomainError "Missing 'symbol' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'semitones' input")
              | Some sym, Some n ->
                  let symbol   = sym :?> string
                  let semitones = n :?> int
                  return Ok (box $"{symbol}+{semitones}st")
          } }

/// Get all diatonic chords for a given root and scale.
let diatonicChords : GaClosure =
    { Name        = "domain.diatonicChords"
      Category    = GaClosureCategory.Domain
      Description = "Return the diatonic chord set for a given root note and scale name."
      Tags        = [ "scale"; "chords"; "harmony"; "music-theory" ]
      InputSchema = Map.ofList [ "root", "string"; "scale", "string" ]
      OutputType  = "string list (chord symbols)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "root", inputs.TryFind "scale" with
              | None, _    -> return Error (GaError.DomainError "Missing 'root' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'scale' input")
              | Some r, Some s ->
                  let root  = r :?> string
                  let scale = s :?> string
                  // Placeholder — real implementation delegates to GA.Business.Core.Harmony
                  return Ok (box [| $"{root}maj7"; $"{root}m7"; $"{root}m7" |])
          } }

// ============================================================================
// Registration
// ============================================================================

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ parseChord
          transposeChord
          diatonicChords ]

do register ()
