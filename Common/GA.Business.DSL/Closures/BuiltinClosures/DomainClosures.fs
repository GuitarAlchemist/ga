module GA.Business.DSL.Closures.BuiltinClosures.DomainClosures

open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry
open GA.Business.DSL.Types
open GA.Business.DSL.Services

// ── Note / accidental helpers ─────────────────────────────────────────────────

let private accStr = function
    | Natural    -> "" | Sharp    -> "#"  | Flat    -> "b"
    | DoubleSharp -> "##"               | DoubleFlat -> "bb"

let private noteToSemitone = function
    | "C" -> 0 | "D" -> 2 | "E" -> 4 | "F" -> 5
    | "G" -> 7 | "A" -> 9 | "B" -> 11 | _ -> 0

let private accToSemitone = function
    | Natural -> 0 | Sharp -> 1 | DoubleSharp ->  2
    | Flat   -> -1 | DoubleFlat             -> -2

/// Chromatic note names using sharp spelling.
let private sharpNames = [| "C";"C#";"D";"D#";"E";"F";"F#";"G";"G#";"A";"A#";"B" |]

/// Split e.g. "C#" → ("C", Sharp) or "Bb" → ("B", Flat). Assumes first char is uppercase.
let private splitNoteAcc (s: string) : string * AccidentalType =
    if   s.EndsWith "##" then s.[..s.Length-3], DoubleSharp
    elif s.EndsWith "#"  then s.[..s.Length-2], Sharp
    elif s.EndsWith "bb" then s.[..s.Length-3], DoubleFlat
    elif s.Length > 1 && s.EndsWith "b" && "CDEFGAB".Contains(s.[0]) then
         s.[..s.Length-2], Flat
    else s, Natural

/// Normalize user-supplied root so the letter is uppercase, accidentals remain as-is.
/// E.g. "bb" → "Bb", "f#" → "F#", "C" → "C".
let private normalizeRoot (s: string) =
    if s.Length = 0 then s
    else (string s.[0]).ToUpperInvariant() + s.[1..]

/// Serialize a ChordAst to a compact JSON string.
let private serializeAst (ast: ChordAst) =
    let compStr =
        ast.Components
        |> List.map (function
            | Extension e      -> sprintf "\"ext:%s\"" e
            | Alteration(a, d) -> sprintf "\"alt:%s%s\"" (accStr a) d
            | Omission d       -> sprintf "\"omit:%s\"" d
            | Alt              -> "\"alt\"")
        |> String.concat ","
    let qualStr =
        match ast.Quality with
        | None            -> "null"
        | Some Major      -> "\"major\""     | Some Minor     -> "\"minor\""
        | Some Diminished -> "\"diminished\"" | Some Augmented -> "\"augmented\""
        | Some Suspended  -> "\"suspended\""  | Some Dominant  -> "\"dominant\""
    let bassStr =
        match ast.Bass with
        | None       -> "null"
        | Some(n, a) -> sprintf "\"%s%s\"" n (accStr a)
    sprintf """{"root":"%s%s","quality":%s,"components":[%s],"bass":%s}"""
        ast.Root (accStr ast.RootAccidental) qualStr compStr bassStr

// ── Diatonic scale degree patterns ────────────────────────────────────────────
// Each entry: (semitone offset from root, triad QualityType option).
// None = major triad (no suffix rendered).

let private majorPattern : (int * QualityType option) list =
    [ 0, None; 2, Some Minor; 4, Some Minor; 5, None
      7, None; 9, Some Minor; 11, Some Diminished ]

let private minorPattern : (int * QualityType option) list =
    [ 0, Some Minor; 2, Some Diminished; 3, None; 5, Some Minor
      7, Some Minor; 8, None; 10, None ]

let private qualSuffix = function
    | None            -> ""    | Some Major     -> ""
    | Some Minor      -> "m"   | Some Diminished -> "dim"
    | Some Augmented  -> "aug" | Some Suspended  -> "sus"
    | Some Dominant   -> "7"

// ── Closures ──────────────────────────────────────────────────────────────────

/// Parse a chord symbol string and return its structure as JSON.
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
                  match ChordDslService().Parse symbol with
                  | Result.Error err -> return Error (GaError.ParseError ("chord", err))
                  | Result.Ok ast    -> return Ok (box (serializeAst ast))
          } }

/// Transpose a chord symbol by N semitones, returning the new chord symbol string.
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
              | None, _ -> return Error (GaError.DomainError "Missing 'symbol' input")
              | _, None -> return Error (GaError.DomainError "Missing 'semitones' input")
              | Some sym, Some n ->
                  let symbol    = sym :?> string
                  let semitones = n   :?> int
                  let svc       = ChordDslService()
                  match svc.Parse symbol with
                  | Result.Error err -> return Error (GaError.ParseError ("chord", err))
                  | Result.Ok ast ->
                      let rootPc  = (noteToSemitone ast.Root + accToSemitone ast.RootAccidental + 120) % 12
                      let newPc   = (rootPc + semitones % 12 + 12) % 12
                      let newRoot, newAcc = splitNoteAcc sharpNames.[newPc]
                      let newAst  = { ast with Root = newRoot; RootAccidental = newAcc }
                      return Ok (box (svc.Render newAst))
          } }

/// Return all 7 diatonic triads for a given root note and scale (major or minor).
let diatonicChords : GaClosure =
    { Name        = "domain.diatonicChords"
      Category    = GaClosureCategory.Domain
      Description = "Return the 7 diatonic triads for a root note and scale (major/minor)."
      Tags        = [ "scale"; "chords"; "harmony"; "music-theory" ]
      InputSchema = Map.ofList [ "root", "string"; "scale", "string" ]
      OutputType  = "string[] (chord symbols)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "root", inputs.TryFind "scale" with
              | None, _ -> return Error (GaError.DomainError "Missing 'root' input")
              | _, None -> return Error (GaError.DomainError "Missing 'scale' input")
              | Some r, Some s ->
                  let rootNote, rootAcc = splitNoteAcc (normalizeRoot (r :?> string))
                  let rootPc  = (noteToSemitone rootNote + accToSemitone rootAcc + 120) % 12
                  let pattern =
                      match (s :?> string).ToLowerInvariant() with
                      | "minor" | "aeolian" | "natural minor" -> minorPattern
                      | _                                      -> majorPattern
                  let chords =
                      pattern
                      |> List.map (fun (offset, quality) ->
                          let pc         = (rootPc + offset) % 12
                          let note, acc  = splitNoteAcc sharpNames.[pc]
                          sprintf "%s%s%s" note (accStr acc) (qualSuffix quality))
                      |> List.toArray
                  return Ok (box chords)
          } }

// ── Registration ──────────────────────────────────────────────────────────────

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ parseChord
          transposeChord
          diatonicChords ]

do register ()
