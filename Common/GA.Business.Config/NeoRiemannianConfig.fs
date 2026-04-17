namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System
open System.Collections.Generic

/// Loader for the neo-Riemannian transform catalog (P, L, R, S, N, H) plus
/// hexatonic and octatonic cycles. Backed by `NeoRiemannian.yaml`, which is
/// generated programmatically from first principles (triad arithmetic mod 12).
///
/// Triad names use the convention `{Root}_{maj|min}` with sharp accidentals
/// (e.g. `C_maj`, `G#_min`). Every transform is a bijection on the 24 triads,
/// and all six are involutions.
module NeoRiemannianConfig =

    /// A triad identifier such as "C_maj" or "G#_min".
    type Triad = string

    /// A single neo-Riemannian transform with its 24-entry triad mapping.
    type NeoRiemannianTransform =
        { /// Transform name: "P", "L", "R", "S", "N", or "H".
          Name: string
          /// Human-readable description (voice-leading semantics).
          Description: string
          /// Number of semitone movements aggregated across all voices
          /// (1 for P/L, 2 for R/S/N, 3 for H).
          VoiceLeadingCost: int
          /// Bijection over the 24 triads. Applying the transform twice to
          /// any triad yields the original triad for P, L, R (involutions).
          Mappings: Map<Triad, Triad> }

    /// A named cycle (hexatonic or octatonic) produced by alternating a pair
    /// of basic transforms (P-L for hexatonic, P-R for octatonic).
    type Cycle =
        { Name: string
          Triads: Triad list }

    // ── Internal YAML shape (CLIMutable for YamlDotNet) ───────────────────────

    [<CLIMutable>]
    type private TransformYaml =
        { Description: string
          VoiceLeadingCost: int
          Mappings: Dictionary<string, string> }

    [<CLIMutable>]
    type private CycleYaml =
        { Name: string
          Triads: ResizeArray<string> }

    [<CLIMutable>]
    type private NeoRiemannianYaml =
        { Transforms: Dictionary<string, TransformYaml>
          HexatonicCycles: ResizeArray<CycleYaml>
          OctatonicCycles: ResizeArray<CycleYaml> }

    // ── Config discovery ──────────────────────────────────────────────────────

    let private findConfigPath () =
        let configName = "NeoRiemannian.yaml"

        let possiblePaths =
            [ Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)
              Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", configName)
              Path.Combine(Environment.CurrentDirectory, configName)
              Path.Combine(Environment.CurrentDirectory, "Common", "GA.Business.Config", configName)
              // For test runners running from bin/<Config>/<tfm>/
              Path.Combine(
                  AppDomain.CurrentDomain.BaseDirectory,
                  "..",
                  "..",
                  "..",
                  "..",
                  "Common",
                  "GA.Business.Config",
                  configName
              ) ]

        possiblePaths |> List.tryFind File.Exists

    // ── Lazy-loaded, cached catalog ───────────────────────────────────────────

    let private loadCatalog () : NeoRiemannianTransform list * Cycle list * Cycle list =
        match findConfigPath () with
        | Some path ->
            try
                let yaml = File.ReadAllText(path)

                let deserializer =
                    DeserializerBuilder()
                        .WithNamingConvention(PascalCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build()

                let data = deserializer.Deserialize<NeoRiemannianYaml>(yaml)

                if isNull (box data) then
                    [], [], []
                else
                    let transforms =
                        if isNull (box data.Transforms) then
                            []
                        else
                            data.Transforms
                            |> Seq.map (fun kv ->
                                let t = kv.Value

                                let mappings =
                                    if isNull (box t.Mappings) then
                                        Map.empty
                                    else
                                        t.Mappings
                                        |> Seq.map (fun kv2 -> kv2.Key, kv2.Value)
                                        |> Map.ofSeq

                                { Name = kv.Key
                                  Description = if isNull t.Description then "" else t.Description
                                  VoiceLeadingCost = t.VoiceLeadingCost
                                  Mappings = mappings })
                            |> Seq.toList

                    let toCycles (src: ResizeArray<CycleYaml>) : Cycle list =
                        if isNull (box src) then
                            []
                        else
                            src
                            |> Seq.map (fun c ->
                                let name = if isNull c.Name then "" else c.Name

                                let triads =
                                    if isNull (box c.Triads) then
                                        []
                                    else
                                        c.Triads |> Seq.toList

                                ({ Name = name; Triads = triads }: Cycle))
                            |> Seq.toList

                    transforms, toCycles data.HexatonicCycles, toCycles data.OctatonicCycles
            with _ ->
                [], [], []
        | None -> [], [], []

    let private catalog = lazy (loadCatalog ())

    // ── Public API ────────────────────────────────────────────────────────────

    /// All six neo-Riemannian transforms (P, L, R, S, N, H).
    let GetAllTransforms () : NeoRiemannianTransform seq =
        let transforms, _, _ = catalog.Value
        transforms :> seq<_>

    /// Look up a single transform by name (case-sensitive: "P", "L", "R", "S", "N", "H").
    let TryGetTransform (name: string) : NeoRiemannianTransform option =
        let transforms, _, _ = catalog.Value
        transforms |> List.tryFind (fun t -> t.Name = name)

    /// Apply a named transform to a triad. Returns `None` if the transform
    /// name is unknown or the triad is not in the transform's domain.
    let Apply (transform: string) (triad: Triad) : Triad option =
        match TryGetTransform transform with
        | Some t -> Map.tryFind triad t.Mappings
        | None -> None

    /// The four hexatonic cycles (P-L alternation, 6 triads each).
    let GetHexatonicCycles () : Cycle seq =
        let _, hex, _ = catalog.Value
        hex :> seq<_>

    /// The three octatonic cycles (P-R alternation, 8 triads each).
    let GetOctatonicCycles () : Cycle seq =
        let _, _, oct = catalog.Value
        oct :> seq<_>
