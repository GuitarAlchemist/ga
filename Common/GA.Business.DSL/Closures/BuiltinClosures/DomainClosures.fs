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

/// Chord tones of a parsed symbol as (semitones above the root, interval name), sorted.
/// A stacked extension (9, 11, 13) implies the seventh below it, a bare 7 is minor unless the
/// token says maj, a diminished seventh chord has a diminished seventh, and alterations replace
/// the degree they alter: Cm7b5 is P1 m3 d5 m7, G7b9 is P1 M3 P5 m7 m9, Cadd9 has no seventh.
let private chordTones (ast: ChordAst) : (int * string) list =
    let exts = ast.Components |> List.choose (function Extension e -> Some e | _ -> None)
    let has e = List.contains e exts
    let quality = if has "m7b5" then Some Diminished else ast.Quality
    let stackedDegree =
        exts
        |> List.choose (function
            | "7" | "maj7" | "m7b5" -> Some 7
            | "9" | "maj9" -> Some 9
            | "11" | "maj11" -> Some 11
            | "13" | "maj13" -> Some 13
            | _ -> None)
        |> function
            | [] when quality = Some Dominant -> Some 7
            | [] -> None
            | degrees -> Some (List.max degrees)
    let third =
        match quality with
        | _ when has "5" && stackedDegree.IsNone -> []
        | Some Suspended -> if has "2" then [ 2, "M2" ] else [ 5, "P4" ]
        | Some Minor | Some Diminished -> [ 3, "m3" ]
        | _ -> [ 4, "M3" ]
    let fifth =
        match quality with
        | Some Diminished -> 6, "d5"
        | Some Augmented -> 8, "A5"
        | _ -> 7, "P5"
    let seventh =
        match stackedDegree with
        | None -> []
        | Some _ when exts |> List.exists (fun e -> e.StartsWith "maj") -> [ 11, "M7" ]
        | Some _ when quality = Some Diminished && not (has "m7b5") -> [ 9, "d7" ]
        | Some _ -> [ 10, "m7" ]
    let upper =
        match stackedDegree with
        | Some 9 -> [ 14, "M9" ]
        | Some 11 -> [ 14, "M9"; 17, "P11" ]
        | Some 13 -> [ 14, "M9"; 21, "M13" ]
        | _ -> []
    let added =
        exts
        |> List.collect (function
            | "6" -> [ 9, "M6" ]
            | "6/9" -> [ 9, "M6"; 14, "M9" ]
            | "add9" -> [ 14, "M9" ]
            | "add2" -> [ 2, "M2" ]
            | "add11" -> [ 17, "P11" ]
            | "add4" -> [ 5, "P4" ]
            | "add13" -> [ 21, "M13" ]
            | _ -> [])
    let alter (tones: (int * string) list) chordComponent =
        let replace (removed: int list) tone =
            (tones |> List.filter (fun (s, _) -> not (List.contains s removed))) @ [ tone ]
        match chordComponent with
        | Alteration (Flat, "5") -> replace [ 7; 8 ] (6, "d5")
        | Alteration (Sharp, "5") -> replace [ 6; 7 ] (8, "A5")
        | Alteration (Flat, "9") -> replace [ 14 ] (13, "m9")
        | Alteration (Sharp, "9") -> replace [ 14 ] (15, "A9")
        | Alteration (Sharp, "11") -> replace [ 17 ] (18, "A11")
        | Alteration (Flat, "13") -> replace [ 21 ] (20, "m13")
        | Omission "3" -> tones |> List.filter (fun (s, _) -> s <> 3 && s <> 4)
        | Omission "5" -> tones |> List.filter (fun (s, _) -> s < 6 || s > 8)
        | _ -> tones
    let baseTones = [ 0, "P1" ] @ third @ [ fifth ] @ seventh @ added @ upper
    ast.Components
    |> List.fold alter baseTones
    |> List.distinctBy fst
    |> List.sortBy fst

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
                      let all = chordTones ast |> List.map snd |> List.toArray
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

/// Interval name → semitone offset (the names chordTones produces).
let private intervalSemitone = function
    | "P1" -> Some 0  | "m2" -> Some 1  | "M2" -> Some 2  | "m3" -> Some 3
    | "M3" -> Some 4  | "P4" -> Some 5  | "TT" -> Some 6  | "P5" -> Some 7
    | "m6" -> Some 8  | "M6" -> Some 9  | "m7" -> Some 10 | "M7" -> Some 11
    | "M9" -> Some 14 | "P11" -> Some 17 | "M13" -> Some 21
    | "d5" -> Some 6  | "A5" -> Some 8  | "d7" -> Some 9  | "m9" -> Some 13
    | "A9" -> Some 15 | "A11" -> Some 18 | "m13" -> Some 20
    | _    -> None

/// All pitch classes sounded by a parsed chord (root + intervals, mod 12).
let private chordPitchClasses (ast: ChordAst) =
    let rootPc = (noteToSemitone ast.Root + accToSemitone ast.RootAccidental + 120) % 12
    chordTones ast
    |> List.map (fun (i, _) -> (rootPc + i) % 12)
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
                      match inputs.TryFind "hasInterval" |> Option.map (fun v -> (v :?> string).ToUpperInvariant()) with
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
                      let intervalStr = chordTones ast |> List.map snd |> String.concat " "
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
                                  let role1 = ivals1 |> List.tryFind (fun (i, _) -> (root1Pc + i) % 12 = pc) |> Option.map snd |> Option.defaultValue "?"
                                  let role2 = ivals2 |> List.tryFind (fun (i, _) -> (root2Pc + i) % 12 = pc) |> Option.map snd |> Option.defaultValue "?"
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
                                                  let r1 = tIvals |> List.tryFind (fun (i, _) -> (targetRootPc + i) % 12 = pc) |> Option.map snd |> Option.defaultValue "?"
                                                  let r2 = cIvals |> List.tryFind (fun (i, _) -> (cRootPc + i) % 12 = pc) |> Option.map snd |> Option.defaultValue "?"
                                                  sprintf "%s(%s/%s)" name r1 r2)
                                              |> String.concat " "
                                          Some (candidate, shared.Length, sharedDesc))
                          |> List.sortByDescending (fun (_, n, _) -> n)
                      // Tritone substitution — works for dominant-7th chords (M3 + m7)
                      let tritoneSub =
                          let semitones = tIvals |> List.map fst
                          if semitones |> List.contains 4 && semitones |> List.contains 10 then
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
