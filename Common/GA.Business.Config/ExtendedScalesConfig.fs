namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System
open System.IO
open System.Collections.Generic
open System.Collections.Immutable

/// <summary>
/// Loader for the full 12-bit pitch-class-set catalog (4095 entries) emitted by
/// <c>Demos/ScaleCatalogGenerator</c> into <c>ExtendedScales.yaml</c>.
///
/// Loads lazily on first access. The YAML is ~1.2 MB and decodes to ~4095
/// records; kept in memory once loaded for O(1) lookups by binary id,
/// Forte number, or cardinality.
///
/// See <see cref="ScalesConfig"/> for the curated-only scale catalog (~82
/// well-known named scales). The two loaders are independent but can be
/// cross-referenced via <see cref="ExtendedScaleInfo.WellKnownName"/>.
/// </summary>
module ExtendedScalesConfig =

    // ── Prime-form convention selector ─────────────────────────────────────
    // Forte (1973):  "most packed to the left" = lex-min successive intervals.
    // Rahn  (1980):  "most packed to the left" = lex-min PC values read right-to-left
    //                 (equivalently: lex-min interval sequence reversed).
    //
    // The two conventions disagree on a handful of set classes (5-20, 6-Z29/50,
    // 6-31, 7-20, 8-26 and their orbit members — ~120 of 4095 scale ids).

    type PrimeFormConvention =
        | Forte
        | Rahn

    // ── Raw YAML deserialization type ─────────────────────────────────────
    // Each top-level YAML key is "ScaleId_<n>"; values are the properties.

    [<CLIMutable>]
    type ExtendedScaleData =
        { BinaryScaleId: int
          PitchClasses: string
          Cardinality: int
          IntervalVector: string
          /// Nullable: emitted as `null` when outside the 3-9 cardinality scope.
          ForteNumber: string
          PrimeForm_Forte: int
          PrimeForm_Rahn: int
          PrimeFormsAgree: bool
          Complement: int
          Reflection: int
          Perfections: int
          IsPrime_Forte: bool
          IsPrime_Rahn: bool
          /// Optional cross-reference from Scales.yaml. null when not named.
          WellKnownName: string }

    // ── Public domain type ─────────────────────────────────────────────────

    type ExtendedScaleInfo =
        { BinaryScaleId: int
          PitchClasses: IReadOnlyList<int>
          Cardinality: int
          IntervalVector: IReadOnlyList<int>
          ForteNumber: string option
          PrimeFormForte: int
          PrimeFormRahn: int
          PrimeFormsAgree: bool
          Complement: int
          Reflection: int
          Perfections: int
          IsPrimeForte: bool
          IsPrimeRahn: bool
          WellKnownName: string option }

    // ── YAML loading ───────────────────────────────────────────────────────

    let private findYaml () =
        let name = "ExtendedScales.yaml"
        [ AppDomain.CurrentDomain.BaseDirectory
          Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config")
          Environment.CurrentDirectory
          Path.Combine(Environment.CurrentDirectory, "config") ]
        |> List.map (fun d -> Path.Combine(d, name))
        |> List.tryFind File.Exists

    // Lazy caches — populated on first access, rebuilt on ReloadConfig.
    let mutable private cachedScales: ImmutableList<ExtendedScaleInfo> option = None
    let mutable private byBinaryId: IReadOnlyDictionary<int, ExtendedScaleInfo> = Dictionary() :> _
    let mutable private byForteNumber: IReadOnlyDictionary<string, ResizeArray<ExtendedScaleInfo>> =
        Dictionary() :> _
    let mutable private byCardinality: IReadOnlyDictionary<int, ResizeArray<ExtendedScaleInfo>> =
        Dictionary() :> _
    let mutable private version = Guid.NewGuid()

    // Parse space-separated ints: "0 2 4 5 7 9 11" → [|0;2;4;5;7;9;11|]
    let private parseInts (s: string) : int[] =
        if isNull s || String.IsNullOrWhiteSpace s then [||]
        else
            s.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.choose (fun t ->
                match Int32.TryParse(t) with
                | true, n -> Some n
                | false, _ -> None)

    let private toInfo (d: ExtendedScaleData) : ExtendedScaleInfo =
        { BinaryScaleId   = d.BinaryScaleId
          PitchClasses    = parseInts d.PitchClasses :> IReadOnlyList<int>
          Cardinality     = d.Cardinality
          IntervalVector  = parseInts d.IntervalVector :> IReadOnlyList<int>
          ForteNumber     =
              if isNull d.ForteNumber || String.IsNullOrWhiteSpace d.ForteNumber
              then None else Some d.ForteNumber
          PrimeFormForte  = d.PrimeForm_Forte
          PrimeFormRahn   = d.PrimeForm_Rahn
          PrimeFormsAgree = d.PrimeFormsAgree
          Complement      = d.Complement
          Reflection      = d.Reflection
          Perfections     = d.Perfections
          IsPrimeForte    = d.IsPrime_Forte
          IsPrimeRahn     = d.IsPrime_Rahn
          WellKnownName   =
              if isNull d.WellKnownName || String.IsNullOrWhiteSpace d.WellKnownName
              then None else Some d.WellKnownName }

    let private buildIndices (scales: ImmutableList<ExtendedScaleInfo>) =
        let byId = Dictionary<int, ExtendedScaleInfo>(scales.Count)
        let byForte = Dictionary<string, ResizeArray<ExtendedScaleInfo>>(256, StringComparer.OrdinalIgnoreCase)
        let byCard = Dictionary<int, ResizeArray<ExtendedScaleInfo>>(13)
        for s in scales do
            byId.TryAdd(s.BinaryScaleId, s) |> ignore

            match s.ForteNumber with
            | Some forte ->
                match byForte.TryGetValue(forte) with
                | true, bucket -> bucket.Add(s)
                | false, _ ->
                    let bucket = ResizeArray()
                    bucket.Add(s)
                    byForte.[forte] <- bucket
            | None -> ()

            match byCard.TryGetValue(s.Cardinality) with
            | true, bucket -> bucket.Add(s)
            | false, _ ->
                let bucket = ResizeArray()
                bucket.Add(s)
                byCard.[s.Cardinality] <- bucket

        byBinaryId    <- byId    :> IReadOnlyDictionary<_,_>
        byForteNumber <- byForte :> IReadOnlyDictionary<_,_>
        byCardinality <- byCard  :> IReadOnlyDictionary<_,_>

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
                let yaml = File.ReadAllText(path)
                let rawDict = deserializer.Deserialize<Dictionary<string, ExtendedScaleData>>(yaml)

                let builder = ImmutableList.CreateBuilder<ExtendedScaleInfo>()
                for KeyValue(_, data) in rawDict do
                    // Guard against empty stubs where BinaryScaleId got default-initialized.
                    if data.BinaryScaleId > 0 then
                        builder.Add(toInfo data)

                let scales = builder.ToImmutable()
                buildIndices scales
                cachedScales <- Some scales
                true
        with ex ->
            eprintfn $"[ExtendedScalesConfig] Failed to load ExtendedScales.yaml: %s{ex.Message}"
            false

    let private ensureLoaded () =
        if cachedScales.IsNone then
            loadScalesData () |> ignore

    // ── Public API ─────────────────────────────────────────────────────────

    /// Monotonically increases on successful reload — use to invalidate caches.
    let GetVersion () = version

    /// Force a reload from disk. Returns true on success, false on failure
    /// (e.g. YAML file missing, parse error).
    let ReloadConfig () =
        try
            if loadScalesData () then
                version <- Guid.NewGuid()
                true
            else
                false
        with _ ->
            false

    /// <summary>Enumerate every pitch-class-set entry in the catalog (4095 items).</summary>
    let GetAllExtendedScales () : IEnumerable<ExtendedScaleInfo> =
        ensureLoaded ()
        match cachedScales with
        | None -> Seq.empty
        | Some s -> s :> IEnumerable<ExtendedScaleInfo>

    /// <summary>Look up a scale by its 12-bit binary id (1..4095).</summary>
    let TryGetByBinaryId (id: int) : ExtendedScaleInfo option =
        ensureLoaded ()
        match byBinaryId.TryGetValue(id) with
        | true, s  -> Some s
        | false, _ -> None

    /// <summary>
    /// Look up scales by Forte set-class label (e.g. "7-1", "5-16").
    /// Returns all scales in the catalog sharing that set class.
    /// </summary>
    let TryGetByForteNumber (forte: string) : IReadOnlyList<ExtendedScaleInfo> =
        ensureLoaded ()
        if String.IsNullOrWhiteSpace forte then [||] :> IReadOnlyList<_>
        else
            match byForteNumber.TryGetValue(forte) with
            | true, bucket -> bucket :> IReadOnlyList<_>
            | false, _     -> [||] :> IReadOnlyList<_>

    /// <summary>
    /// Return all scales that are prime forms under the requested convention.
    /// For cardinalities 3..9 this approximates the 224 Forte set classes;
    /// cardinalities 0..2 and 10..12 are always self-prime.
    /// </summary>
    let GetPrimeForms (convention: PrimeFormConvention) : IEnumerable<ExtendedScaleInfo> =
        ensureLoaded ()
        match cachedScales with
        | None -> Seq.empty
        | Some s ->
            match convention with
            | Forte -> s |> Seq.filter (fun x -> x.IsPrimeForte)
            | Rahn  -> s |> Seq.filter (fun x -> x.IsPrimeRahn)

    /// <summary>Return all scales with N pitch classes (0 ≤ N ≤ 12).</summary>
    let GetByCardinality (n: int) : IReadOnlyList<ExtendedScaleInfo> =
        ensureLoaded ()
        match byCardinality.TryGetValue(n) with
        | true, bucket -> bucket :> IReadOnlyList<_>
        | false, _     -> [||] :> IReadOnlyList<_>

    /// <summary>
    /// Return the prime-form binary id for the given scale under the requested
    /// convention, or None if the scale isn't in the catalog.
    /// </summary>
    let TryGetPrimeForm (scaleId: int) (convention: PrimeFormConvention) : int option =
        TryGetByBinaryId scaleId
        |> Option.map (fun info ->
            match convention with
            | Forte -> info.PrimeFormForte
            | Rahn  -> info.PrimeFormRahn)
