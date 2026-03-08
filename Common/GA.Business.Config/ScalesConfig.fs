namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System
open System.Collections.Generic
open System.Collections.Immutable

module ScalesConfig =

    // ── Raw YAML deserialization type ─────────────────────────────────────────
    // Each top-level YAML key is the scale name; values are the properties.
    // [<CLIMutable>] lets YamlDotNet set F# record fields.

    [<CLIMutable>]
    type ScaleData =
        { Notes: string
          AlternateNames: ResizeArray<string>
          Common: bool
          Category: string
          Usage: string
          RelatedScales: ResizeArray<string>
          /// Optional override: Forte set-class notation (e.g. "7-35").
          /// Leave blank — YamlDotNet sets this from the YAML if present.
          ForteNumber: string }

    // ── Pitch-class helpers ───────────────────────────────────────────────────

    /// Note-name → pitch class (C = 0 … B = 11).
    /// Covers single sharps/flats plus common enharmonics (Fb, E#, Cb, B#).
    let private noteToPC =
        dict [
            "C",  0; "B#", 0
            "C#", 1; "Db", 1
            "D",  2
            "D#", 3; "Eb", 3
            "E",  4; "Fb", 4
            "F",  5; "E#", 5
            "F#", 6; "Gb", 6
            "G",  7
            "G#", 8; "Ab", 8
            "A",  9
            "A#", 10; "Bb", 10
            "B",  11; "Cb", 11
        ]

    /// Compute the binary scale ID for a space-separated note string.
    /// Each note's pitch class n contributes bit 2^n to the result,
    /// yielding a 12-bit integer that uniquely identifies the pitch-class set.
    let computeBinaryScaleId (notes: string) : int =
        notes.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold
            (fun acc note ->
                match noteToPC.TryGetValue(note) with
                | true, pc -> acc ||| (1 <<< pc)
                | false, _ -> acc)
            0

    // ── Public domain type ────────────────────────────────────────────────────

    type ScaleInfo =
        { Name: string
          Notes: string
          Description: string option
          AlternateNames: IReadOnlyList<string>
          Category: string option
          Common: bool
          Usage: string option
          RelatedScales: IReadOnlyList<string>
          /// 12-bit pitch-class bitmask (bit n = pitch class n present, C=0…B=11).
          BinaryScaleId: int
          /// Forte set-class identifier (e.g. "7-35" for the major scale).
          /// Present only when explicitly set in Scales.yaml.
          ForteNumber: string option }

    // ── YAML loading ──────────────────────────────────────────────────────────

    let private findYaml () =
        let name = "Scales.yaml"
        [ AppDomain.CurrentDomain.BaseDirectory
          Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config")
          Environment.CurrentDirectory
          Path.Combine(Environment.CurrentDirectory, "config") ]
        |> List.map (fun d -> Path.Combine(d, name))
        |> List.tryFind File.Exists

    let mutable private cachedScales: ImmutableList<ScaleInfo> option = None
    let mutable private byIanRingId: IReadOnlyDictionary<int, ScaleInfo> = Dictionary() :> _
    let mutable private byName: IReadOnlyDictionary<string, ScaleInfo> = Dictionary() :> _
    let mutable private version = Guid.NewGuid()

    let private buildScales (rawDict: IDictionary<string, ScaleData>) : ImmutableList<ScaleInfo> =
        let builder = ImmutableList.CreateBuilder<ScaleInfo>()
        for KeyValue(scaleName, data) in rawDict do
            if not (isNull data.Notes) && not (String.IsNullOrWhiteSpace data.Notes) then
                let alts =
                    if isNull data.AlternateNames then [] :> IReadOnlyList<string>
                    else data.AlternateNames :> IReadOnlyList<string>
                let related =
                    if isNull data.RelatedScales then [] :> IReadOnlyList<string>
                    else data.RelatedScales :> IReadOnlyList<string>
                let forte =
                    if isNull data.ForteNumber || String.IsNullOrWhiteSpace data.ForteNumber
                    then None
                    else Some data.ForteNumber
                builder.Add(
                    { Name        = scaleName
                      Notes       = data.Notes
                      Description = None  // YAML field not present; add if needed
                      AlternateNames = alts
                      Category    = if isNull data.Category then None else Some data.Category
                      Common      = data.Common
                      Usage       = if isNull data.Usage then None else Some data.Usage
                      RelatedScales = related
                      BinaryScaleId = computeBinaryScaleId data.Notes
                      ForteNumber = forte })
        builder.ToImmutable()

    let private buildIndices (scales: ImmutableList<ScaleInfo>) =
        let byId  = Dictionary<int, ScaleInfo>(scales.Count)
        let byNm  = Dictionary<string, ScaleInfo>(scales.Count * 2, StringComparer.OrdinalIgnoreCase)
        for s in scales do
            byId.TryAdd(s.BinaryScaleId, s) |> ignore
            byNm.TryAdd(s.Name, s) |> ignore
            for alt in s.AlternateNames do
                byNm.TryAdd(alt, s) |> ignore
        byIanRingId <- byId :> IReadOnlyDictionary<int, ScaleInfo>
        byName      <- byNm :> IReadOnlyDictionary<string, ScaleInfo>

    let private loadScalesData () =
        try
            match findYaml () with
            | None -> false
            | Some path ->
                let deserializer =
                    DeserializerBuilder()
                        .WithNamingConvention(PascalCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build()
                let yaml   = File.ReadAllText(path)
                let rawDict = deserializer.Deserialize<Dictionary<string, ScaleData>>(yaml)
                let scales = buildScales rawDict
                buildIndices scales
                cachedScales <- Some scales
                true
        with ex ->
            eprintfn $"[ScalesConfig] Failed to load Scales.yaml: %s{ex.Message}"
            false

    let private ensureLoaded () =
        if cachedScales.IsNone then
            loadScalesData () |> ignore

    // ── Public API ────────────────────────────────────────────────────────────

    let GetVersion () = version

    let ReloadConfig () =
        try
            if loadScalesData () then
                version <- Guid.NewGuid()
                true
            else
                false
        with _ ->
            false

    let GetAllScales () : IEnumerable<ScaleInfo> =
        ensureLoaded ()
        match cachedScales with
        | None   -> Seq.empty
        | Some s -> s :> IEnumerable<ScaleInfo>

    /// Look up a scale by its binary scale ID (e.g. 2741 for the major scale).
    let TryGetScaleByBinaryId (id: int) : ScaleInfo option =
        ensureLoaded ()
        match byIanRingId.TryGetValue(id) with
        | true, s -> Some s
        | false, _ -> None

    /// Look up a scale by canonical name or alternate name (case-insensitive).
    let TryGetScaleByName (name: string) : ScaleInfo option =
        ensureLoaded ()
        match byName.TryGetValue(name) with
        | true, s -> Some s
        | false, _ -> None

    // Legacy: keep Scale property for any callers that used it.
    // Now always returns None (internal raw dict no longer needed externally).
    [<Obsolete("Use GetAllScales() instead.")>]
    let Scale: obj option = None
