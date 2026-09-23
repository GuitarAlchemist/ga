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

/// Chromatic note names using flat spelling.
let private flatNames  = [| "C";"Db";"D";"Eb";"E";"F";"Gb";"G";"Ab";"A";"Bb";"B" |]

/// True when the key conventionally uses flat accidentals.
/// F natural is the one exception among white-key roots (it contains Bb).
let private preferFlat (note: string) (acc: AccidentalType) =
    match acc with
    | Flat | DoubleFlat  -> true
    | Sharp | DoubleSharp -> false
    | Natural             -> note = "F"

let private spellingOf useFlat = if useFlat then flatNames else sharpNames

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

// ── Interval helpers ──────────────────────────────────────────────────────────

let private qualityBaseIntervals = function
    | None            -> [0; 4; 7]
    | Some Major      -> [0; 4; 7]
    | Some Minor      -> [0; 3; 7]
    | Some Diminished -> [0; 3; 6]
    | Some Augmented  -> [0; 4; 8]
    | Some Dominant   -> [0; 4; 7; 10]
    | Some Suspended  -> [0; 5; 7]

/// Semitones of the major or perfect interval for a chord degree (1 → 0, 3 → 4, 9 → 14, 13 → 21).
let private naturalSemitones degree =
    let d = degree - 1
    [| 0; 2; 4; 5; 7; 9; 11 |].[d % 7] + 12 * (d / 7)

/// Unison, fourth and fifth (and their compounds) are perfect; the other degrees are major/minor.
let private isPerfectDegree degree = match (degree - 1) % 7 with 0 | 3 | 4 -> true | _ -> false

/// Interval name from a chord degree and its size in semitones: (5, 6) → "d5", (7, 9) → "d7", (9, 13) → "m9".
let private intervalNameOf (degree, semitones) =
    let prefix =
        match isPerfectDegree degree, semitones - naturalSemitones degree with
        | true, 0   -> Some "P"  | true, -1  -> Some "d"  | true, 1  -> Some "A"
        | false, 0  -> Some "M"  | false, -1 -> Some "m"  | false, -2 -> Some "d" | false, 1 -> Some "A"
        | _         -> None
    match prefix with
    | Some p -> sprintf "%s%d" p degree
    | None   -> sprintf "+%d" semitones

/// Interval name → semitones: "P5" → 7, "d5" → 6, "m9" → 13. Also accepts the legacy "TT" (tritone).
let private intervalSemitone (name: string) =
    if name = "TT" then Some 6
    elif name.Length < 2 then None
    else
        match System.Int32.TryParse(name.Substring 1) with
        | true, degree when degree >= 1 ->
            let natural = naturalSemitones degree
            match name.[0], isPerfectDegree degree with
            | 'P', true  -> Some natural
            | 'M', false -> Some natural
            | 'm', false -> Some (natural - 1)
            | 'd', true  -> Some (natural - 1)
            | 'd', false -> Some (natural - 2)
            | 'A', _     -> Some (natural + 1)
            | _          -> None
        | _ -> None

/// Chord tones as (degree, semitones above the root), stacked in thirds: the quality's triad,
/// then each extension (9, 11 and 13 imply the seventh; dim7 has a diminished seventh), then
/// alterations (b5, #9, #11, b13…) replacing the natural degree, and omissions.
/// Matches the formulas of Chord.FromSymbol in GA.Domain.Core. Sorted by size.
let private chordTones (ast: ChordAst) : (int * int) list =
    let setDegree degree semitones tones =
        (tones |> List.filter (fun (d, _) -> d <> degree)) @ [ degree, semitones ]
    let removeDegree degree tones = tones |> List.filter (fun (d, _) -> d <> degree)
    let triad =
        match ast.Quality with
        | Some Minor      -> [ 1, 0; 3, 3; 5, 7 ]
        | Some Diminished -> [ 1, 0; 3, 3; 5, 6 ]
        | Some Augmented  -> [ 1, 0; 3, 4; 5, 8 ]
        | Some Dominant   -> [ 1, 0; 3, 4; 5, 7; 7, 10 ]
        | Some Suspended when ast.Components |> List.contains (Extension "2") -> [ 1, 0; 2, 2; 5, 7 ]
        | Some Suspended  -> [ 1, 0; 4, 5; 5, 7 ]
        | None | Some Major -> [ 1, 0; 3, 4; 5, 7 ]
    let seventh = if ast.Quality = Some Diminished then 9 else 10
    let stack minorOrMajorSeventh upTo tones =
        [ 9, 14; 11, 17; 13, 21 ]
        |> List.filter (fun (d, _) -> d <= upTo)
        |> List.fold (fun acc (d, s) -> setDegree d s acc) (setDegree 7 minorOrMajorSeventh tones)
    let apply tones comp =
        match comp with
        | Extension "7"     -> setDegree 7 seventh tones
        | Extension "maj7"  -> setDegree 7 11 tones
        | Extension "9"     -> stack seventh 9 tones
        | Extension "maj9"  -> stack 11 9 tones
        | Extension "11"    -> stack seventh 11 tones
        | Extension "maj11" -> stack 11 11 tones
        | Extension "13"    -> stack seventh 13 tones
        | Extension "maj13" -> stack 11 13 tones
        | Extension "m7b5"  -> tones |> setDegree 3 3 |> setDegree 5 6 |> setDegree 7 10
        | Extension "6"     -> setDegree 6 9 tones
        | Extension "6/9"   -> tones |> setDegree 6 9 |> setDegree 9 14
        | Extension "add9"  -> setDegree 9 14 tones
        | Extension "add11" -> setDegree 11 17 tones
        | Extension "add13" -> setDegree 13 21 tones
        | Extension ("2" | "add2") when ast.Quality <> Some Suspended -> setDegree 2 2 tones
        | Extension ("4" | "add4") when ast.Quality <> Some Suspended -> setDegree 4 5 tones
        | Extension "5"     -> removeDegree 3 tones   // power chord
        | Extension _       -> tones
        | Alteration (acc, degreeStr) ->
            match System.Int32.TryParse degreeStr with
            | true, degree ->
                let natural = naturalSemitones degree
                let altered = natural + accToSemitone acc
                let rest = tones |> List.filter (fun t -> t <> (degree, natural))
                if List.contains (degree, altered) rest then rest else rest @ [ degree, altered ]
            | _ -> tones
        | Omission degreeStr ->
            match System.Int32.TryParse degreeStr with
            | true, degree -> removeDegree degree tones
            | _            -> tones
        | Alt ->
            // Altered dominant: minor seventh, no perfect fifth, b9 #9 #11 b13.
            (tones |> setDegree 7 10 |> removeDegree 5) @ [ 9, 13; 9, 15; 11, 18; 13, 20 ]
    ast.Components
    |> List.fold apply triad
    |> List.distinct
    |> List.sortBy (fun (d, s) -> s, d)

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
                      let naming  = spellingOf (preferFlat ast.Root ast.RootAccidental)
                      let newRoot, newAcc = splitNoteAcc naming.[newPc]
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
                  let naming = spellingOf (preferFlat rootNote rootAcc)
                  let chords =
                      pattern
                      |> List.map (fun (offset, quality) ->
                          let pc         = (rootPc + offset) % 12
                          let note, acc  = splitNoteAcc naming.[pc]
                          sprintf "%s%s%s" note (accStr acc) (qualSuffix quality))
                      |> List.toArray
                  return Ok (box chords)
          } }

/// Return the interval names (P1, M3, P5, …) for a chord symbol.
let chordIntervals : GaClosure =
    { Name        = "domain.chordIntervals"
      Category    = GaClosureCategory.Domain
      Description = "Return interval names (P1, m3, P5…) for a chord symbol."
      Tags        = [ "chord"; "intervals"; "music-theory" ]
      InputSchema = Map.ofList [ "symbol", "string — chord symbol" ]
      OutputType  = "string[] (interval names)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "symbol" with
              | None -> return Error (GaError.DomainError "Missing 'symbol' input")
              | Some sym ->
                  match ChordDslService().Parse(sym :?> string) with
                  | Result.Error err -> return Error (GaError.ParseError ("chord", err))
                  | Result.Ok ast ->
                      let all =
                          chordTones ast
                          |> List.map intervalNameOf
                          |> List.toArray
                      return Ok (box all)
          } }

/// Return the relative key (major ↔ minor) for a root note and scale.
let relativeKey : GaClosure =
    { Name        = "domain.relativeKey"
      Category    = GaClosureCategory.Domain
      Description = "Return the relative major/minor key for a given root and scale."
      Tags        = [ "key"; "relative"; "harmony"; "music-theory" ]
      InputSchema = Map.ofList [ "root", "string"; "scale", "string (major|minor)" ]
      OutputType  = "string (e.g. 'A minor' or 'C major')"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "root", inputs.TryFind "scale" with
              | None, _ -> return Error (GaError.DomainError "Missing 'root' input")
              | _, None -> return Error (GaError.DomainError "Missing 'scale' input")
              | Some r, Some s ->
                  let rootNote, rootAcc = splitNoteAcc (normalizeRoot (r :?> string))
                  let rootPc   = (noteToSemitone rootNote + accToSemitone rootAcc + 120) % 12
                  let offset, relScale =
                      match (s :?> string).ToLowerInvariant() with
                      | "minor" | "aeolian" | "natural minor" -> 3, "major"
                      | _                                      -> 9, "minor"
                  let relPc   = (rootPc + offset) % 12
                  let naming  = spellingOf (preferFlat rootNote rootAcc)
                  let relRoot = naming.[relPc]
                  return Ok (box (sprintf "%s %s" relRoot relScale))
          } }

// ── Progression analysis ──────────────────────────────────────────────────────

let private majorOffsets = [| 0; 2; 4; 5; 7; 9; 11 |]
let private minorOffsets = [| 0; 2; 3; 5; 7; 8; 10 |]
let private majorRomans  = [| "I";   "ii";  "iii"; "IV"; "V";  "vi"; "vii°" |]
let private minorRomans  = [| "i";   "ii°"; "III"; "iv"; "v";  "VI"; "VII"  |]

/// Conventional key name (Bb, Eb, Ab, Db, Gb rather than A#, D#, …).
let private conventionalKeyName pc =
    match pc with
    | 1 | 3 | 8 | 10 -> flatNames.[pc]   // Db, Eb, Ab, Bb — prefer flat
    | _               -> sharpNames.[pc]  // everything else — prefer sharp / natural

let private scoreKey rootPc (offsets: int[]) (chordPcs: int list) =
    let diatonic = offsets |> Array.map (fun o -> (rootPc + o) % 12) |> Set.ofArray
    chordPcs |> List.filter diatonic.Contains |> List.length

let private romanFor rootPc (offsets: int[]) (romans: string[]) chordPc =
    offsets
    |> Array.tryFindIndex (fun o -> (rootPc + o) % 12 = chordPc)
    |> Option.map (fun i -> romans.[i])
    |> Option.defaultValue "?"

/// Infer the key of a chord progression and label each chord with a Roman numeral.
let analyzeProgression : GaClosure =
    { Name        = "domain.analyzeProgression"
      Category    = GaClosureCategory.Domain
      Description = "Infer the key of a progression and label each chord with a Roman numeral."
      Tags        = [ "progression"; "analysis"; "roman-numerals"; "harmony"; "music-theory" ]
      InputSchema = Map.ofList [ "chords", "string — space-separated chord symbols" ]
      OutputType  = "string (formatted key + Roman numeral analysis)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "chords" with
              | None -> return Error (GaError.DomainError "Missing 'chords' input")
              | Some c ->
                  let svc     = ChordDslService()
                  let symbols =
                      (c :?> string).Split([|' '; ','; '\t'|],
                          System.StringSplitOptions.RemoveEmptyEntries)
                  let parsed =
                      symbols
                      |> Array.map (fun sym ->
                          match svc.Parse sym with
                          | Result.Error _ -> None
                          | Result.Ok ast  ->
                              let pc = (noteToSemitone ast.Root + accToSemitone ast.RootAccidental + 120) % 12
                              Some (sym, pc))
                  let validPcs =
                      parsed |> Array.choose (Option.map snd) |> Array.toList
                  if validPcs.IsEmpty then
                      return Error (GaError.DomainError "Could not parse any chord symbols")
                  else
                      // Score every major and minor key.
                      // Tiebreaker: prefer the key whose root matches the first chord.
                      let firstPc = validPcs |> List.tryHead |> Option.defaultValue 0
                      let keyRootPc, scaleName =
                          [ for rpc in 0..11 do
                              yield rpc, "major", scoreKey rpc majorOffsets validPcs
                              yield rpc, "minor", scoreKey rpc minorOffsets validPcs ]
                          |> List.maxBy (fun (rpc, _, s) -> s * 2 + (if rpc = firstPc then 1 else 0))
                          |> fun (rpc, scale, _) -> rpc, scale
                      let offsets = if scaleName = "major" then majorOffsets else minorOffsets
                      let romans  = if scaleName = "major" then majorRomans  else minorRomans
                      let keyName = conventionalKeyName keyRootPc
                      let confidence =
                          let matches = scoreKey keyRootPc offsets validPcs
                          sprintf "%d/%d" matches validPcs.Length
                      let symLine =
                          parsed |> Array.map (fun p ->
                              let s = p |> Option.map fst |> Option.defaultValue "?"
                              sprintf "%-6s" s) |> String.concat " "
                      let romLine =
                          parsed |> Array.map (fun p ->
                              let r = p |> Option.map (fun (_, pc) ->
                                  romanFor keyRootPc offsets romans pc) |> Option.defaultValue "?"
                              sprintf "%-6s" r) |> String.concat " "
                      let result =
                          sprintf "Key: %s %s  (confidence %s)\n%s\n%s"
                              keyName scaleName confidence symLine romLine
                      return Ok (box result)
          } }

// ── Query / projection / join helpers ─────────────────────────────────────────

/// All pitch classes sounded by a parsed chord (root + intervals, mod 12).
let private chordPitchClasses (ast: ChordAst) =
    let rootPc = (noteToSemitone ast.Root + accToSemitone ast.RootAccidental + 120) % 12
    chordTones ast
    |> List.map (fun (_, s) -> (rootPc + s) % 12)
    |> List.distinct

/// Filter diatonic chords by quality and/or interval content.
let queryChords : GaClosure =
    { Name        = "domain.queryChords"
      Category    = GaClosureCategory.Domain
      Description = "Filter diatonic chords by quality or interval content."
      Tags        = [ "query"; "filter"; "diatonic"; "harmony"; "music-theory" ]
      InputSchema = Map.ofList
          [ "key",         "string — root note (e.g. G, Bb)"
            "scale",       "string — major|minor"
            "quality",     "string? — major|minor|diminished|augmented|dominant"
            "hasInterval", "string? — interval name a chord must contain (P1, m3, P5…)"
            "degree",      "string? — Roman numeral to select (I, ii, IV…)" ]
      OutputType  = "string[] (matched degree=chord pairs)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "key", inputs.TryFind "scale" with
              | None, _ -> return Error (GaError.DomainError "Missing 'key' input")
              | _, None -> return Error (GaError.DomainError "Missing 'scale' input")
              | Some k, Some s ->
                  let rootNote, rootAcc = splitNoteAcc (normalizeRoot (k :?> string))
                  let rootPc  = (noteToSemitone rootNote + accToSemitone rootAcc + 120) % 12
                  let scale   = (s :?> string).ToLowerInvariant()
                  let pattern = match scale with
                                | "minor" | "aeolian" | "natural minor" -> minorPattern
                                | _                                      -> majorPattern
                  let romans  = match scale with
                                | "minor" | "aeolian" | "natural minor" -> minorRomans
                                | _                                      -> majorRomans
                  let naming  = spellingOf (preferFlat rootNote rootAcc)
                  // Build annotated diatonic set
                  let degrees =
                      pattern |> List.mapi (fun i (offset, quality) ->
                          let pc          = (rootPc + offset) % 12
                          let note, acc   = splitNoteAcc naming.[pc]
                          let sym         = sprintf "%s%s%s" note (accStr acc) (qualSuffix quality)
                          let intervals   = qualityBaseIntervals quality
                          romans.[i], sym, quality, intervals)
                  // Filters
                  let qualOk =
                      match inputs.TryFind "quality" |> Option.map (fun q -> (q :?> string).ToLowerInvariant()) with
                      | None -> fun _ -> true
                      | Some "major"      -> fun q -> q = None || q = Some Major
                      | Some "minor"      -> fun q -> q = Some Minor
                      | Some "diminished" -> fun q -> q = Some Diminished
                      | Some "augmented"  -> fun q -> q = Some Augmented
                      | Some "dominant"   -> fun q -> q = Some Dominant
                      | _                 -> fun _ -> true
                  let ivOk =
                      // Case matters: m3 (minor third) is not M3 (major third).
                      match inputs.TryFind "hasInterval" |> Option.map (fun v -> (v :?> string).Trim()) with
                      | None    -> fun _ -> true
                      | Some iv -> match intervalSemitone iv with
                                   | Some semi -> fun (ivals: int list) -> ivals |> List.contains semi
                                   | None      -> fun _ -> false
                  let degOk =
                      match inputs.TryFind "degree" |> Option.map (fun v -> (v :?> string).ToUpperInvariant()) with
                      | None   -> fun _ -> true
                      | Some d -> fun (roman: string) -> roman.ToUpperInvariant() = d
                  let results =
                      degrees
                      |> List.choose (fun (roman, sym, quality, intervals) ->
                          if qualOk quality && ivOk intervals && degOk roman
                          then Some (sprintf "%s=%s" roman sym)
                          else None)
                      |> List.toArray
                  return Ok (box results)
          } }

/// Project specific fields from a parsed chord symbol.
let projectChord : GaClosure =
    { Name        = "domain.projectChord"
      Category    = GaClosureCategory.Domain
      Description = "Project selected fields from a chord: root quality components bass intervals pc."
      Tags        = [ "project"; "chord"; "fields"; "music-theory" ]
      InputSchema = Map.ofList
          [ "symbol", "string — chord symbol"
            "fields", "string — space-separated field names" ]
      OutputType  = "string (field=value pairs)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "symbol", inputs.TryFind "fields" with
              | None, _ -> return Error (GaError.DomainError "Missing 'symbol' input")
              | _, None -> return Error (GaError.DomainError "Missing 'fields' input")
              | Some sym, Some flds ->
                  match ChordDslService().Parse(sym :?> string) with
                  | Result.Error err -> return Error (GaError.ParseError ("chord", err))
                  | Result.Ok ast ->
                      let pc = (noteToSemitone ast.Root + accToSemitone ast.RootAccidental + 120) % 12
                      let intervalStr =
                          chordTones ast
                          |> List.map intervalNameOf
                          |> String.concat " "
                      let qualStr =
                          match ast.Quality with
                          | None | Some Major -> "major" | Some Minor -> "minor"
                          | Some Diminished   -> "diminished"
                          | Some Augmented    -> "augmented"
                          | Some Dominant     -> "dominant"
                          | Some Suspended    -> "suspended"
                      let compStr =
                          ast.Components
                          |> List.map (function
                              | Extension e      -> e
                              | Alteration(a, d) -> sprintf "%s%s" (accStr a) d
                              | Omission d       -> sprintf "omit%s" d
                              | Alt              -> "alt")
                          |> String.concat ","
                      let bassStr =
                          match ast.Bass with
                          | None       -> "none"
                          | Some(n, a) -> sprintf "%s%s" n (accStr a)
                      let row =
                          (flds :?> string).Split([|' '; ','|])
                          |> Array.filter (fun s -> s <> "")
                          |> Array.map (fun f ->
                              match f.ToLowerInvariant() with
                              | "root"       -> sprintf "root=%s%s" ast.Root (accStr ast.RootAccidental)
                              | "quality"    -> sprintf "quality=%s" qualStr
                              | "components" -> sprintf "components=[%s]" compStr
                              | "bass"       -> sprintf "bass=%s" bassStr
                              | "intervals"  -> sprintf "intervals=%s" intervalStr
                              | "pc"         -> sprintf "pc=%d" pc
                              | other        -> sprintf "%s=?" other)
                          |> String.concat "  "
                      return Ok (box row)
          } }

/// Find common tones between two chords and describe their voice-leading roles.
let commonTones : GaClosure =
    { Name        = "domain.commonTones"
      Category    = GaClosureCategory.Domain
      Description = "Find notes shared between two chords — useful for pivot-chord and voice-leading analysis."
      Tags        = [ "join"; "common-tones"; "voice-leading"; "pivot"; "music-theory" ]
      InputSchema = Map.ofList [ "chord1", "string — first chord symbol"; "chord2", "string — second chord symbol" ]
      OutputType  = "string (shared notes with their roles in each chord)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "chord1", inputs.TryFind "chord2" with
              | None, _ -> return Error (GaError.DomainError "Missing 'chord1' input")
              | _, None -> return Error (GaError.DomainError "Missing 'chord2' input")
              | Some c1, Some c2 ->
                  let svc = ChordDslService()
                  match svc.Parse(c1 :?> string), svc.Parse(c2 :?> string) with
                  | Result.Error e, _ -> return Error (GaError.ParseError ("chord1", e))
                  | _, Result.Error e -> return Error (GaError.ParseError ("chord2", e))
                  | Result.Ok ast1, Result.Ok ast2 ->
                      let pcs1 = chordPitchClasses ast1
                      let pcs2 = chordPitchClasses ast2
                      let shared = pcs1 |> List.filter (fun pc -> pcs2 |> List.contains pc)
                      if shared.IsEmpty then
                          let result = sprintf "%s and %s share no common tones" (c1 :?> string) (c2 :?> string)
                          return Ok (box result)
                      else
                          let root1Pc = (noteToSemitone ast1.Root + accToSemitone ast1.RootAccidental + 120) % 12
                          let root2Pc = (noteToSemitone ast2.Root + accToSemitone ast2.RootAccidental + 120) % 12
                          let ivals1  = chordTones ast1
                          let ivals2  = chordTones ast2
                          let desc =
                              shared
                              |> List.map (fun pc ->
                                  let noteName = conventionalKeyName pc
                                  let role1 = ivals1 |> List.tryFind (fun (_, s) -> (root1Pc + s) % 12 = pc) |> Option.map intervalNameOf |> Option.defaultValue "?"
                                  let role2 = ivals2 |> List.tryFind (fun (_, s) -> (root2Pc + s) % 12 = pc) |> Option.map intervalNameOf |> Option.defaultValue "?"
                                  sprintf "%s (%s in %s, %s in %s)" noteName role1 (c1 :?> string) role2 (c2 :?> string))
                              |> String.concat "\n  "
                          let result =
                              sprintf "Common tones (%d):\n  %s" shared.Length desc
                          return Ok (box result)
          } }

/// Suggest diatonic substitutions for a chord in a key, ranked by common tones,
/// plus tritone sub for dominant-7th chords.
let chordSubstitutions : GaClosure =
    { Name        = "domain.chordSubstitutions"
      Category    = GaClosureCategory.Domain
      Description = "Suggest chord substitutions: diatonic swaps ranked by common tones, plus tritone sub for dominant 7th chords."
      Tags        = [ "substitution"; "harmony"; "voice-leading"; "pivot"; "music-theory" ]
      InputSchema = Map.ofList
          [ "symbol", "string — chord to substitute (e.g. 'Am', 'G7')"
            "key",    "string? — key root (e.g. 'C', 'G'). Defaults to chord root."
            "scale",  "string? — 'major' or 'minor'. Defaults to 'major'." ]
      OutputType  = "string (ranked substitution suggestions)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "symbol" with
              | None -> return Error (GaError.DomainError "Missing 'symbol' input")
              | Some sym ->
                  let symbol = sym :?> string
                  let svc    = ChordDslService()
                  match svc.Parse symbol with
                  | Result.Error err -> return Error (GaError.ParseError ("chord", err))
                  | Result.Ok targetAst ->
                      let targetRootPc = (noteToSemitone targetAst.Root + accToSemitone targetAst.RootAccidental + 120) % 12
                      let targetPcs    = chordPitchClasses targetAst
                      // Determine key (default: chord root, major scale)
                      let keyStr   = inputs.TryFind "key"   |> Option.map (fun v -> v :?> string) |> Option.defaultValue targetAst.Root
                      let scaleStr = inputs.TryFind "scale" |> Option.map (fun v -> v :?> string) |> Option.defaultValue "major"
                      let keyNote, keyAcc = splitNoteAcc (normalizeRoot keyStr)
                      let keyPc    = (noteToSemitone keyNote + accToSemitone keyAcc + 120) % 12
                      let pattern  =
                          match scaleStr.ToLowerInvariant() with
                          | "minor" | "aeolian" -> minorPattern
                          | _                   -> majorPattern
                      let naming   = spellingOf (preferFlat keyNote keyAcc)
                      // Build diatonic chord symbols for this key
                      let diatonicSymbols =
                          pattern
                          |> List.map (fun (offset, quality) ->
                              let pc        = (keyPc + offset) % 12
                              let note, acc = splitNoteAcc naming.[pc]
                              sprintf "%s%s%s" note (accStr acc) (qualSuffix quality))
                      let tIvals = chordTones targetAst
                      // Score each diatonic chord by common tones with the target
                      let subs =
                          diatonicSymbols
                          |> List.choose (fun candidate ->
                              match svc.Parse candidate with
                              | Result.Error _ -> None
                              | Result.Ok cAst ->
                                  let cRootPc = (noteToSemitone cAst.Root + accToSemitone cAst.RootAccidental + 120) % 12
                                  // Skip the chord itself
                                  if cRootPc = targetRootPc && cAst.Quality = targetAst.Quality then None
                                  else
                                      let cIvals = chordTones cAst
                                      let cPcs   = chordPitchClasses cAst
                                      let shared = targetPcs |> List.filter (fun pc -> cPcs |> List.contains pc)
                                      if shared.IsEmpty then None
                                      else
                                          let sharedDesc =
                                              shared |> List.map (fun pc ->
                                                  let name = conventionalKeyName pc
                                                  let r1 = tIvals |> List.tryFind (fun (_, s) -> (targetRootPc + s) % 12 = pc) |> Option.map intervalNameOf |> Option.defaultValue "?"
                                                  let r2 = cIvals |> List.tryFind (fun (_, s) -> (cRootPc + s)     % 12 = pc) |> Option.map intervalNameOf |> Option.defaultValue "?"
                                                  sprintf "%s(%s/%s)" name r1 r2)
                                              |> String.concat " "
                                          Some (candidate, shared.Length, sharedDesc))
                          |> List.sortByDescending (fun (_, n, _) -> n)
                      // Tritone substitution — works for dominant-7th chords (M3 + m7)
                      let tritoneSub =
                          if tIvals |> List.contains (3, 4) && tIvals |> List.contains (7, 10) then
                              let ttPc    = (targetRootPc + 6) % 12
                              let ttChord = sprintf "%s7" (spellingOf true).[ttPc]
                              Some (sprintf "  ◈  %-6s — tritone sub (shares guide tones enharmonically)" ttChord)
                          else None
                      // Format output
                      let keyDesc = sprintf "%s %s" keyStr scaleStr
                      let lines =
                          subs |> List.map (fun (cand, n, desc) ->
                              let stars = if n >= 3 then "★★★" elif n = 2 then "★★ " else "★  "
                              sprintf "  %s %-6s — %d shared: %s" stars cand n desc)
                      let header = sprintf "Substitutions for %s in key of %s:" symbol keyDesc
                      let body   = if lines.IsEmpty then [ "  (no diatonic substitutions found)" ] else lines
                      let ttLine = tritoneSub |> Option.toList
                      let result = (header :: body @ ttLine) |> String.concat "\n"
                      return Ok (box result)
          } }

/// Suggest 2–3 diatonic chord completions to cadence an in-progress progression.
let progressionCompletion : GaClosure =
    { Name        = "domain.progressionCompletion"
      Category    = GaClosureCategory.Domain
      Description = "Suggest 2–3 diatonic chord completions to cadence an in-progress progression."
      Tags        = [ "progression"; "completion"; "cadence"; "harmony"; "music-theory" ]
      InputSchema = Map.ofList [ "chords", "string — space-separated chord symbols" ]
      OutputType  = "string (formatted completion suggestions)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "chords" with
              | None -> return Error (GaError.DomainError "Missing 'chords' input")
              | Some c ->
                  let svc     = ChordDslService()
                  let symbols =
                      (c :?> string).Split([|' '; ','; '\t'|],
                          System.StringSplitOptions.RemoveEmptyEntries)
                  let parsed =
                      symbols
                      |> Array.map (fun sym ->
                          match svc.Parse sym with
                          | Result.Error _ -> None
                          | Result.Ok ast  ->
                              let pc = (noteToSemitone ast.Root + accToSemitone ast.RootAccidental + 120) % 12
                              Some pc)
                  let validPcs = parsed |> Array.choose id |> Array.toList
                  if validPcs.IsEmpty then
                      return Error (GaError.DomainError "Could not parse any chord symbols")
                  else
                      // Score every key; tiebreaker: prefer key whose root = first chord.
                      let firstPc = validPcs |> List.tryHead |> Option.defaultValue 0
                      let keyRootPc, scaleName =
                          [ for rpc in 0..11 do
                              yield rpc, "major", scoreKey rpc majorOffsets validPcs
                              yield rpc, "minor", scoreKey rpc minorOffsets validPcs ]
                          |> List.maxBy (fun (rpc, _, s) -> s * 2 + (if rpc = firstPc then 1 else 0))
                          |> fun (rpc, scale, _) -> rpc, scale
                      let keyName = conventionalKeyName keyRootPc
                      // Diatonic chord name at a given scale degree index.
                      let offsets   = if scaleName = "major" then majorOffsets else minorOffsets
                      let qualities =
                          if scaleName = "major"
                          then [| ""; "m"; "m"; ""; ""; "m"; "dim" |]   // I ii iii IV V vi vii°
                          else [| "m"; "dim"; ""; "m"; "m"; ""; "" |]   // i ii° III iv v VI VII
                      let diatonicAt idx =
                          let root = (keyRootPc + offsets.[idx]) % 12
                          sprintf "%s%s" (conventionalKeyName root) qualities.[idx]
                      // Build 2–3 cadence suggestions.
                      let suggestions =
                          if scaleName = "minor" then
                              // V7 from harmonic minor (raised 7th → dominant 7th)
                              let v7Name   = sprintf "%s7" (conventionalKeyName ((keyRootPc + 7) % 12))
                              // ♭VII from natural minor
                              let bviiName = conventionalKeyName ((keyRootPc + 10) % 12)
                              // iv (minor subdominant)
                              let ivName   = diatonicAt 3
                              [ sprintf "  1. %-12s → authentic cadence (V7 → i)" v7Name
                                sprintf "  2. %-12s → half cadence (♭VII → i loop)" bviiName
                                sprintf "  3. %-12s → iv–V7–i turnaround" (sprintf "%s %s" ivName v7Name) ]
                          else
                              let vName  = diatonicAt 4   // V
                              let ivName = diatonicAt 3   // IV
                              let iiName = diatonicAt 1   // ii
                              [ sprintf "  1. %-12s → authentic cadence (V → I)" vName
                                sprintf "  2. %-12s → plagal cadence (IV → I)" ivName
                                sprintf "  3. %-12s → ii–V–I approach" (sprintf "%s %s" iiName vName) ]
                      let inputLine = String.concat " – " (Array.toList symbols)
                      let result =
                          sprintf "Progression: %s  (key: %s %s)\n\nSuggested completions:\n%s"
                              inputLine keyName scaleName (String.concat "\n" suggestions)
                      return Ok (box result)
          } }

// ── Registration ──────────────────────────────────────────────────────────────

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ parseChord
          transposeChord
          diatonicChords
          chordIntervals
          relativeKey
          analyzeProgression
          progressionCompletion
          queryChords
          projectChord
          commonTones
          chordSubstitutions ]
